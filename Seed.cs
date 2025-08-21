using CreditsApp.Account.Models;
using CreditsApp.Accounting.Models;
using CreditsApp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CreditsApp;

public static class Seed
{
    public static async Task RunAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // 1) Ensure System user exists
        var systemUser = await users.Users.FirstOrDefaultAsync(u => u.Email == Constants.SystemEmail);
        if (systemUser == null)
        {
            systemUser = new AppUser
            {
                UserName = Constants.SystemEmail,
                Email = Constants.SystemEmail,
                Handle = Constants.SystemHandle
            };
            // No one logs into this; but Identity requires a created user row
            await users.CreateAsync(systemUser, Guid.NewGuid().ToString("N") + "aA1!");
        }

        // 2) Ensure System account exists
        var systemAccount = await db.Accounts.SingleOrDefaultAsync(a => a.UserId == systemUser.Id);
        if (systemAccount == null)
        {
            systemAccount = new Account.Models.Account { Id = Guid.NewGuid(), UserId = systemUser.Id };
            db.Accounts.Add(systemAccount);
            await db.SaveChangesAsync();
        }

        // 3) Backfill: give InitialCredits to any account with NO ledger entries yet (safe to run every boot)
        var accountsNeedingInitial = await db.Accounts
            .Where(a => !db.LedgerEntries.Any(e => e.AccountId == a.Id))
            .ToListAsync();

        if (accountsNeedingInitial.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var acct in accountsNeedingInitial)
            {
                // Skip system’s own account
                if (acct.Id == systemAccount.Id) continue;

                var transferId = Guid.NewGuid();
                db.Transfers.Add(new Transfer
                {
                    Id = transferId,
                    FromAccountId = systemAccount.Id,
                    ToAccountId = acct.Id,
                    Amount = Constants.InitialCredits,
                    Message = "Initial signup bonus",
                    IdempotencyKey = $"init-{acct.Id}",
                    CreatedAt = now,
                    Status = "Succeeded",
                    CreatedAtUtc = now.UtcDateTime
                });

                db.LedgerEntries.AddRange(
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
                        AccountId = acct.Id,
                        TransferId = transferId,
                        Amount = Constants.InitialCredits,
                        Type = EntryType.Credit,
                        CreatedAt = now
                    }
                );
            }

            await db.SaveChangesAsync();
        }
    }
}