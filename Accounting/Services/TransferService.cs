using CreditsApp.Accounting.Messaging;
using CreditsApp.Accounting.Models;
using CreditsApp.Data;
using Microsoft.EntityFrameworkCore;

namespace CreditsApp.Accounting.Services;

public class TransferService
{
    private readonly AppDbContext _db;

    public TransferService(AppDbContext db) => _db = db;

    public async Task<Transfer> CreateAsync(CreateTransferCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Amount <= 0) throw new ArgumentException("Amount must be > 0.");

        // Idempotency quick path
        var existing = await _db.Transfers
            .FirstOrDefaultAsync(t => t.IdempotencyKey == cmd.IdempotencyKey, ct);
        if (existing != null) return existing;

        // Load participants
        var fromUser = await _db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Id == cmd.FromUserId, ct)
            ?? throw new InvalidOperationException("Sender not found.");

        var toUser = await _db.Users
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Handle == cmd.ToHandle, ct)
            ?? throw new InvalidOperationException("Recipient not found.");

        if (fromUser.Account.Id == toUser.Account.Id)
            throw new InvalidOperationException("Cannot transfer to self.");

        // Use a transaction with SERIALIZABLE isolation to avoid double-spend under concurrency
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        // Recompute fresh balance within the transaction
        var balance = await GetBalanceAsync(fromUser.Account.Id, _db, ct);
        if (balance < cmd.Amount)
            throw new InvalidOperationException("Insufficient credits.");

        var transfer = new Transfer
        {
            Id = Guid.NewGuid(),
            FromAccountId = fromUser.Account.Id,
            ToAccountId = toUser.Account.Id,
            Amount = decimal.Round(cmd.Amount, 2, MidpointRounding.AwayFromZero),
            Message = string.IsNullOrWhiteSpace(cmd.Message) ? null : cmd.Message!.Trim(),
            IdempotencyKey = cmd.IdempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Succeeded"
        };

        _db.Transfers.Add(transfer);

        // Two ledger rows: debit sender, credit recipient
        _db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = fromUser.Account.Id,
            TransferId = transfer.Id,
            Amount = transfer.Amount,
            Type = EntryType.Debit,
            CreatedAt = transfer.CreatedAt
        });

        _db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = toUser.Account.Id,
            TransferId = transfer.Id,
            Amount = transfer.Amount,
            Type = EntryType.Credit,
            CreatedAt = transfer.CreatedAt
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return transfer;
    }

    private static async Task<decimal> GetBalanceAsync(Guid accountId, AppDbContext db, CancellationToken ct)
    {
        var credits = await db.LedgerEntries
            .Where(e => e.AccountId == accountId && e.Type == EntryType.Credit)
            .Select(e => (double?)e.Amount)
            .SumAsync(ct) ?? 0.0;
        
        var debits = await db.LedgerEntries
            .Where(e => e.AccountId == accountId && e.Type == EntryType.Debit)
            .Select(e => (double?)e.Amount)
            .SumAsync(ct) ?? 0.0;
        
        return (decimal)(credits - debits);
    }
}