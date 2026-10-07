using System.Security.Claims;
using Anima.Catalog;
using Anima.Contracts;
using Anima.SharedKernel;

namespace Anima.Collection;

public sealed record CardInstance(Guid Id, long Serial, int CardDefinitionId, int EditionNo, Guid OwnerId, string State, bool Soulbound, string OriginType, string? OriginId, DateTimeOffset CreatedAt);

public interface ICollectionApi
{
    Task<CardInstance> GrantAsync(Guid accountId, int cardDefinitionId, int editionNo, bool soulbound, string originType, string? originId, CancellationToken ct);
    /// <summary>Khóa dòng (FOR UPDATE, theo thứ tự id để tránh deadlock) các thẻ; trả về những thẻ tìm thấy.</summary>
    Task<IReadOnlyList<CardInstance>> LockAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task BurnAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    /// <summary>Đọc (không khóa) các thẻ theo id, thuộc bất kỳ chủ nào; thẻ không tồn tại bị bỏ qua.</summary>
    Task<IReadOnlyList<CardInstance>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    /// <summary>Số thẻ gắn chặt tài khoản (còn sở hữu) theo Card Definition — dùng cho BR-NEW-05.</summary>
    Task<IReadOnlyDictionary<int, int>> SoulboundCountsAsync(Guid accountId, CancellationToken ct);
}

public sealed class CollectionModule : IModule
{
    public string Name => "collection";
    public System.Reflection.Assembly MigrationAssembly => typeof(CollectionModule).Assembly;
    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<CollectionService>();
        s.AddScoped<ICollectionApi>(sp => sp.GetRequiredService<CollectionService>());
        s.AddScoped<IStatsContributor, CollectionStats>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1").RequireAuthorization();
        g.MapGet("/collection", async (ClaimsPrincipal u, string? locale, string? element, string? rarity, string? type, string? q, CollectionService s, ICatalogApi cat, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(u.AccountId(), Locales.Normalize(locale), element, rarity, type, q, cat, ct)));
        g.MapGet("/collection/progress", async (ClaimsPrincipal u, CollectionService s, ICatalogApi cat, CancellationToken ct) => Results.Ok(await s.ProgressAsync(u.AccountId(), cat, ct)));
        g.MapGet("/cards/{id:int}", async (ClaimsPrincipal u, int id, string? locale, CollectionService s, ICatalogApi cat, CancellationToken ct) =>
        {
            var d = await cat.GetCardAsync(id, ct) ?? throw DomainException.NotFound("Card");
            // US-05.4: chỉ thẻ đã từng sở hữu mới đọc được Story Fragment.
            var seen = await s.HasDiscoveredAsync(u.AccountId(), id, ct);
            return Results.Ok(await cat.ToPublicAsync(d, Locales.Normalize(locale), seen, ct));
        });
    }
}

