using System.Text.Json;
using Anima.Contracts;
using Anima.SharedKernel;

namespace Anima.Catalog;

public sealed record RarityOdds(string Rarity, int Ppm);
public sealed record OddsVersion(int Id, string PackCode, int Version, IReadOnlyList<RarityOdds> Entries);
public sealed record PackDefinition(string Code, string Name, string Kind, long? PriceCoin, long? PriceGem, int CardsPerPack, bool OnSale, string? OffSaleReason, string SeasonId);
public sealed record CardDefinition(int Id, string Code, string Name, string SeasonId, string Element, string Rarity, string CardType, int Cost,
    int? Atk, int? Def, int? Hp, string? Skill, string? Arc, int MaxSupply, int Issued, int Burned);
public sealed record CardTranslation(string? Epithet, string Story);

public interface ICatalogApi
{
    Task<PackDefinition?> GetPackAsync(string code, CancellationToken ct);
    Task<OddsVersion> GetActiveOddsAsync(string packCode, CancellationToken ct);
    Task<OddsVersion> GetOddsAsync(int versionId, CancellationToken ct);
    /// <summary>Card Definition còn bản của mùa đang mở, sắp theo id (SAD 8.2 bước 4).</summary>
    Task<IReadOnlyList<int>> AvailableCardIdsAsync(string rarity, CancellationToken ct, string? cardType = null, string? element = null);
    /// <summary>Tăng <c>issued</c> nguyên tử. Trả về số thứ tự edition, hoặc null nếu vừa hết bản.</summary>
    Task<int?> TryIssueAsync(int cardDefinitionId, CancellationToken ct);
    Task MarkBurnedAsync(int cardDefinitionId, CancellationToken ct);
    /// <summary>BR-SUP-04: ngừng bán pack khi một rarity hết sạch bản; không tự hạ tỷ lệ.</summary>
    Task SetPackOffSaleAsync(string packCode, string reason, CancellationToken ct);
    Task<CardDefinition?> GetCardAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<CardDefinition>> ListCardsAsync(CancellationToken ct);
    Task<CardTranslation?> GetTranslationAsync(int cardId, string locale, CancellationToken ct);
}

public sealed class CatalogModule : IModule
{
    public string Name => "catalog";
    public System.Reflection.Assembly MigrationAssembly => typeof(CatalogModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<ICatalogApi, CatalogService>();
        s.AddScoped<ICatalogAdminApi, CatalogAdminService>();
        s.AddScoped<IModuleSeeder, CatalogSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Công khai: tỷ lệ rơi phải xem được trước khi thanh toán (BR-PACK-01).
        app.MapGet("/v1/packs", async (ICatalogApi c, CancellationToken ct) =>
        {
            var list = new List<object>();
            foreach (var code in new[] { "awakening-standard" })
                list.Add(await PackView(c, code, ct));
            return Results.Ok(list);
        });
        app.MapGet("/v1/packs/{code}", async (string code, ICatalogApi c, CancellationToken ct) => Results.Ok(await PackView(c, code, ct)));
        app.MapGet("/v1/forge/odds", async (ICatalogApi c, CancellationToken ct) => Results.Ok(await PackView(c, "forge", ct)));

        app.MapGet("/v1/cards", async (string? locale, ICatalogApi c, CancellationToken ct) =>
        {
            var loc = Locales.Normalize(locale);
            var cards = await c.ListCardsAsync(ct);
            var result = new List<object>(cards.Count);
            foreach (var d in cards) result.Add(await c.ToPublicAsync(d, loc, false, ct));
            return Results.Ok(result);
        });
    }

    private static async Task<object> PackView(ICatalogApi c, string code, CancellationToken ct)
    {
        var p = await c.GetPackAsync(code, ct) ?? throw DomainException.NotFound("Pack");
        var odds = p.Kind == "welcome" ? null : await c.GetActiveOddsAsync(code, ct);
        return new { pack = p, odds = odds is null ? null : new { odds.Version, entries = odds.Entries } };
    }
}

