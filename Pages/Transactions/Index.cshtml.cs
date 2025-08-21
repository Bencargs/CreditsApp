using System.ComponentModel.DataAnnotations;
using CreditsApp.Account.Models;
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
}
