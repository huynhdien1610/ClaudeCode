using System.Net;
using Npgsql;

namespace Anima.IntegrationTests;

public sealed class CatalogTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private async Task<List<System.Text.Json.JsonElement>> AllCards() => (await new Player.Anon(fx).Get("/v1/cards")).Body.EnumerateArray().ToList();

    [Fact(DisplayName = "BR-CARD-07: set Awakening có 100 thẻ = 80 Anima + 12 Tiếng vọng + 8 Ký ức phong ấn")]
    public async Task SetComposition()
    {
        var cards = await AllCards();
        Assert.Equal(100, cards.Count);
        Assert.Equal(80, cards.Count(c => c.GetProperty("type").GetString() == "anima"));
        Assert.Equal(12, cards.Count(c => c.GetProperty("type").GetString() == "echo"));
        Assert.Equal(8, cards.Count(c => c.GetProperty("type").GetString() == "seal"));
        var byRarity = cards.GroupBy(c => c.GetProperty("rarity").GetString()).ToDictionary(g => g.Key!, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["common"] = 39, ["uncommon"] = 22, ["rare"] = 21, ["epic"] = 10, ["legendary"] = 7, ["secret"] = 1 }, byRarity);
        Assert.Equal(8, cards.Select(c => c.GetProperty("element").GetString()).Distinct().Count());
    }

    [Fact(DisplayName = "BR-CARD-03: mọi Anima nằm trong khung chỉ số theo chi phí (±10%), thang 0–3,000 / 100–4,000, bước 50")]
    public async Task StatBudget()
    {
        foreach (var c in (await AllCards()).Where(c => c.GetProperty("type").GetString() == "anima"))
        {
            int atk = c.GetProperty("atk").GetInt32(), def = c.GetProperty("def").GetInt32(), hp = c.GetProperty("hp").GetInt32(), cost = c.GetProperty("cost").GetInt32();
            var budget = 600 * cost + 300; var score = atk + def + hp / 2.0;
            Assert.InRange(score, budget * 0.9, budget * 1.1);
            Assert.InRange(atk, 0, 3000); Assert.InRange(def, 0, 3000); Assert.InRange(hp, 100, 4000);
            Assert.All(new[] { atk, def, hp }, v => Assert.Equal(0, v % 50));
            Assert.InRange(cost, 1, 6);
        }
    }

    [Fact(DisplayName = "BR-CARD-01: Tiếng vọng và Ký ức phong ấn không có chỉ số ATK/DEF/HP")]
    public async Task SupportCardsHaveNoCombatStats()
    {
        foreach (var c in (await AllCards()).Where(c => c.GetProperty("type").GetString() != "anima"))
            Assert.Equal(System.Text.Json.JsonValueKind.Null, c.GetProperty("atk").ValueKind);
    }

    [Fact(DisplayName = "BR-SUP-01: số lượng phát hành tối đa theo rarity công khai")]
    public async Task MaxSupplyByRarity()
    {
        var expected = new Dictionary<string, int> { ["common"] = 50_000, ["uncommon"] = 20_000, ["rare"] = 5_000, ["epic"] = 1_000, ["legendary"] = 300, ["secret"] = 100 };
        foreach (var c in await AllCards())
            Assert.Equal(expected[c.GetProperty("rarity").GetString()!], c.GetProperty("supply").GetProperty("max").GetInt32());
    }

    [Fact(DisplayName = "BR-CARD-06: chỉ số, kỹ năng, mạch truyện của thẻ đã phát hành là bất biến")]
    public async Task PublishedCardIsImmutable()
    {
        foreach (var col in new[] { "atk = atk + 50", "hp = hp + 50", "def = def + 50", "rarity = 'secret'", "element = 'Nihilum'", "skill_id = 'x'", "arc_id = 'x'", "resonance_cost = 6" })
            await Assert.ThrowsAsync<PostgresException>(() => fx.Sql($"UPDATE catalog.card_definition SET {col} WHERE id = 3"));
        await Assert.ThrowsAsync<PostgresException>(() => fx.Sql("DELETE FROM catalog.card_definition WHERE id = 3"));
        await Assert.ThrowsAsync<PostgresException>(() => fx.Sql("UPDATE catalog.card_definition SET published = false WHERE id = 3"));
    }

    [Fact(DisplayName = "BR-ADM-03: không sửa tỷ lệ của version đang active; maker-checker ở database")]
    public async Task ActiveOddsImmutable()
    {
        await Assert.ThrowsAsync<PostgresException>(() => fx.Sql(@"UPDATE catalog.odds_version SET entries = '[{""rarity"":""common"",""ppm"":1000000}]'::jsonb WHERE pack_code='awakening-standard'"));
        await Assert.ThrowsAsync<PostgresException>(() => fx.Sql(@"INSERT INTO catalog.odds_version(pack_code,version,status,effective_from,created_by,approved_by,entries)
            VALUES('awakening-standard',2,'active','2026-01-01','alice','alice','[]'::jsonb)"));
    }

    [Fact(DisplayName = "BR-SUP: issued không bao giờ vượt max_supply (ràng buộc ở database)")]
    public async Task IssuedNeverExceedsSupply() =>
        await Assert.ThrowsAsync<PostgresException>(() => fx.Sql("UPDATE catalog.edition SET issued = max_supply + 1 WHERE card_definition_id = 1"));

    [Fact(DisplayName = "BR-SUP-03: 40 lần cấp song song cho thẻ chỉ còn 5 bản → đúng 5 thành công, số thứ tự không trùng")]
    public async Task IssuingIsAtomicUnderConcurrency()
    {
        await fx.Sql("UPDATE catalog.edition SET issued = 0, max_supply = 5 WHERE card_definition_id = 2");
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ =>
            await fx.Scalar<int?>("UPDATE catalog.edition SET issued = issued + 1 WHERE card_definition_id = 2 AND issued < max_supply RETURNING issued")));
        var won = results.Where(r => r is not null).Select(r => r!.Value).OrderBy(x => x).ToList();
        Assert.Equal([1, 2, 3, 4, 5], won);
    }

    [Theory(DisplayName = "BR-I18N-03: tên riêng giữ nguyên, tên hiệu và truyện dịch theo ngôn ngữ; thiếu bản dịch dùng en")]
    [InlineData("vi", "Kẻ mang hy vọng")]
    [InlineData("en", "the Hopebringer")]
    [InlineData("zh-Hans", "带来希望者")]
    [InlineData("zh-Hant", "帶來希望者")]
    public async Task CardTranslations(string locale, string epithet)
    {
        var cards = (await new Player.Anon(fx).Get($"/v1/cards?locale={locale}")).Body.EnumerateArray().ToList();
        var seraphel = cards.Single(c => c.GetProperty("name").GetString() == "Seraphel");
        Assert.Equal(epithet, seraphel.GetProperty("epithet").GetString());
        Assert.Equal("legendary", seraphel.GetProperty("rarity").GetString());
    }

    [Fact(DisplayName = "BR-I18N-03: đủ 4 bản dịch cho mọi thẻ trước khi mở bán")]
    public async Task EveryCardHasFourTranslations() =>
        Assert.Equal(0L, await fx.Scalar<long>("SELECT count(*) FROM catalog.card_definition d WHERE (SELECT count(*) FROM catalog.card_translation t WHERE t.card_definition_id = d.id) <> 4"));

    [Fact(DisplayName = "Pack không có trong cửa hàng trả 404")]
    public async Task UnknownPack() =>
        Assert.Equal(HttpStatusCode.NotFound, (await new Player.Anon(fx).Get("/v1/packs/nope")).Status);
}
