using System.ComponentModel.DataAnnotations;
using CreditsApp.Account.Models;
using CreditsApp.Accounting.Models;
using CreditsApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CreditsApp.Pages.Transactions;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<AppUser> _users;
    private readonly AppDbContext _db;

    public IndexModel(UserManager<AppUser> users, AppDbContext db)
    {
        _users = users;
        _db = db;
    }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; } = 1;

    public List<Row> Rows { get; set; } = new();

    public class Row
    {
        public Guid Id { get; set; }
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}")]
        public DateTimeOffset CreatedAt { get; set; }
        public string Direction { get; set; } = ""; // "Sent" or "Received"
        public string? CounterpartyHandle { get; set; }
        public string? CounterpartyEmail { get; set; }
        public decimal Amount { get; set; } // Signed: negative for sent, positive for received
        public string? Message { get; set; }
    }

    public async Task OnGetAsync(int page = 1)
    {
        Page = Math.Max(1, page);

        var me = await _users.Users
            .Include(u => u.Account)
            .SingleAsync(u => u.Id == Guid.Parse(_users.GetUserId(User)!));
        
        await EnsureAccountAndInitialCreditAsync(me);

        var myAccountId = me.Account!.Id;

        // Join Transfers to Accounts/Users to fetch counterparties in one query.
        var baseQuery =
            from t in _db.Transfers.AsNoTracking()
            join aFrom in _db.Accounts on t.FromAccountId equals aFrom.Id
            join uFrom in _db.Users on aFrom.UserId equals uFrom.Id
            join aTo in _db.Accounts on t.ToAccountId equals aTo.Id
            join uTo in _db.Users on aTo.UserId equals uTo.Id
            where t.FromAccountId == myAccountId || t.ToAccountId == myAccountId
            orderby t.CreatedAtUtc descending
            select new
            {
                t.Id,
                t.CreatedAt,
                t.Amount,
                t.Message,
                FromId = t.FromAccountId,
                ToId = t.ToAccountId,
                FromHandle = uFrom.Handle,
                FromEmail = uFrom.Email,
                ToHandle = uTo.Handle,
                ToEmail = uTo.Email
            };

        var total = await baseQuery.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var pageIndex = Page - 1;

        var data = await baseQuery
            .Skip(pageIndex * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Rows = data.Select(x =>
        {
            var sent = x.FromId == myAccountId;
            return new Row
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                Direction = sent ? "Sent" : "Received",
                CounterpartyHandle = sent ? x.ToHandle : x.FromHandle,
                CounterpartyEmail = sent ? x.ToEmail : x.FromEmail,
                Amount = sent ? -x.Amount : x.Amount,
                Message = x.Message
            };
        }).ToList();
    }
    
    private async Task EnsureAccountAndInitialCreditAsync(AppUser me)
    {
        // Ensure Account exists
        if (me.Account == null)
        {
            var existing = await _db.Accounts.SingleOrDefaultAsync(a => a.UserId == me.Id);
            if (existing == null)
            {
                existing = new CreditsApp.Account.Models.Account { Id = Guid.NewGuid(), UserId = me.Id };
                _db.Accounts.Add(existing);
                await _db.SaveChangesAsync();
            }
            me.Account = existing;
        }

        // If the account has no ledger entries yet, grant initial credits
        var hasEntries = await _db.LedgerEntries.AnyAsync(e => e.AccountId == me.Account!.Id);
        if (!hasEntries)
        {
            // Fetch system account (created by your Seed)
            var systemUser = await _db.Users.FirstAsync(u => u.Email == Constants.SystemEmail);
            var systemAccount = await _db.Accounts.FirstAsync(a => a.UserId == systemUser.Id);

            var now = DateTimeOffset.UtcNow;
            var transferId = Guid.NewGuid();

            _db.Transfers.Add(new Transfer
            {
                Id = transferId,
                FromAccountId = systemAccount.Id,
                ToAccountId = me.Account.Id,
                Amount = Constants.InitialCredits, // 50,000
                Message = "Initial signup bonus",
                IdempotencyKey = $"init-{me.Account.Id}",
                CreatedAt = now,
                Status = "Succeeded"
                // If you added CreatedAtUtc, set it here too
                // CreatedAtUtc = now.UtcDateTime
            });

            _db.LedgerEntries.AddRange(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    AccountId = systemAccount.Id,
                    TransferId = transferId,
                    Amount = Constants.InitialCredits,
                    Type = EntryType.Debit,
                    CreatedAt = now
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    AccountId = me.Account.Id,
                    TransferId = transferId,
                    Amount = Constants.InitialCredits,
                    Type = EntryType.Credit,
                    CreatedAt = now
                }
            );

            await _db.SaveChangesAsync();
        }
    }
}
