using System.Net;
using System.Text.Json;

namespace Anima.IntegrationTests;

public sealed class PackTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private static readonly string[] Order = ["common", "uncommon", "rare", "epic", "legendary", "secret"];

    [Fact(DisplayName = "BR-PACK-01: tỷ lệ rơi công khai, không cần đăng nhập, tổng 100%")]
    public async Task OddsArePublic()
    {
        var r = await new Player.Anon(fx).Get("/v1/packs/awakening-standard");
        var entries = r["odds"].GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(1_000_000, entries.Sum(e => e.GetProperty("ppm").GetInt32()));
        Assert.Equal(6, entries.Count);
        Assert.Equal(1000, r["pack"].GetProperty("priceCoin").GetInt64());
        Assert.Equal(100, r["pack"].GetProperty("priceGem").GetInt64());
    }

    [Fact(DisplayName = "US-03.1: không đủ tiền → INSUFFICIENT_BALANCE, không tạo pack, không trừ tiền")]
    public async Task PurchaseWithoutFunds()
    {
        var p = await fx.NewPlayer();                                    // 100 Coin, giá 1,000 Coin
        var r = await p.Post("/v1/packs/awakening-standard/purchase", new { currency = "COIN", quantity = 1 });
        Assert.Equal(HttpStatusCode.PaymentRequired, r.Status);
        Assert.Equal("INSUFFICIENT_BALANCE", r.Code);
        Assert.Equal((0, 100), await p.Balances());
        Assert.Single((await p.Get("/v1/me/packs")).Body.EnumerateArray());          // chỉ còn gói chào mừng
    }

    [Fact(DisplayName = "SC-PACK-05: mua hai lần cùng Idempotency-Key chỉ trừ một lần, trả cùng pack")]
    public async Task PurchaseIsIdempotent()
    {
        var p = await fx.NewPlayer(); await p.TopUp(200);
        var a = await p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 }, "key-1");
        var b = await p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 }, "key-1");
        Assert.Equal(a["packInstanceIds"].ToString(), b["packInstanceIds"].ToString());
        Assert.Equal(100, (await p.Balances()).Gem);
    }

    [Fact(DisplayName = "SC-PACK-05: hai yêu cầu cùng key đồng thời chỉ tạo một lần mua")]
    public async Task ConcurrentSameKeyChargesOnce()
    {
        var p = await fx.NewPlayer(); await p.TopUp(200);
        var rs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 }, "same")));
        Assert.All(rs, r => Assert.True(r.Ok, r.Body.ToString()));
        Assert.Single(rs.Select(r => r["packInstanceIds"].ToString()).Distinct());
        Assert.Equal(100, (await p.Balances()).Gem);
    }

    [Fact(DisplayName = "BR-PACK-06 / SC-PACK-21: mở pack ra 5 thẻ, lật theo rarity tăng dần")]
    public async Task OpenReturnsFiveCardsInAscendingRarity()
    {
        var p = await fx.NewPlayer();
        for (var i = 0; i < 5; i++)
        {
            await p.TopUp(100);
            var r = await p.Open((await p.Buy(1))[0]);
            var cards = r["cards"].EnumerateArray().ToList();
            Assert.Equal(5, cards.Count);
            var idx = cards.Select(c => Array.IndexOf(Order, c.GetProperty("rarity").GetString())).ToList();
            Assert.Equal(idx.OrderBy(x => x), idx);
            Assert.Equal(Order[idx[^1]], r["maxRarity"].GetString());
            Assert.Equal(idx[^1] >= 3, r["climax"].GetBoolean());                  // BR-PACK-07: Climax khi có Epic+
        }
    }

    [Fact(DisplayName = "BR-SUP-02: mỗi thẻ có serial duy nhất toàn hệ thống và số thứ tự edition `#n/N`")]
    public async Task SerialsAndEditionNumbers()
    {
        var p = await fx.NewPlayer();
        var cards = await p.BuyAndOpen(4);
        Assert.Equal(20, cards.Select(c => c.GetProperty("serial").GetInt64()).Distinct().Count());
        foreach (var c in cards)
        {
            var max = c.GetProperty("card").GetProperty("supply").GetProperty("max").GetInt32();
            Assert.Equal($"#{c.GetProperty("editionNo").GetInt32()}/{max}", c.GetProperty("edition").GetString());
            Assert.InRange(c.GetProperty("editionNo").GetInt32(), 1, max);
        }
        Assert.Equal(0L, await fx.Scalar<long>("SELECT count(*) FROM (SELECT card_definition_id, edition_no FROM collection.card_instance GROUP BY 1,2 HAVING count(*) > 1) d"));
        Assert.Equal(0L, await fx.Scalar<long>("SELECT count(*) FROM catalog.edition e WHERE e.issued <> (SELECT count(*) FROM collection.card_instance c WHERE c.card_definition_id = e.card_definition_id)"));
    }

    [Fact(DisplayName = "SC-PACK-13: mở cùng một pack hai lần → PACK_ALREADY_OPENED")]
    public async Task OpenTwice()
    {
        var p = await fx.NewPlayer(); await p.TopUp(100);
        var id = (await p.Buy(1))[0];
        Assert.True((await p.Open(id)).Ok);
        var again = await p.Open(id);
        Assert.Equal(HttpStatusCode.Conflict, again.Status);
        Assert.Equal("PACK_ALREADY_OPENED", again.Code);
        Assert.Equal(5, (await p.Collection()).Count);
    }

    [Fact(DisplayName = "SC-PACK-13: nhiều yêu cầu mở đồng thời chỉ một thành công, chỉ cấp 5 thẻ")]
    public async Task ConcurrentOpen()
    {
        var p = await fx.NewPlayer(); await p.TopUp(100);
        var id = (await p.Buy(1))[0];
        var rs = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => p.Open(id)));
        Assert.Equal(1, rs.Count(r => r.Ok));
        Assert.All(rs.Where(r => !r.Ok), r => Assert.Equal("PACK_ALREADY_OPENED", r.Code));
        Assert.Equal(5, (await p.Collection()).Count);
    }

    [Fact(DisplayName = "PACK_NOT_OWNED: không mở được pack của người khác")]
    public async Task CannotOpenOthersPack()
    {
        var a = await fx.NewPlayer(); var b = await fx.NewPlayer(); await a.TopUp(100);
        var id = (await a.Buy(1))[0];
        var r = await b.Open(id);
        Assert.Equal("PACK_NOT_OWNED", r.Code);
        Assert.Equal(HttpStatusCode.NotFound, r.Status);
    }

    [Fact(DisplayName = "BR-PACK-05 / SC-PACK-09: pity đạt 49 thì bảo đảm Legendary+, rồi về 0")]
    public async Task PityGuaranteesLegendary()
    {
        var p = await fx.NewPlayer(); var triggered = false;
        for (var attempt = 0; attempt < 12 && !triggered; attempt++)
        {
            await p.TopUp(100);
            await fx.Sql("INSERT INTO gacha.pity_counter(account_id,pack_code,count) VALUES(@a,'awakening-standard',49) ON CONFLICT (account_id,pack_code) DO UPDATE SET count=49", ("a", p.Id));
            var r = await p.Open((await p.Buy(1))[0]);
            Assert.Equal(49, r["pityBefore"].GetInt32());
            Assert.Equal(0, r["pityAfter"].GetInt32());
            Assert.True(r["cards"].EnumerateArray().Any(c => Array.IndexOf(Order, c.GetProperty("rarity").GetString()) >= 4), "phải có Legendary+");
            triggered = r["pityTriggered"].GetBoolean();     // ~81% mỗi lần; lặp lại để gặp đúng trường hợp phải ép
        }
        Assert.True(triggered, "pity chưa bao giờ phải ép Legendary sau 12 lần thử");
    }

    [Fact(DisplayName = "BR-PACK-05: bộ đếm pity tăng 1 sau mỗi pack không có Legendary+")]
    public async Task PityCounterIncrements()
    {
        var p = await fx.NewPlayer();
        var expected = 0;
        for (var i = 0; i < 6; i++)
        {
            await p.TopUp(100);
            var r = await p.Open((await p.Buy(1))[0]);
            Assert.Equal(expected, r["pityBefore"].GetInt32());
            var hasLegendary = r["cards"].EnumerateArray().Any(c => Array.IndexOf(Order, c.GetProperty("rarity").GetString()) >= 4);
            expected = hasLegendary ? 0 : expected + 1;
            Assert.Equal(expected, r["pityAfter"].GetInt32());
        }
        Assert.Equal(expected, (await p.Get("/v1/me/pity/awakening-standard"))["count"].GetInt32());
    }

    [Fact(DisplayName = "BR-PF-04/05: sau khi đổi seed, tính lại được rarity từng slot từ bản ghi mở pack")]
    public async Task OpeningCanBeVerifiedAfterRotation()
    {
        var p = await fx.NewPlayer(); await p.TopUp(100);
        var id = (await p.Buy(1))[0];
        await p.Put("/v1/fairness/client-seed", new { clientSeed = "verify-me" });
        var open = await p.Open(id);
        Assert.True(open.Ok);
        var rec = await p.Get($"/v1/pack-instances/{id}/opening");
        var seedHash = rec["fairness"].GetProperty("seedHash").GetString();
        var nonce = rec["fairness"].GetProperty("nonce").GetInt32();

        var rot = await p.Post("/v1/fairness/rotate");
        var prev = rot["previous"];
        Assert.Equal(seedHash, prev.GetProperty("serverSeedHash").GetString());

        // Người chơi tự tính lại bằng công cụ công khai.
        var verify = await new Player.Anon(fx).Post("/v1/fairness/verify", new { serverSeed = prev.GetProperty("serverSeed").GetString(), clientSeed = "verify-me", nonce, slots = 5 });
        Assert.Equal(seedHash, verify["serverSeedHash"].GetString());
        var rolls = verify["rolls"].EnumerateArray().Select(x => x.GetInt32()).ToArray();

        var odds = (await new Player.Anon(fx).Get("/v1/packs/awakening-standard"))["odds"].GetProperty("entries").EnumerateArray().Select(e => (e.GetProperty("rarity").GetString()!, e.GetProperty("ppm").GetInt32())).ToList();
        string Pick(int roll) { var acc = 0; foreach (var (r, ppm) in odds) { acc += ppm; if (roll < acc) return r; } return odds[^1].Item1; }
        var recomputed = rolls.Select(Pick).ToArray();

        var recorded = rec["raw"].GetProperty("rarityBeforePity").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(recorded, recomputed);
        // Và bản ghi cho thấy rarity cuối cùng của từng slot nhất quán với lúc mở.
        var issued = rec["cards"].EnumerateArray().Select(c => c.GetProperty("card").GetProperty("rarity").GetString()).OrderBy(x => Array.IndexOf(Order, x)).ToArray();
        var opened = open["cards"].EnumerateArray().Select(c => c.GetProperty("rarity").GetString()).ToArray();
        Assert.Equal(opened, issued);
    }

    [Fact(DisplayName = "BR-NEW-01/04: gói chào mừng = 5 Anima Common, 5 hệ khác nhau trong 7 hệ vòng nhân quả, gắn chặt tài khoản")]
    public async Task WelcomePackRules()
    {
        for (var i = 0; i < 5; i++)
        {
            var p = await fx.NewPlayer();
            var packs = await p.Get("/v1/me/packs");
            var id = packs.Body[0].GetProperty("id").GetGuid();
            var r = await p.Open(id);
            Assert.True(r.Ok, r.Body.ToString());
            var cards = r["cards"].EnumerateArray().ToList();
            Assert.Equal(5, cards.Count);
            Assert.All(cards, c =>
            {
                Assert.Equal("common", c.GetProperty("rarity").GetString());
                Assert.Equal("anima", c.GetProperty("card").GetProperty("type").GetString());
                Assert.True(c.GetProperty("soulbound").GetBoolean());
            });
            var elements = cards.Select(c => c.GetProperty("card").GetProperty("element").GetString()!).ToList();
            Assert.Equal(5, elements.Distinct().Count());
            Assert.DoesNotContain("Nihilum", elements);
            Assert.Equal(0, (await p.Get("/v1/me/packs")).Body.GetArrayLength());      // gói chào mừng đã mở
        }
    }

    [Fact(DisplayName = "BR-NEW-04: thẻ gắn chặt tài khoản hiển thị trong bộ sưu tập với cờ soulbound")]
    public async Task WelcomeCardsAreSoulboundInCollection()
    {
        var p = await fx.NewPlayer(); await p.OpenWelcomePackAsync();
        var items = await p.Collection();
        Assert.Equal(5, items.Count);
        Assert.All(items, i => { Assert.True(i.GetProperty("soulbound").GetBoolean()); Assert.Equal("WELCOME", i.GetProperty("origin").GetString()); });
    }

    [Fact(DisplayName = "BR-WEB-01: ví, bộ sưu tập là một bản duy nhất giữa các phiên đăng nhập")]
    public async Task SameStateAcrossSessions()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(1);
        var second = await p.Login();
        var token = second["accessToken"].GetString();
        using var http = fx.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var items = JsonDocument.Parse(await http.GetStringAsync("/v1/collection")).RootElement;
        Assert.Equal(5, items.GetProperty("total").GetInt32());
    }
}
