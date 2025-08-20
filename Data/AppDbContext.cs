using CreditsApp.Account.Models;
using CreditsApp.Accounting.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CreditsApp.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public DbSet<Account.Models.Account> Accounts => Set<Account.Models.Account>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Transfer> Transfers => Set<Transfer>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>()
            .HasIndex(u => u.Handle).IsUnique();

        b.Entity<Account.Models.Account>()
            .HasOne(a => a.User)
            .WithOne(u => u.Account)
            .HasForeignKey<Account.Models.Account>(a => a.UserId);

        b.Entity<LedgerEntry>()
            .HasIndex(e => new { e.AccountId, e.CreatedAt });

        b.Entity<Transfer>()
            .HasIndex(t => t.IdempotencyKey).IsUnique();
    }
}