internal sealed class CatalogService(IUnitOfWork uow) : ICatalogApi
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    public async Task<PackDefinition?> GetPackAsync(string code, CancellationToken ct) =>
        (await uow.QueryAsync("SELECT code,name,kind,price_coin,price_gem,cards_per_pack,on_sale,off_sale_reason,season_id FROM catalog.pack_definition WHERE code=@c",
            r => new PackDefinition(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetInt64(3), r.IsDBNull(4) ? null : r.GetInt64(4), r.GetInt32(5), r.GetBoolean(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8)), ct, ("c", code))).FirstOrDefault();

    private static OddsVersion MapOdds(Npgsql.NpgsqlDataReader r) =>
        new(r.GetInt32(0), r.GetString(1), r.GetInt32(2), JsonSerializer.Deserialize<List<RarityOdds>>(r.GetString(3), J)!);

    public async Task<OddsVersion> GetActiveOddsAsync(string packCode, CancellationToken ct) =>
        (await uow.QueryAsync("SELECT id,pack_code,version,entries::text FROM catalog.odds_version WHERE pack_code=@p AND status IN ('approved','active') AND effective_from <= now() ORDER BY effective_from DESC, version DESC LIMIT 1", MapOdds, ct, ("p", packCode)))
        .FirstOrDefault() ?? throw DomainException.NotFound("Odds version");

    public async Task<OddsVersion> GetOddsAsync(int id, CancellationToken ct) =>
        (await uow.QueryAsync("SELECT id,pack_code,version,entries::text FROM catalog.odds_version WHERE id=@i", MapOdds, ct, ("i", id))).FirstOrDefault() ?? throw DomainException.NotFound("Odds version");

    public async Task<IReadOnlyList<int>> AvailableCardIdsAsync(string rarity, CancellationToken ct, string? cardType = null, string? element = null) =>
        await uow.QueryAsync(@"SELECT d.id FROM catalog.card_definition d JOIN catalog.edition e ON e.card_definition_id=d.id JOIN catalog.season s ON s.id=d.season_id
            WHERE d.rarity=@r AND d.published AND NOT d.discontinued AND s.status='open' AND e.issued < e.max_supply AND (@t::text IS NULL OR d.card_type=@t) AND (@el::text IS NULL OR d.element=@el) ORDER BY d.id",
            r => r.GetInt32(0), ct, ("r", rarity), ("t", cardType), ("el", element));

    public async Task<int?> TryIssueAsync(int id, CancellationToken ct) =>
        await uow.ScalarAsync<int?>("UPDATE catalog.edition SET issued = issued + 1 WHERE card_definition_id=@i AND issued < max_supply RETURNING issued", ct, ("i", id));

    public Task MarkBurnedAsync(int id, CancellationToken ct) =>
        uow.ExecAsync("UPDATE catalog.edition SET burned = burned + 1 WHERE card_definition_id=@i", ct, ("i", id));

    public Task SetPackOffSaleAsync(string code, string reason, CancellationToken ct) =>
        uow.ExecAsync("UPDATE catalog.pack_definition SET on_sale=false, off_sale_reason=@r WHERE code=@c AND on_sale", ct, ("r", reason), ("c", code));

    private const string CardSql = @"SELECT d.id,d.code,d.name,d.season_id,d.element,d.rarity,d.card_type,d.resonance_cost,d.atk,d.def,d.hp,d.skill_id,d.arc_id,e.max_supply,e.issued,e.burned
        FROM catalog.card_definition d JOIN catalog.edition e ON e.card_definition_id=d.id WHERE d.published";
    private static CardDefinition MapCard(Npgsql.NpgsqlDataReader r) => new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetInt32(7),
        r.IsDBNull(8) ? null : r.GetInt32(8), r.IsDBNull(9) ? null : r.GetInt32(9), r.IsDBNull(10) ? null : r.GetInt32(10), r.IsDBNull(11) ? null : r.GetString(11), r.IsDBNull(12) ? null : r.GetString(12), r.GetInt32(13), r.GetInt32(14), r.GetInt32(15));

    public async Task<CardDefinition?> GetCardAsync(int id, CancellationToken ct) =>
        (await uow.QueryAsync(CardSql + " AND d.id=@i", MapCard, ct, ("i", id))).FirstOrDefault();

    public async Task<IReadOnlyList<CardDefinition>> ListCardsAsync(CancellationToken ct) =>
        await uow.QueryAsync(CardSql + " ORDER BY d.id", MapCard, ct);

    public async Task<CardTranslation?> GetTranslationAsync(int cardId, string locale, CancellationToken ct)
    {
        // Thiếu bản dịch thì dùng en (BR-I18N-02, chuỗi thường).
        var rows = await uow.QueryAsync("SELECT locale, epithet, story FROM catalog.card_translation WHERE card_definition_id=@i AND locale IN (@l,'en')",
            r => (Loc: r.GetString(0), Ep: r.IsDBNull(1) ? null : r.GetString(1), St: r.GetString(2)), ct, ("i", cardId), ("l", locale));
        var pick = rows.FirstOrDefault(x => x.Loc == locale);
        if (pick.Loc is null) pick = rows.FirstOrDefault();
        return pick.Loc is null ? null : new CardTranslation(pick.Ep, pick.St);
    }
}

/// <summary>Biểu diễn công khai của một Card Definition (dùng chung cho API catalog, bộ sưu tập, mở pack, rèn).</summary>
public static class CardProjection
{
    public static async Task<object> ToPublicAsync(this ICatalogApi c, CardDefinition d, string locale, bool withStory, CancellationToken ct)
    {
        var t = await c.GetTranslationAsync(d.Id, locale, ct);
        return new
        {
            id = d.Id,
            code = d.Code,
            name = d.Name,
            epithet = t?.Epithet,
            story = withStory ? t?.Story : null,
            season = d.SeasonId,
            element = d.Element,
            rarity = d.Rarity,
            type = d.CardType,
            cost = d.Cost,
            atk = d.Atk,
            def = d.Def,
            hp = d.Hp,
            skill = d.Skill,
            arc = d.Arc,
            supply = new { max = d.MaxSupply, issued = d.Issued, burned = d.Burned, remaining = d.MaxSupply - d.Issued },
        };
    }
}
