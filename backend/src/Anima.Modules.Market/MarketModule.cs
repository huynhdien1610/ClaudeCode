using System.Security.Claims;
using Anima.Catalog;
using Anima.Collection;
using Anima.Contracts;
using Anima.Identity.Contracts;
using Anima.SharedKernel;
using Anima.Wallet;
using Microsoft.Extensions.Hosting;

namespace Anima.Market;

public sealed record CreateListingRequest(Guid CardInstanceId, string? Currency, long Price, int Days);

/// <summary>Quy tắc chợ (BR-MKT-04/05, US-06.1). Khoảng giá theo rarity là giá tạm chờ PO chốt (Q-19).</summary>
public static class MarketRules
{
    public const int FeePercent = 5;
    public static readonly int[] Durations = [1, 3, 7];

    /// <summary>Khoảng giá bằng Coin; Gem = Coin ÷ 10 (tối thiểu 1).</summary>
    public static readonly IReadOnlyDictionary<string, (long Min, long Max)> CoinRange = new Dictionary<string, (long, long)>
    {
        ["common"] = (10, 5_000),
        ["uncommon"] = (50, 20_000),
        ["rare"] = (100, 50_000),
        ["epic"] = (500, 200_000),
        ["legendary"] = (2_000, 1_000_000),
        ["secret"] = (5_000, 5_000_000),
    };

    public static (long Min, long Max) Range(string rarity, string currency)
    {
        var (min, max) = CoinRange[rarity];
        return currency == Currencies.Coin ? (min, max) : (Math.Max(1, min / 10), Math.Max(1, max / 10));
    }

    /// <summary>Phí Seller: 5% giá, làm tròn lên đến đơn vị nguyên (BR-MKT-05). 999 → 50, 10 → 1.</summary>
    public static long Fee(long price) => (price * FeePercent + 99) / 100;
}

public sealed class MarketModule : IModule
{
    public string Name => "market";
    public System.Reflection.Assembly MigrationAssembly => typeof(MarketModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<MarketService>();
        s.AddScoped<IStatsContributor, MarketStats>();
        s.AddHostedService<ListingExpiryWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/market").RequireAuthorization();
        g.MapGet("/rules", () => Results.Ok(new
        {
            feePercent = MarketRules.FeePercent,
            durations = MarketRules.Durations,
            ranges = MarketRules.CoinRange.Select(r => new { rarity = r.Key, coin = new { min = r.Value.Min, max = r.Value.Max }, gem = new { min = MarketRules.Range(r.Key, "GEM").Min, max = MarketRules.Range(r.Key, "GEM").Max } }),
        }));
        g.MapGet("/listings", async (ClaimsPrincipal u, string? element, string? rarity, string? type, string? currency, string? q, string? sort, int? limit, int? offset, string? locale, MarketService s, CancellationToken ct) =>
            Results.Ok(await s.BrowseAsync(u.AccountId(), new Browse(element, rarity, type, currency, q, sort, Math.Clamp(limit ?? 24, 1, 60), Math.Max(0, offset ?? 0), locale), ct)));
        g.MapGet("/mine", async (ClaimsPrincipal u, string? locale, MarketService s, CancellationToken ct) => Results.Ok(await s.MineAsync(u.AccountId(), locale, ct)));
        g.MapPost("/listings", async (ClaimsPrincipal u, CreateListingRequest r, MarketService s, CancellationToken ct) => Results.Created("/v1/market/mine", await s.CreateAsync(u.AccountId(), r, ct)));
        g.MapPost("/listings/{id:guid}/buy", async (HttpRequest req, ClaimsPrincipal u, Guid id, MarketService s, IIdempotency idem, CancellationToken ct) =>
        {
            var key = req.RequireIdempotencyKey(); var acc = u.AccountId();
            return Results.Ok(await idem.RunAsync(acc, key, "market.buy", Idempotency.Hash(id), () => s.BuyAsync(acc, id, ct), ct));
        });
        g.MapPost("/listings/{id:guid}/cancel", async (ClaimsPrincipal u, Guid id, MarketService s, CancellationToken ct) => Results.Ok(await s.CancelAsync(u.AccountId(), id, ct)));
    }
}

