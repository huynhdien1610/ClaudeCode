using System.Security.Claims;
using System.Text;
using Anima.Catalog;
using Anima.Collection;
using Anima.Contracts;
using Anima.Economy;
using Anima.Fairness;
using Anima.Forge;
using Anima.Gacha;
using Anima.Identity;
using Anima.SharedKernel;
using Anima.Wallet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var isDev = builder.Environment.IsDevelopment() || builder.Environment.EnvironmentName == "Testing";

// Thứ tự module = thứ tự chạy migration.
IModule[] modules = [new IdentityModule(), new FairnessModule(), new EconomyModule(), new WalletModule(), new CatalogModule(), new CollectionModule(), new GachaModule(), new ForgeModule()];

builder.Services.AddSharedKernel(builder.Configuration, isDev);
foreach (var m in modules) m.ConfigureServices(builder.Services, builder.Configuration);
builder.Services.AddSingleton<IReadOnlyList<IModule>>(modules);

var idOpt = new IdentityOptions(); builder.Configuration.GetSection("Identity").Bind(idOpt);
if (!isDev && idOpt.JwtKey.Contains("dev", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Identity:JwtKey must be configured outside Development");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = idOpt.JwtIssuer,
        ValidAudience = idOpt.JwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(idOpt.JwtKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    o.Events = new JwtBearerEvents
    {
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { code = ErrorCodes.Unauthorized, message = "Authentication is required" });
        },
    };
});
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins((builder.Configuration["Cors:Origins"] ?? "http://localhost:3000,http://localhost:5173").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Idempotency-Key")));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

app.UseMiddleware<DomainErrorMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/readyz", async (Npgsql.NpgsqlDataSource ds, CancellationToken ct) =>
{
    await using var c = await ds.OpenConnectionAsync(ct);
    await using var cmd = new Npgsql.NpgsqlCommand("SELECT 1", c);
    await cmd.ExecuteScalarAsync(ct);
    return Results.Ok(new { status = "ready" });
});
foreach (var m in modules) m.MapEndpoints(app);

// Chỉ môi trường dev: nạp Gem giả để chơi thử khi chưa có cổng thanh toán (BR-WEB-04, Q-31).
if (app.Configuration.GetValue<bool>("Dev:MockTopUp"))
{
    app.MapPost("/v1/dev/topup", async (HttpRequest req, ClaimsPrincipal u, TopUpRequest r, IWalletApi wallet, IUnitOfWork uow, CancellationToken ct) =>
    {
        var key = req.RequireIdempotencyKey();
        if (r.Gem is < 1 or > 100_000) throw DomainException.Validation("gem must be between 1 and 100000");
        var acc = u.AccountId();
        await uow.RunAsync(() => wallet.CreditAsync(acc, Currencies.Gem, r.Gem, "DEV_TOPUP", "dev", null, $"dev-topup:{key}", ct), ct);
        return Results.Ok(await wallet.GetBalancesAsync(acc, ct));
    }).RequireAuthorization();
}

if (isDev) app.MapOpenApi("/openapi/{documentName}.json");

// Khởi động: migration SQL, rồi dữ liệu khởi tạo (catalog tạm).
if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    var ds = app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>();
    var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");
    var all = new List<(string, System.Reflection.Assembly)> { ("shared", typeof(DomainException).Assembly) };
    all.AddRange(modules.Select(m => (m.Name, m.MigrationAssembly)));
    await Migrator.RunAsync(ds, all, log);
    if (app.Configuration.GetValue("Seed:Enabled", isDev))
    {
        await using var scope = app.Services.CreateAsyncScope();
        foreach (var s in scope.ServiceProvider.GetServices<IModuleSeeder>()) await s.SeedAsync(CancellationToken.None);
    }
}

await app.RunAsync();

public sealed record TopUpRequest(long Gem);

public partial class Program;
