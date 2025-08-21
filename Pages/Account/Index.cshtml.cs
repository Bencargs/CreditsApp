using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CreditsApp.Account.Models;
using CreditsApp.Accounting.Messaging;
using CreditsApp.Accounting.Models;
using CreditsApp.Accounting.Services;
using CreditsApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CreditsApp.Pages.Account;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<AppUser> _users;
    private readonly AppDbContext _db;
    private readonly TransferService _transfers;

    public IndexModel(UserManager<AppUser> users, AppDbContext db, TransferService transfers)
    {
        _users = users;
        _db = db;
        _transfers = transfers;
    }

    public decimal Balance { get; set; }
    public string Handle { get; set; } = "";
    [BindProperty] public TransferInput Transfer { get; set; } = new();
    public string? ResultMessage { get; set; }

    public class TransferInput
    {
        [Required, Display(Name = "Recipient handle")]
        public string ToHandle { get; set; } = "";

        [Range(0.01, 1_000_000), Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [MaxLength(280)]
        public string? Message { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var myId = Guid.Parse(_users.GetUserId(User)!);
        
        var me = await _users.Users
            .Include(u => u.Account)
            .FirstAsync(u => u.Id == myId);
        await EnsureHandleAndAccountAsync(me);

        Balance = await GetBalanceAsync(me.Account!.Id);
        Handle  = me.Handle ?? "";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadBalanceAndHandle();
            return Page();
        }

        var me = await _users.Users.Include(u => u.Account)
            .FirstAsync(u => u.Id == Guid.Parse(_users.GetUserId(User)!));

        try
        {
            var cmd = new CreateTransferCommand(
                ToHandle: Transfer.ToHandle.Trim(),
                Amount: decimal.Round(Transfer.Amount, 2, MidpointRounding.AwayFromZero),
                Message: string.IsNullOrWhiteSpace(Transfer.Message) ? null : Transfer.Message!.Trim(),
                IdempotencyKey: $"ui-{Guid.NewGuid()}",
                FromUserId: me.Id);

            await _transfers.CreateAsync(cmd, HttpContext.RequestAborted);
            ResultMessage = "Transfer sent.";
        }
        catch (Exception ex)
        {
            ResultMessage = ex.Message;
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        await LoadBalanceAndHandle();
        return Page();
    }

    private async Task LoadBalanceAndHandle()
    {
        var me = await _users.Users.Include(u => u.Account)
            .FirstAsync(u => u.Id == Guid.Parse(_users.GetUserId(User)!));
        Balance = await GetBalanceAsync(me.Account!.Id);
        Handle = me.Handle ?? "";
    }

    private async Task EnsureHandleAndAccountAsync(AppUser me)
    {
        var changed = false;

        // Ensure a unique handle
        if (string.IsNullOrWhiteSpace(me.Handle))
        {
            var baseHandle = "@" + (me.Email?.Split('@')[0].ToLowerInvariant() ?? "user");
            var candidate = baseHandle;
            var i = 1;
            while (await _db.Users.AnyAsync(u => u.Handle == candidate))
                candidate = baseHandle + i++;
            me.Handle = candidate;
            changed = true;
        }

        // Ensure an Account exists (check DB, not just the null nav)
        if (me.Account == null)
        {
            var existing = await _db.Accounts.SingleOrDefaultAsync(a => a.UserId == me.Id);
            if (existing == null)
            {
                existing = new CreditsApp.Account.Models.Account { Id = Guid.NewGuid(), UserId = me.Id };
                _db.Accounts.Add(existing);
                changed = true;
            }
            me.Account = existing; // populate the nav so callers can use it
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
            await _db.Entry(me).Reference(u => u.Account).LoadAsync();
        }
        
        var hasEntries = await _db.LedgerEntries.AnyAsync(e => e.AccountId == me.Account!.Id);
        if (!hasEntries)
        {
            await AddInitialCredits(me);
        }
    }

    private async Task AddInitialCredits(AppUser me)
    {
        // Get system account
        var systemUser = await _db.Users.FirstAsync(u => u.Email == Constants.SystemEmail);
        var systemAccount = await _db.Accounts.FirstAsync(a => a.UserId == systemUser.Id);

        var now = DateTimeOffset.UtcNow;
        var transferId = Guid.NewGuid();

        _db.Transfers.Add(new Transfer
        {
            Id = transferId,
            FromAccountId = systemAccount.Id,
            ToAccountId = me.Account.Id,
            Amount = Constants.InitialCredits,
            Message = "Initial signup bonus",
            IdempotencyKey = $"init-{me.Account.Id}",
            CreatedAt = now,
            Status = "Succeeded"
            // If you added CreatedAtUtc earlier:
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


    private async Task<decimal> GetBalanceAsync(Guid accountId)
    {
        var credits = await _db.LedgerEntries
            .Where(e => e.AccountId == accountId && e.Type == EntryType.Credit)
            .Select(e => (double?)e.Amount)
            .SumAsync() ?? 0.0;

        var debits = await _db.LedgerEntries
            .Where(e => e.AccountId == accountId && e.Type == EntryType.Debit)
            .Select(e => (double?)e.Amount)
            .SumAsync() ?? 0.0;

        return (decimal)(credits - debits);
    }
}
