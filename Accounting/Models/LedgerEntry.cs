namespace CreditsApp.Accounting.Models;

public class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Account.Models.Account Account { get; set; } = default!;
    public Guid TransferId { get; set; }
    public decimal Amount { get; set; } // always positive
    public EntryType Type { get; set; } // credit/debit
    public DateTimeOffset CreatedAt { get; set; }
}