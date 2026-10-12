using System.Security.Claims;
using Anima.Catalog;
using Anima.Contracts;
using Anima.SharedKernel;
using Anima.Wallet;

namespace Anima.Memory;

/// <summary>Quy tắc Ký ức phong ấn (BR-CARD-05, BR-CARD-08, BR-CARD-09).</summary>
public static class MemoryRules
{
    public const int MaxStock = 5;
    /// <summary>Giá Gem tạm thời; Finance chốt giá thật ở Q-58.</summary>
    public const long GemPrice = 50;

    /// <summary>Thứ Hai của tuần chứa <paramref name="at"/> (UTC): mốc đếm giới hạn mua 1 lần mỗi tuần.</summary>
    public static DateOnly WeekStart(DateTimeOffset at)
    {
        var d = DateOnly.FromDateTime(at.UtcDateTime);
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    }
}

/// <summary>Một Ký ức phong ấn trong kho: gắn với một Anima gốc có Passive.</summary>
public sealed record MemoryItem(int DefId, string Name, string Element, string Rarity, string Skill, int Qty);

public sealed record MemoryOverview(IReadOnlyList<MemoryItem> Stock, int Total, int Max, IReadOnlyList<MemoryItem> Available, long GemPrice, bool CanBuyThisWeek, DateOnly WeekStart);

public sealed record BuyMemoryRequest(int? CardDefinitionId);

public interface IMemoryApi
{
    /// <summary>Nhận Ký ức phong ấn từ Story, sự kiện, Battle Pass... Ném MEMORY_STOCK_FULL nếu kho đã đủ 5.</summary>
    Task GrantAsync(Guid accountId, int sourceDefId, CancellationToken ct);
    /// <summary>Kho hiện tại, mỗi cái lặp theo số lượng, dùng để chốt bộ Ký ức mang vào trận.</summary>
    Task<IReadOnlyList<MemoryItem>> StockAsync(Guid accountId, CancellationToken ct);
    /// <summary>Tiêu một cái khi dùng trong trận; ném MEMORY_NOT_AVAILABLE nếu không còn.</summary>
    Task ConsumeAsync(Guid accountId, int sourceDefId, CancellationToken ct);
}

public sealed class MemoryModule : IModule
{
    public string Name => "memory";
    public System.Reflection.Assembly MigrationAssembly => typeof(MemoryModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<MemoryService>();
        s.AddScoped<IMemoryApi>(sp => sp.GetRequiredService<MemoryService>());
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/memories").RequireAuthorization();
        g.MapGet("", async (ClaimsPrincipal u, MemoryService s, CancellationToken ct) => Results.Ok(await s.OverviewAsync(u.AccountId(), ct)));
        g.MapPost("/purchase", async (HttpRequest req, ClaimsPrincipal u, BuyMemoryRequest r, MemoryService s, IIdempotency idem, CancellationToken ct) =>
        {
            var key = req.RequireIdempotencyKey(); var acc = u.AccountId();
            return Results.Ok(await idem.RunAsync(acc, key, "memory.purchase", Idempotency.Hash(r.CardDefinitionId), () => s.BuyWithGemAsync(acc, r.CardDefinitionId, key, ct), ct));
        });
    }
}

public sealed class MemoryService(IUnitOfWork uow, IClock clock, ICatalogApi catalog, IWalletApi wallet) : IMemoryApi
{
    /// <summary>Anima giữ Passive: mỗi cái có đúng một loại Ký ức phong ấn tương ứng.</summary>
    private async Task<Dictionary<int, CardDefinition>> SourcesAsync(CancellationToken ct) =>
        (await catalog.ListCardsAsync(ct)).Where(d => d.CardType == CardTypes.Anima && PassiveSkills.IsPassive(d.Skill)).ToDictionary(d => d.Id);

    private static MemoryItem Item(CardDefinition d, int qty) => new(d.Id, d.Name, d.Element, d.Rarity, d.Skill!, qty);

    private async Task<int> TotalAsync(Guid acc, CancellationToken ct) =>
        await uow.ScalarAsync<int?>("SELECT COALESCE(sum(qty),0)::int FROM memory.stock WHERE account_id=@a", ct, ("a", acc)) ?? 0;

