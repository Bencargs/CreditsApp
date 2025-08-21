namespace CreditsApp.Accounting.Models;

public class Transfer
{
    public Guid Id { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; } // > 0
    public string? Message { get; set; } // optional
    public string IdempotencyKey { get; set; } = default!; // provided by client
    public DateTimeOffset CreatedAt { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = "Succeeded"; // MVP: single state
}