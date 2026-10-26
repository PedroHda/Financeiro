using System.Data;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Financeiro.Api;
using Financeiro.Sync.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024 * 1024);
var provider = builder.Configuration["Database:Provider"] ?? (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? "Sqlite" : "Postgres");
var connectionString = builder.Configuration.GetConnectionString("Financeiro");
if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing")) throw new InvalidOperationException("Use PostgreSQL fora do ambiente de desenvolvimento.");
    builder.Services.AddDbContext<SyncDbContext>(options => options.UseSqlite(connectionString ?? "Data Source=financeiro-api-dev.db"));
}
else
{
    if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Configure ConnectionStrings__Financeiro com a conexao PostgreSQL.");
    builder.Services.AddDbContext<SyncDbContext>(options => options.UseNpgsql(connectionString));
}
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
{
    options.Password.RequiredLength = 12;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<SyncDbContext>();
var keysPath = Path.GetFullPath(builder.Configuration["DataProtection:KeysPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "private", "keys"));
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("Financeiro.Sync.v1");
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (System.Net.IPAddress.TryParse(builder.Configuration["ReverseProxy:KnownProxy"], out var proxy)) options.KnownProxies.Add(proxy);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("sync", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseExceptionHandler();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing") && !context.Request.IsHttps)
    { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
    var registration = app.Configuration.GetValue("Auth:AllowRegistration", app.Environment.IsDevelopment());
    if (!registration && context.Request.Path.Equals("/auth/register", StringComparison.OrdinalIgnoreCase))
    { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGroup("/auth").MapIdentityApi<IdentityUser>().RequireRateLimiting("auth");
app.MapGet("/health", () => Results.Ok(new { status = "ok", protocol = SyncProtocol.Version }));
var api = app.MapGroup("/api").RequireAuthorization().RequireRateLimiting("sync");
api.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new { id = user.FindFirstValue(ClaimTypes.NameIdentifier) }));
api.MapGet("/sync", async (long cursor, long? through, ClaimsPrincipal user, SyncDbContext db, CancellationToken ct) =>
{
    if (cursor < 0 || through < 0) return Results.BadRequest();
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var version = await db.Workspaces.Where(w => w.UserId == userId).Select(w => w.Revision).SingleOrDefaultAsync(ct);
    var ceiling = through ?? version;
    if (cursor > ceiling || ceiling > version) return Results.Conflict(new { message = "Cursor invalido. Reconecte a conta." });
    var records = await db.Records.AsNoTracking().Where(r => r.UserId == userId && r.Revision > cursor && r.Revision <= ceiling).OrderBy(r => r.Revision).Take(SyncProtocol.PageSize + 1).ToListAsync(ct);
    var hasMore = records.Count > SyncProtocol.PageSize;
    var page = records.Take(SyncProtocol.PageSize).Select(r => new SyncRecord(r.Id, r.Table, r.Revision, r.Payload, r.Deleted)).ToArray();
    return Results.Ok(new SyncPullPage(hasMore ? page[^1].Revision : ceiling, ceiling, hasMore, page));
});
api.MapPost("/sync", async (SyncPushRequest request, ClaimsPrincipal user, SyncDbContext db, CancellationToken ct) =>
{
    if (request.Protocol != SyncProtocol.Version || request.WorkspaceRevision < 0 || request.Changes.Count > SyncProtocol.MaxChanges || request.Changes.Select(c => c.Id).Distinct().Count() != request.Changes.Count)
        return Results.BadRequest(new { message = "Protocolo ou lote invalido." });
    foreach (var change in request.Changes)
    {
        if (change.Id == Guid.Empty || change.BaseRevision < 0 || !SyncProtocol.Tables.Contains(change.Table, StringComparer.Ordinal) || change.Deleted != (change.Payload is null)) return Results.BadRequest();
        if (change.Payload is not null)
        {
            if (Encoding.UTF8.GetByteCount(change.Payload) > SyncProtocol.MaxPayloadBytes) return Results.BadRequest();
            try
            {
                using var document = JsonDocument.Parse(change.Payload, new JsonDocumentOptions { MaxDepth = 16 });
                if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Count() > 40) return Results.BadRequest();
            }
            catch (JsonException) { return Results.BadRequest(); }
        }
    }
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    try
    {
        var workspace = await db.Workspaces.SingleOrDefaultAsync(w => w.UserId == userId, ct);
        if (workspace is null) { workspace = new SyncWorkspace { UserId = userId }; db.Workspaces.Add(workspace); }
        if (workspace.Revision != request.WorkspaceRevision) return Results.Conflict(new { message = "Ha novas alteracoes. Atualize antes de enviar." });
        var accepted = new List<SyncRecord>();
        foreach (var change in request.Changes)
        {
            var stored = await db.Records.SingleOrDefaultAsync(r => r.UserId == userId && r.Id == change.Id, ct);
            if (stored is not null && stored.Table != change.Table) return Results.BadRequest();
            if (stored is not null && stored.Payload == change.Payload && stored.Deleted == change.Deleted)
            { accepted.Add(new SyncRecord(stored.Id, stored.Table, stored.Revision, stored.Payload, stored.Deleted)); continue; }
            if ((stored?.Revision ?? 0) != change.BaseRevision) return Results.Conflict(new { message = "Registro alterado por outro aparelho." });
            if (stored is null) { stored = new StoredRecord { UserId = userId, Id = change.Id, Table = change.Table }; db.Records.Add(stored); }
            stored.Payload = change.Payload; stored.Deleted = change.Deleted; stored.Revision = ++workspace.Revision;
            accepted.Add(new SyncRecord(stored.Id, stored.Table, stored.Revision, stored.Payload, stored.Deleted));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new SyncPushResult(workspace.Revision, accepted));
    }
    catch (DbUpdateException)
    { return Results.Conflict(new { message = "Alteracoes concorrentes. Tente novamente." }); }
});
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) await db.Database.EnsureCreatedAsync();
    else await db.Database.MigrateAsync();
}
app.Run();

public partial class Program;