public sealed class CollectionService(IUnitOfWork uow, IClock clock) : ICollectionApi
{
    private const string Cols = "id,serial,card_definition_id,edition_no,owner_id,state,soulbound,origin_type,origin_id,created_at";
    private static CardInstance Map(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetInt64(1), r.GetInt32(2), r.GetInt32(3), r.GetGuid(4), r.GetString(5), r.GetBoolean(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetFieldValue<DateTimeOffset>(9));

    public async Task<IReadOnlyDictionary<int, int>> SoulboundCountsAsync(Guid acc, CancellationToken ct) =>
        (await uow.QueryAsync("SELECT card_definition_id, count(*)::int FROM collection.card_instance WHERE owner_id=@a AND soulbound AND state <> 'Burned' GROUP BY 1",
            r => (Id: r.GetInt32(0), N: r.GetInt32(1)), ct, ("a", acc))).ToDictionary(x => x.Id, x => x.N);

    public async Task<CardInstance> GrantAsync(Guid accountId, int defId, int editionNo, bool soulbound, string originType, string? originId, CancellationToken ct)
    {
        var rows = await uow.QueryAsync($@"INSERT INTO collection.card_instance(id,card_definition_id,edition_no,owner_id,state,soulbound,origin_type,origin_id,created_at)
            VALUES(@id,@d,@e,@o,'Owned',@sb,@ot,@oi,@now) RETURNING {Cols}", Map, ct,
            ("id", Guid.NewGuid()), ("d", defId), ("e", editionNo), ("o", accountId), ("sb", soulbound), ("ot", originType), ("oi", originId), ("now", clock.UtcNow));
        await uow.ExecAsync("INSERT INTO collection.discovery(account_id,card_definition_id,first_at) VALUES(@a,@d,@now) ON CONFLICT DO NOTHING", ct, ("a", accountId), ("d", defId), ("now", clock.UtcNow));
        return rows[0];
    }

    public async Task<IReadOnlyList<CardInstance>> LockAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await uow.QueryAsync($"SELECT {Cols} FROM collection.card_instance WHERE id = ANY(@ids) ORDER BY id FOR UPDATE", Map, ct, ("ids", ids.ToArray()));

    public async Task<IReadOnlyList<CardInstance>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await uow.QueryAsync($"SELECT {Cols} FROM collection.card_instance WHERE id = ANY(@ids)", Map, ct, ("ids", ids.ToArray()));

    public Task BurnAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        uow.ExecAsync("UPDATE collection.card_instance SET state='Burned' WHERE id = ANY(@ids)", ct, ("ids", ids.ToArray()));

    public async Task<bool> HasDiscoveredAsync(Guid acc, int defId, CancellationToken ct) =>
        await uow.ScalarAsync<int?>("SELECT 1 FROM collection.discovery WHERE account_id=@a AND card_definition_id=@d", ct, ("a", acc), ("d", defId)) is not null;

    public async Task<object> ListAsync(Guid acc, string locale, string? element, string? rarity, string? type, string? q, ICatalogApi cat, CancellationToken ct)
    {
        var inst = await uow.QueryAsync($"SELECT {Cols} FROM collection.card_instance WHERE owner_id=@a AND state <> 'Burned' ORDER BY created_at DESC, serial DESC", Map, ct, ("a", acc));
        var defs = (await cat.ListCardsAsync(ct)).ToDictionary(d => d.Id);
        var items = new List<object>();
        foreach (var i in inst)
        {
            var d = defs[i.CardDefinitionId];
            if (element is not null && d.Element != element) continue;
            if (rarity is not null && d.Rarity != rarity) continue;
            if (type is not null && d.CardType != type) continue;
            if (!string.IsNullOrWhiteSpace(q) && !d.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            items.Add(new
            {
                id = i.Id,
                serial = i.Serial,
                edition = $"#{i.EditionNo}/{d.MaxSupply}",
                editionNo = i.EditionNo,
                state = i.State,
                soulbound = i.Soulbound,
                origin = i.OriginType,
                acquiredAt = i.CreatedAt,
                card = await cat.ToPublicAsync(d, locale, false, ct),
            });
        }
        return new { total = items.Count, items };
    }

    /// <summary>US-05.2: "X/Y" với X là số Card Definition khác nhau đang sở hữu (bản trùng không cộng).</summary>
    public async Task<object> ProgressAsync(Guid acc, ICatalogApi cat, CancellationToken ct)
    {
        var owned = await uow.QueryAsync("SELECT DISTINCT card_definition_id FROM collection.card_instance WHERE owner_id=@a AND state <> 'Burned'", r => r.GetInt32(0), ct, ("a", acc));
        var total = (await cat.ListCardsAsync(ct)).Count;
        return new { owned = owned.Count, total, percent = total == 0 ? 0 : Math.Round(100.0 * owned.Count / total, 1) };
    }
}

internal sealed class CollectionStats(IUnitOfWork uow) : IStatsContributor
{
    public async Task<IReadOnlyDictionary<string, long>> CollectAsync(CancellationToken ct)
    {
        var v = (await uow.QueryAsync("SELECT count(*), count(*) FILTER (WHERE state='Burned'), count(*) FILTER (WHERE soulbound) FROM collection.card_instance", r => new[] { r.GetInt64(0), r.GetInt64(1), r.GetInt64(2) }, ct))[0];
        return new Dictionary<string, long> { ["cards_issued"] = v[0], ["cards_burned"] = v[1], ["cards_soulbound"] = v[2] };
    }
}