public sealed record Browse(string? Element, string? Rarity, string? Type, string? Currency, string? Q, string? Sort, int Limit, int Offset, string? Locale);

public sealed class MarketService(IUnitOfWork uow, IClock clock, IIdentityApi identity, IWalletApi wallet, ICatalogApi catalog, ICollectionApi collection)
{
    private sealed record Row(Guid Id, Guid Seller, Guid Card, int DefId, string Rarity, string Currency, long Price, long? Fee, string Status, Guid? Buyer, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? ClosedAt);
    private const string Cols = "id,seller_id,card_instance_id,card_definition_id,rarity,currency,price,fee,status,buyer_id,created_at,expires_at,closed_at";
    private static Row Map(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetInt32(3), r.GetString(4), r.GetString(5), r.GetInt64(6), r.IsDBNull(7) ? null : r.GetInt64(7), r.GetString(8),
        r.IsDBNull(9) ? null : r.GetGuid(9), r.GetFieldValue<DateTimeOffset>(10), r.GetFieldValue<DateTimeOffset>(11), r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12));

    /// <summary>BR-MKT-01: chỉ Verified Player, tài khoản không Restricted/bị khóa.</summary>
    private async Task RequireTraderAsync(Guid acc, CancellationToken ct)
    {
        var info = await identity.GetAsync(acc, ct);
        if (info.Status is "Restricted" or "Banned" or "PendingDeletion" or "Deleted") throw new DomainException(ErrorCodes.AccountRestricted, "This account cannot trade", 403);
        if (!info.PhoneVerified) throw new DomainException(ErrorCodes.PhoneVerificationRequired, "Verify your phone number to use the market", 403);
    }

    /// <summary>Đóng các listing quá hạn và trả thẻ về chủ (SC-MKT-15). Gọi lười ở mỗi thao tác và định kỳ bởi worker.</summary>
    public Task<int> ExpireDueAsync(CancellationToken ct) => uow.RunAsync(async () =>
    {
        var ids = await uow.QueryAsync("UPDATE market.listing SET status='Expired', closed_at=@now WHERE status='Active' AND expires_at <= @now RETURNING card_instance_id", r => r.GetGuid(0), ct, ("now", clock.UtcNow));
        if (ids.Count > 0) await collection.ReleaseListedAsync(ids, ct);
        return ids.Count;
    }, ct);

    // ---------- Niêm yết (US-06.1) ----------
    public Task<object> CreateAsync(Guid acc, CreateListingRequest r, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        if (!Currencies.IsValid(r.Currency)) throw DomainException.Validation("currency must be GEM or COIN");
        if (!MarketRules.Durations.Contains(r.Days)) throw DomainException.Validation("days must be 1, 3 or 7");
        await RequireTraderAsync(acc, ct);
        await ExpireDueAsync(ct);

        var card = (await collection.GetManyAsync([r.CardInstanceId], ct)).FirstOrDefault();
        if (card is null || card.OwnerId != acc || card.State == "Burned") throw new DomainException(ErrorCodes.CardNotOwned, "Card not found in your collection", 404);
        if (card.Soulbound) throw new DomainException(ErrorCodes.CardNotTradable, "Account-bound cards cannot be traded", 403);                    // BR-MKT-03
        if (card.State != "Owned") throw DomainException.Conflict(ErrorCodes.CardLocked, "This card is locked");                                  // BR-MKT-02
        var def = await catalog.GetCardAsync(card.CardDefinitionId, ct) ?? throw DomainException.NotFound("Card");
        var (min, max) = MarketRules.Range(def.Rarity, r.Currency!);
        if (r.Price < min || r.Price > max) throw new DomainException(ErrorCodes.PriceOutOfRange, $"Price must be between {min} and {max} {r.Currency}", 400, new { min, max, currency = r.Currency });

        if (!await collection.TrySetStateAsync(card.Id, acc, "Owned", "Listed", ct)) throw DomainException.Conflict(ErrorCodes.CardLocked, "This card is locked");
        var now = clock.UtcNow;
        var row = (await uow.QueryAsync($@"INSERT INTO market.listing(id,seller_id,card_instance_id,card_definition_id,rarity,currency,price,status,created_at,expires_at)
            VALUES(@i,@s,@c,@d,@r,@cur,@p,'Active',@now,@exp) RETURNING {Cols}", Map, ct,
            ("i", Guid.NewGuid()), ("s", acc), ("c", card.Id), ("d", def.Id), ("r", def.Rarity), ("cur", r.Currency), ("p", r.Price), ("now", now), ("exp", now.AddDays(r.Days))))[0];
        return await ToView(row, acc, "en", ct);
    }, ct);

    // ---------- Mua nguyên tử (US-06.2) ----------
    public Task<object> BuyAsync(Guid acc, Guid id, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        await RequireTraderAsync(acc, ct);
        await ExpireDueAsync(ct);
        // Khóa dòng: hai người mua cùng lúc xếp hàng, người sau thấy Sold (SC-MKT-02).
        var l = (await uow.QueryAsync($"SELECT {Cols} FROM market.listing WHERE id=@i FOR UPDATE", Map, ct, ("i", id))).FirstOrDefault() ?? throw DomainException.NotFound("Listing");
        if (l.Seller == acc) throw new DomainException(ErrorCodes.SelfPurchaseForbidden, "You cannot buy your own listing", 403);
        if (l.Status != "Active" || l.ExpiresAt <= clock.UtcNow) throw DomainException.Conflict(ErrorCodes.ListingNotAvailable, "This listing is no longer available");

        var fee = MarketRules.Fee(l.Price);
        await wallet.DebitAsync(acc, l.Currency, l.Price, "MARKET_BUY", "listing", l.Id.ToString(), $"market-buy:{l.Id}", ct);                 // thiếu tiền → INSUFFICIENT_BALANCE, mọi thứ rollback
        await wallet.CreditAsync(l.Seller, l.Currency, l.Price - fee, "MARKET_SALE", "listing", l.Id.ToString(), $"market-sale:{l.Id}", ct);
        await collection.TransferListedAsync(l.Card, acc, ct);
        await uow.ExecAsync("UPDATE market.listing SET status='Sold', buyer_id=@b, fee=@f, closed_at=@now WHERE id=@i", ct, ("b", acc), ("f", fee), ("now", clock.UtcNow), ("i", id));
        return new { listingId = l.Id, cardInstanceId = l.Card, currency = l.Currency, price = l.Price, fee, balances = await wallet.GetBalancesAsync(acc, ct) };
    }, ct);

    // ---------- Hủy (SC-MKT-15) ----------
    public Task<object> CancelAsync(Guid acc, Guid id, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        var l = (await uow.QueryAsync($"SELECT {Cols} FROM market.listing WHERE id=@i AND seller_id=@s FOR UPDATE", Map, ct, ("i", id), ("s", acc))).FirstOrDefault() ?? throw DomainException.NotFound("Listing");
        if (l.Status != "Active") throw DomainException.Conflict(ErrorCodes.ListingNotAvailable, "This listing is no longer active");                // đã Sold/Expired thì không đổi
        await uow.ExecAsync("UPDATE market.listing SET status='Cancelled', closed_at=@now WHERE id=@i", ct, ("now", clock.UtcNow), ("i", id));
        await collection.ReleaseListedAsync([l.Card], ct);
        return new { listingId = l.Id, status = "Cancelled" };
    }, ct);

    // ---------- Duyệt chợ ----------
    private async Task<object> ToView(Row l, Guid viewer, string locale, CancellationToken ct)
    {
        var def = (await catalog.GetCardAsync(l.DefId, ct))!;
        var inst = (await collection.GetManyAsync([l.Card], ct)).First();
        return new
        {
            id = l.Id,
            status = l.Status,
            currency = l.Currency,
            price = l.Price,
            fee = l.Fee ?? MarketRules.Fee(l.Price),
            sellerReceives = l.Price - (l.Fee ?? MarketRules.Fee(l.Price)),
            createdAt = l.CreatedAt,
            expiresAt = l.ExpiresAt,
            closedAt = l.ClosedAt,
            mine = l.Seller == viewer,
            boughtByMe = l.Buyer == viewer,
            cardInstanceId = l.Card,
            serial = inst.Serial,
            edition = $"#{inst.EditionNo}/{def.MaxSupply}",
            card = await catalog.ToPublicAsync(def, locale, false, ct),
        };
    }

    public async Task<object> BrowseAsync(Guid acc, Browse b, CancellationToken ct)
    {
        await ExpireDueAsync(ct);
        var loc = Locales.Normalize(b.Locale ?? (await identity.GetAsync(acc, ct)).Locale);
        var defs = (await catalog.ListCardsAsync(ct)).ToDictionary(d => d.Id);
        var order = b.Sort switch { "price_asc" => "price ASC, created_at DESC", "price_desc" => "price DESC, created_at DESC", _ => "created_at DESC" };
        // Lọc theo thuộc tính thẻ (hệ, loại, tên) ở tầng ứng dụng vì chúng nằm ở module Catalog; lọc rarity/tiền tệ ở SQL.
        var rows = await uow.QueryAsync($"SELECT {Cols} FROM market.listing WHERE status='Active' AND (@r::text IS NULL OR rarity=@r) AND (@c::text IS NULL OR currency=@c) ORDER BY {order}", Map, ct, ("r", b.Rarity), ("c", b.Currency));
        var filtered = rows.Where(l =>
        {
            var d = defs[l.DefId];
            return (b.Element is null || d.Element == b.Element) && (b.Type is null || d.CardType == b.Type) && (string.IsNullOrWhiteSpace(b.Q) || d.Name.Contains(b.Q, StringComparison.OrdinalIgnoreCase));
        }).ToList();
        var page = new List<object>();
        foreach (var l in filtered.Skip(b.Offset).Take(b.Limit)) page.Add(await ToView(l, acc, loc, ct));
        return new { total = filtered.Count, items = page };
    }

    public async Task<object> MineAsync(Guid acc, string? locale, CancellationToken ct)
    {
        await ExpireDueAsync(ct);
        var loc = Locales.Normalize(locale ?? (await identity.GetAsync(acc, ct)).Locale);
        var rows = await uow.QueryAsync($"SELECT {Cols} FROM market.listing WHERE seller_id=@a OR buyer_id=@a ORDER BY created_at DESC LIMIT 50", Map, ct, ("a", acc));
        var items = new List<object>();
        foreach (var l in rows) items.Add(await ToView(l, acc, loc, ct));
        return new { items };
    }
}

