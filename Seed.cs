using CreditsApp.Account.Models;
using CreditsApp.Accounting.Models;
using CreditsApp.Data;
using Microsoft.AspNetCore.Identity;

namespace CreditsApp;

public static class Seed
{
    public static async Task RunAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        if (!db.Users.Any())
        {
            var alice = new AppUser { UserName = "alice@example.com", Email = "alice@example.com", Handle = "@alice" };
            var bob   = new AppUser { UserName = "bob@example.com",   Email = "bob@example.com",   Handle = "@bob" };
            await users.CreateAsync(alice, "Pa$$w0rd");
            await users.CreateAsync(bob,   "Pa$$w0rd");

            // create Accounts (Identity won't auto-create your Account entity)
            db.Accounts.AddRange(
                new Account.Models.Account { Id = Guid.NewGuid(), UserId = alice.Id },
                new Account.Models.Account { Id = Guid.NewGuid(), UserId = bob.Id }
            );
            await db.SaveChangesAsync();

            // Seed initial credits to Alice from a synthetic "System" account
            var system = new Account.Models.Account { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
            db.Accounts.Add(system);
            await db.SaveChangesAsync();

            var transferId = Guid.NewGuid();
            db.LedgerEntries.AddRange(
                new LedgerEntry { Id = Guid.NewGuid(), AccountId = system.Id, TransferId = transferId, Amount = 1000m, Type = EntryType.Debit,  CreatedAt = DateTimeOffset.UtcNow },
                new LedgerEntry { Id = Guid.NewGuid(), AccountId = db.Accounts.First(a => a.UserId == alice.Id).Id, TransferId = transferId, Amount = 1000m, Type = EntryType.Credit, CreatedAt = DateTimeOffset.UtcNow }
            );
            await db.SaveChangesAsync();
        }
    }
}