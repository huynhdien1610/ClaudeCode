using System.Net;

namespace Anima.IntegrationTests;

/// <summary>Lớp riêng (database riêng) vì test làm hết bản của cả một rarity.</summary>
public sealed class SupplyTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    [Fact(DisplayName = "BR-SUP-04: hết bản một rarity → ngừng bán pack, lần mở đang chạy hạ xuống rarity gần nhất còn bản và ghi rõ lý do")]
    public async Task ExhaustedRarityStopsSale()
    {
        var p = await fx.NewPlayer(); await p.TopUp(300);
        var packs = await p.Buy(2);
        // Hết toàn bộ Legendary và Secret.
        await fx.Sql("UPDATE catalog.edition e SET issued = max_supply FROM catalog.card_definition d WHERE d.id = e.card_definition_id AND d.rarity IN ('legendary','secret')");
        await fx.Sql("INSERT INTO gacha.pity_counter(account_id,pack_code,count) VALUES(@a,'awakening-standard',49) ON CONFLICT (account_id,pack_code) DO UPDATE SET count=49", ("a", p.Id));

        var r = await p.Open(packs[0]);
        Assert.True(r.Ok, r.Body.ToString());
        var rarities = r["cards"].EnumerateArray().Select(c => c.GetProperty("rarity").GetString()).ToList();
        Assert.DoesNotContain("legendary", rarities); Assert.DoesNotContain("secret", rarities);
        Assert.Equal(5, rarities.Count);

        // Pity ép Legendary nhưng không còn bản → hạ xuống Epic, ghi trong bản ghi mở pack.
        var rec = await p.Get($"/v1/pack-instances/{packs[0]}/opening");
        Assert.Contains("DOWNGRADED_FROM:legendary", rec.Body.ToString());

        // Pack tự ngừng bán, không hạ tỷ lệ ngầm.
        Assert.Equal("RARITY_EXHAUSTED:legendary", await fx.Scalar<string>("SELECT off_sale_reason FROM catalog.pack_definition WHERE code='awakening-standard'"));
        var buy = await p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, buy.Status);
        Assert.Equal("PACK_NOT_ON_SALE", buy.Code);
        // Pack đã mua trước đó vẫn mở được (BR-PACK-04).
        Assert.True((await p.Open(packs[1])).Ok);
    }
}