    public async Task GrantAsync(Guid acc, int sourceDefId, CancellationToken ct) => await uow.RunAsync(async () =>
    {
        if (!(await SourcesAsync(ct)).ContainsKey(sourceDefId)) throw DomainException.Validation("This card has no Sealed Memory");
        await uow.ExecAsync("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", ct, ("k", "memory:" + acc));        // chặn hai yêu cầu cùng vượt trần kho
        if (await TotalAsync(acc, ct) >= MemoryRules.MaxStock) throw DomainException.Conflict(ErrorCodes.MemoryStockFull, $"Your Sealed Memory inventory is full ({MemoryRules.MaxStock})");
        await uow.ExecAsync(@"INSERT INTO memory.stock(account_id,source_def_id,qty) VALUES(@a,@d,1)
            ON CONFLICT (account_id,source_def_id) DO UPDATE SET qty = memory.stock.qty + 1", ct, ("a", acc), ("d", sourceDefId));
    }, ct);

    public async Task<IReadOnlyList<MemoryItem>> StockAsync(Guid acc, CancellationToken ct)
    {
        var src = await SourcesAsync(ct);
        var rows = await uow.QueryAsync("SELECT source_def_id,qty FROM memory.stock WHERE account_id=@a AND qty>0 ORDER BY source_def_id", r => (Def: r.GetInt32(0), Qty: r.GetInt32(1)), ct, ("a", acc));
        return rows.Where(r => src.ContainsKey(r.Def)).Select(r => Item(src[r.Def], r.Qty)).ToList();
    }

    public async Task ConsumeAsync(Guid acc, int sourceDefId, CancellationToken ct)
    {
        var n = await uow.ExecAsync("UPDATE memory.stock SET qty = qty - 1 WHERE account_id=@a AND source_def_id=@d AND qty > 0", ct, ("a", acc), ("d", sourceDefId));
        if (n == 0) throw DomainException.Conflict(ErrorCodes.MemoryNotAvailable, "You do not have this Sealed Memory");
    }

    public async Task<MemoryOverview> OverviewAsync(Guid acc, CancellationToken ct)
    {
        var stock = await StockAsync(acc, ct);
        var week = MemoryRules.WeekStart(clock.UtcNow);
        var bought = await uow.ScalarAsync<int?>("SELECT 1 FROM memory.weekly_purchase WHERE account_id=@a AND week_start=@w", ct, ("a", acc), ("w", week)) is not null;
        var avail = (await SourcesAsync(ct)).Values.OrderBy(d => d.Id).Select(d => Item(d, 0)).ToList();
        return new MemoryOverview(stock, stock.Sum(s => s.Qty), MemoryRules.MaxStock, avail, MemoryRules.GemPrice, !bought, week);
    }

    public Task<MemoryOverview> BuyWithGemAsync(Guid acc, int? defId, string idemKey, CancellationToken ct) => uow.RunAsync(async () =>
    {
        if (defId is null) throw DomainException.Validation("cardDefinitionId is required");
        if (!(await SourcesAsync(ct)).ContainsKey(defId.Value)) throw DomainException.Validation("This card has no Sealed Memory");
        var week = MemoryRules.WeekStart(clock.UtcNow);
        var inserted = await uow.ExecAsync(@"INSERT INTO memory.weekly_purchase(account_id,week_start,source_def_id,created_at) VALUES(@a,@w,@d,@now)
            ON CONFLICT (account_id,week_start) DO NOTHING", ct, ("a", acc), ("w", week), ("d", defId.Value), ("now", clock.UtcNow));
        if (inserted == 0) throw DomainException.Conflict(ErrorCodes.MemoryWeeklyLimit, "You can buy only one Sealed Memory with Gem per week");
        await GrantAsync(acc, defId.Value, ct);                                                                            // đầy kho thì ném và rollback cả dòng giới hạn tuần
        await wallet.DebitAsync(acc, "GEM", MemoryRules.GemPrice, "MEMORY_PURCHASE", "memory", defId.Value.ToString(), $"memory:{idemKey}", ct);
        return await OverviewAsync(acc, ct);
    }, ct);
}