/// <summary>Đóng listing quá hạn định kỳ để thẻ không bị khóa mãi khi không ai mở chợ.</summary>
public sealed class ListingExpiryWorker(IServiceScopeFactory scopes, ILogger<ListingExpiryWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MarketService>().ExpireDueAsync(stop);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e) { log.LogWarning(e, "Listing expiry sweep failed"); }
        }
    }
}

internal sealed class MarketStats(IUnitOfWork uow) : IStatsContributor
{
    public async Task<IReadOnlyDictionary<string, long>> CollectAsync(CancellationToken ct)
    {
        var v = (await uow.QueryAsync(@"SELECT count(*) FILTER (WHERE status='Active'), count(*) FILTER (WHERE status='Sold'),
            COALESCE(sum(price) FILTER (WHERE status='Sold' AND currency='COIN'),0)::bigint, COALESCE(sum(price) FILTER (WHERE status='Sold' AND currency='GEM'),0)::bigint,
            COALESCE(sum(fee) FILTER (WHERE status='Sold' AND currency='COIN'),0)::bigint, COALESCE(sum(fee) FILTER (WHERE status='Sold' AND currency='GEM'),0)::bigint FROM market.listing",
            r => new[] { r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5) }, ct))[0];
        return new Dictionary<string, long> { ["listings_active"] = v[0], ["listings_sold"] = v[1], ["market_volume_coin"] = v[2], ["market_volume_gem"] = v[3], ["market_fee_coin"] = v[4], ["market_fee_gem"] = v[5] };
    }
}
