namespace CreditsApp.Account.Models;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
}