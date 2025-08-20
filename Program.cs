using CreditsApp;
using CreditsApp.Account.Models;
using CreditsApp.Accounting.Messaging;
using CreditsApp.Accounting.Services;
using CreditsApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                       ?? "Data Source=./data/credits.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connectionString));

// builder.Services
builder.Services
    .AddDefaultIdentity<AppUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Services
builder.Services.AddScoped<TransferService>();

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Optional: improve concurrent writes
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
    await Seed.RunAsync(app.Services);
}

// middleware
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// --- Minimal endpoints ---
app.MapGet("/", () => Results.Redirect("/Index"));
app.MapGet("/status", () => Results.Ok("ok"));
app.MapPost("/api/transfers", async (
    CreateTransferCommand cmd,
    TransferService svc,
    AppDbContext db,
    CancellationToken ct) =>
{
    // For now, fake current user (replace with real auth later)
    var me = await db.Users.Include(u => u.Account).FirstAsync(ct);
    var fixedCmd = cmd with { FromUserId = me.Id };
    var transfer = await svc.CreateAsync(fixedCmd, ct);
    return Results.Ok(new { transfer.Id, transfer.Amount, transfer.Message, transfer.CreatedAt });
});

app.Run();

public partial class Program { }