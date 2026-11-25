using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using LoyaltyPoints.Api.Data;
using LoyaltyPoints.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Secrets are NEVER stored in appsettings.json. Provide them through environment
// variables (Api__Key, Security__CpfHashKey) or `dotnet user-secrets` in development.
var apiKey = builder.Configuration["Api:Key"];
var cpfHashKey = builder.Configuration["Security:CpfHashKey"];
if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(cpfHashKey))
    throw new InvalidOperationException(
        "Missing configuration. Set Api__Key and Security__CpfHashKey (see README).");

builder.Services.AddDbContext<LoyaltyDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Loyalty") ?? "Data Source=loyalty.db"));

builder.Services.AddSingleton(new PointsCalculator(
    builder.Configuration.GetSection("Points").Get<PointsRules>() ?? new PointsRules()));

// Rate limiting is partitioned by client IP: one noisy client cannot use up
// the quota of everybody else. Sensitive routes get a stricter limit.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("general", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("sensitive", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>().Database.EnsureCreated();

app.UseRateLimiter();

// API key check with a constant-time comparison.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/health"))
    {
        await next();
        return;
    }

    var provided = ctx.Request.Headers["X-Api-Key"].ToString();
    var ok = CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
        SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    if (!ok)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    await next();
});

static AccountView View(Account a) => new(a.Id, a.CpfMasked, a.IsActive, a.PendingPoints, a.Balance);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/accounts", async (CreateAccountRequest req, LoyaltyDbContext db) =>
{
    if (!Cpf.IsValid(req.Cpf))
        return Results.BadRequest(new { error = "Invalid CPF." });

    var hash = Cpf.Hash(req.Cpf, cpfHashKey);
    if (await db.Accounts.AnyAsync(a => a.CpfHash == hash))
        return Results.Conflict(new { error = "Account already exists." });

    var account = new Account { CpfHash = hash, CpfMasked = Cpf.Mask(req.Cpf) };
    db.Accounts.Add(account);
    await db.SaveChangesAsync();
    return Results.Created($"/accounts/{account.Id}", View(account));
}).RequireRateLimiting("general");

app.MapGet("/accounts/{id:guid}", async (Guid id, LoyaltyDbContext db) =>
    await db.Accounts.FindAsync(id) is { } a ? Results.Ok(View(a)) : Results.NotFound())
    .RequireRateLimiting("general");

app.MapPost("/accounts/{id:guid}/purchases", async (Guid id, PurchaseRequest req, LoyaltyDbContext db, PointsCalculator calc) =>
{
    var account = await db.Accounts.FindAsync(id);
    if (account is null) return Results.NotFound();

    try
    {
        var points = account.RegisterPurchase(req.Amount, calc, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Results.Ok(new { pointsEarned = points, account = View(account) });
    }
    catch (DomainException ex)
    {
        return Results.UnprocessableEntity(new { error = ex.Message });
    }
}).RequireRateLimiting("general");

app.MapPost("/accounts/{id:guid}/activate", async (Guid id, LoyaltyDbContext db) =>
{
    var account = await db.Accounts.FindAsync(id);
    if (account is null) return Results.NotFound();

    var released = account.Activate(DateTime.UtcNow);
    await db.SaveChangesAsync();
    return Results.Ok(new { pointsReleased = released, account = View(account) });
}).RequireRateLimiting("sensitive");

app.MapPost("/accounts/{id:guid}/redeem", async (Guid id, RedeemRequest req, LoyaltyDbContext db) =>
{
    var account = await db.Accounts.FindAsync(id);
    if (account is null) return Results.NotFound();

    try
    {
        var redemption = account.Redeem(req.Points, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Results.Ok(new { redemption, account = View(account) });
    }
    catch (DomainException ex)
    {
        return Results.UnprocessableEntity(new { error = ex.Message });
    }
}).RequireRateLimiting("sensitive");

// Bulk import from a plain-text export ("cpf;amount" per line).
app.MapPost("/imports/purchases", async (HttpRequest request, LoyaltyDbContext db, PointsCalculator calc) =>
{
    using var reader = new StreamReader(request.Body);
    var content = await reader.ReadToEndAsync();
    var parsed = PurchaseFileParser.Parse(content.Split('\n').Select(l => l.TrimEnd('\r')));

    int applied = 0, unknownAccount = 0;
    foreach (var line in parsed.Lines)
    {
        var hash = Cpf.Hash(line.CpfDigits, cpfHashKey);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.CpfHash == hash);
        if (account is null) { unknownAccount++; continue; }

        account.RegisterPurchase(line.Amount, calc, DateTime.UtcNow);
        applied++;
    }

    await db.SaveChangesAsync();

    // The summary never includes CPFs.
    return Results.Ok(new { applied, unknownAccount, invalidLines = parsed.Errors.Count, errors = parsed.Errors });
}).RequireRateLimiting("sensitive");

app.Run();

public sealed record CreateAccountRequest(string Cpf);
public sealed record PurchaseRequest(decimal Amount);
public sealed record RedeemRequest(int Points);
public sealed record AccountView(Guid Id, string CpfMasked, bool IsActive, int PendingPoints, int Balance);
