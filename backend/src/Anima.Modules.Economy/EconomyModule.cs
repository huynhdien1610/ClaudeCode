using Anima.SharedKernel;

namespace Anima.Economy;

public interface IEconomyApi
{
    /// <summary>Giá trị tham số đang có hiệu lực tại thời điểm hiện tại.</summary>
    Task<long> GetAsync(string key, CancellationToken ct);
    Task<IReadOnlyDictionary<string, long>> GetAllAsync(CancellationToken ct);
}

public static class EconomyKeys
{
    public const string GemToCoinRate = "gem_to_coin_rate";
    public const string CoinToGemRate = "coin_to_gem_rate";
    public const string CoinToGemDailyCapGem = "coin_to_gem_daily_cap_gem";
    public const string ForgeFeeCoin = "forge_fee_coin";
    public const string ForgeFeeGem = "forge_fee_gem";
    public const string ForgeDailyLimitUnverified = "forge_daily_limit_unverified";
    public const string ForgeDailyLimitVerified = "forge_daily_limit_verified";
    public const string FirstLoginCoin = "first_login_coin";
    public const string PityGuaranteeAfter = "pity_guarantee_after";
}

public sealed class EconomyModule : IModule
{
    public string Name => "economy";
    public System.Reflection.Assembly MigrationAssembly => typeof(EconomyModule).Assembly;
    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<IEconomyApi, EconomyService>();
        s.AddScoped<IEconomyAdminApi, EconomyAdminService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) =>
        app.MapGet("/v1/economy", async (IEconomyApi e, CancellationToken ct) => Results.Ok(await e.GetAllAsync(ct)));
}

internal sealed class EconomyService(IUnitOfWork uow, IClock clock) : IEconomyApi
{
    private const string Sql = @"SELECT DISTINCT ON (key) key, value FROM economy.parameter WHERE effective_from <= @now ORDER BY key, version DESC";

    public async Task<long> GetAsync(string key, CancellationToken ct)
    {
        var all = await GetAllAsync(ct);
        return all.TryGetValue(key, out var v) ? v : throw new InvalidOperationException($"Economy parameter '{key}' is not configured");
    }

    public async Task<IReadOnlyDictionary<string, long>> GetAllAsync(CancellationToken ct) =>
        (await uow.QueryAsync(Sql, r => (r.GetString(0), r.GetInt64(1)), ct, ("now", clock.UtcNow))).ToDictionary(x => x.Item1, x => x.Item2);
}
