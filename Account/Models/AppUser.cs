using Microsoft.AspNetCore.Identity;

namespace CreditsApp.Account.Models;

public class AppUser : IdentityUser<Guid>  // users + auth
{
    public Account Account { get; set; } = default!;
    public string Handle { get; set; } = default!; // e.g. @ben (unique)
}