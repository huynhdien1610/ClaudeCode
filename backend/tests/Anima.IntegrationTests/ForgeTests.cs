using System.Net;

namespace Anima.IntegrationTests;

public sealed class ForgeTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    /// <summary>Người chơi có Coin/Gem định sẵn và ít nhất <paramref name="cards"/> thẻ rèn được (không soulbound).</summary>
    private async Task<(Player P, List<Guid> Cards)> Setup(int cards = 2)
    {
        var p = await fx.NewPlayer();
        var ids = new List<Guid>();
        while (ids.Count < cards)
        {
            await p.BuyAndOpen(1);
            ids = (await p.Collection()).Where(i => !i.GetProperty("soulbound").GetBoolean()).Select(i => i.GetProperty("id").GetGuid()).ToList();
        }
        return (p, ids);
    }

    private static Task<Resp> Forge(Player p, IEnumerable<Guid> ids, string fee = "COIN", string? idem = null) => p.Post("/v1/forge", new { cardInstanceIds = ids, feeCurrency = fee }, idem);

    [Fact(DisplayName = "SC-FRG-01: rèn trả bằng Coin → trừ 50 Coin, 2 thẻ Burned, có 1 thẻ chưa lật, burned của edition +1")]
    public async Task ForgeWithCoin()
    {
        var (p, cards) = await Setup(); await p.GetCoin(500);
        var inputs = cards.Take(2).ToArray();
        var defs = await Task.WhenAll(inputs.Select(i => fx.Scalar<int>("SELECT card_definition_id FROM collection.card_instance WHERE id=@i", ("i", i))));
        var burnedBefore = await Task.WhenAll(defs.Select(d => fx.Scalar<int>("SELECT burned FROM catalog.edition WHERE card_definition_id=@d", ("d", d))));
        var (gem, coin) = await p.Balances();

        var r = await Forge(p, inputs);
        Assert.True(r.Ok, r.Body.ToString());
        Assert.Equal((gem, coin - 50), await p.Balances());
        foreach (var i in inputs) Assert.Equal("Burned", await fx.Scalar<string>("SELECT state FROM collection.card_instance WHERE id=@i", ("i", i)));
        Assert.Single((await p.Get("/v1/me/sealed")).Body.EnumerateArray());
        var after = await Task.WhenAll(defs.Select(d => fx.Scalar<int>("SELECT burned FROM catalog.edition WHERE card_definition_id=@d", ("d", d))));
        Assert.Equal(burnedBefore.Zip(defs).Select(x => x.First + defs.Count(d => d == x.Second)).ToArray(), after);
        var remaining = (await p.Collection()).Select(c => c.GetProperty("id").GetGuid()).ToList();
        foreach (var i in inputs) Assert.DoesNotContain(i, remaining);                      // thẻ đã hủy không còn trong bộ sưu tập
    }

    [Fact(DisplayName = "SC-FRG-02: rèn trả bằng Gem → trừ 5 Gem")]
    public async Task ForgeWithGem()
    {
        var (p, cards) = await Setup(); await p.TopUp(20);
        var (gem, coin) = await p.Balances();
        Assert.True((await Forge(p, cards.Take(2), "GEM")).Ok);
        Assert.Equal((gem - 5, coin), await p.Balances());
    }

    [Fact(DisplayName = "SC-FRG-03: không đủ phí → INSUFFICIENT_BALANCE và hai thẻ vẫn Owned")]
    public async Task InsufficientFeeKeepsCards()
    {
        var (p, cards) = await Setup();
        // Đưa ví về mức không đủ phí rèn (50 Coin hoặc 5 Gem).
        await p.TopUp(5);
        await fx.Sql("UPDATE wallet.balance SET amount = 49 WHERE account_id=@a AND currency='COIN'", ("a", p.Id));
        await fx.Sql("UPDATE wallet.balance SET amount = 4 WHERE account_id=@a AND currency='GEM'", ("a", p.Id));
        var r = await Forge(p, cards.Take(2));
        Assert.Equal("INSUFFICIENT_BALANCE", r.Code);
        foreach (var id in cards.Take(2)) Assert.Equal("Owned", await fx.Scalar<string>("SELECT state FROM collection.card_instance WHERE id=@i", ("i", id)));
        Assert.Empty((await p.Get("/v1/me/sealed")).Body.EnumerateArray());
        Assert.Equal((4, 49), await p.Balances());
    }

    [Fact(DisplayName = "SC-FRG-04: đầu vào không hợp lệ bị từ chối, không hủy thẻ, không trừ phí")]
    public async Task InvalidInputs()
    {
        var (p, cards) = await Setup(3); await p.GetCoin(500);
        var other = await fx.NewPlayer(); await other.BuyAndOpen(1);
        var othersCard = (await other.Collection()).First(i => !i.GetProperty("soulbound").GetBoolean()).GetProperty("id").GetGuid();
        await p.OpenWelcomePackAsync();
        var soulbound = (await p.Collection()).First(i => i.GetProperty("soulbound").GetBoolean()).GetProperty("id").GetGuid();
        var before = await p.Balances();

        Assert.Equal("FORGE_REQUIRES_TWO_CARDS", (await Forge(p, [cards[0]])).Code);
        Assert.Equal("FORGE_DUPLICATE_INPUT", (await Forge(p, [cards[0], cards[0]])).Code);
        Assert.Equal("CARD_NOT_OWNED", (await Forge(p, [cards[0], othersCard])).Code);
        Assert.Equal("CARD_NOT_OWNED", (await Forge(p, [cards[0], Guid.NewGuid()])).Code);
        Assert.Equal("CARD_NOT_FORGEABLE", (await Forge(p, [cards[0], soulbound])).Code);
        await fx.Sql("UPDATE collection.card_instance SET state='Listed' WHERE id=@i", ("i", cards[1]));
        Assert.Equal("CARD_LOCKED", (await Forge(p, [cards[0], cards[1]])).Code);
        await fx.Sql("UPDATE collection.card_instance SET state='InWallet' WHERE id=@i", ("i", cards[1]));
        Assert.Equal("CARD_NOT_IN_ACCOUNT", (await Forge(p, [cards[0], cards[1]])).Code);
        await fx.Sql("UPDATE collection.card_instance SET state='Owned' WHERE id=@i", ("i", cards[1]));

        Assert.Equal(before, await p.Balances());
        Assert.Equal(0L, await fx.Scalar<long>("SELECT count(*) FROM forge.forge_record WHERE account_id=@a", ("a", p.Id)));
        Assert.Equal("Owned", await fx.Scalar<string>("SELECT state FROM collection.card_instance WHERE id=@i", ("i", cards[0])));
    }

    [Fact(DisplayName = "Idempotency: gửi lại lệnh rèn cùng key không rèn lần hai")]
    public async Task ForgeIsIdempotent()
    {
        var (p, cards) = await Setup(4); await p.GetCoin(500);
        var a = await Forge(p, cards.Take(2), "COIN", "forge-key");
        var b = await Forge(p, cards.Take(2), "COIN", "forge-key");
        Assert.Equal(a["sealedCardId"].GetGuid(), b["sealedCardId"].GetGuid());
        Assert.Single((await p.Get("/v1/me/sealed")).Body.EnumerateArray());
    }

    [Fact(DisplayName = "SC-FRG-09: cùng một thẻ trong hai lần rèn đồng thời → đúng một lần thành công, lần kia CARD_NOT_AVAILABLE")]
    public async Task ConcurrentForgeOfSharedCard()
    {
        var (p, cards) = await Setup(3); await p.GetCoin(500);
        var (gem, coin) = await p.Balances();
        var rs = await Task.WhenAll(Forge(p, [cards[0], cards[1]]), Forge(p, [cards[0], cards[2]]));
        Assert.Equal(1, rs.Count(r => r.Ok));
        var failed = rs.Single(r => !r.Ok);
        Assert.Equal(HttpStatusCode.Conflict, failed.Status);
        Assert.Equal("CARD_NOT_AVAILABLE", failed.Code);
        Assert.Equal((gem, coin - 50), await p.Balances());                           // chỉ trừ phí một lần
        Assert.Equal(2L, await fx.Scalar<long>("SELECT count(*) FROM collection.card_instance WHERE owner_id=@a AND state='Burned'", ("a", p.Id)));
    }

    [Fact(DisplayName = "BR-FRG-07 / SC-FRG-08: chưa xác thực SĐT rèn tối đa 5 lần/ngày")]
    public async Task DailyLimitForUnverified()
    {
        var (p, _) = await Setup(12); await p.GetCoin(1_000);
        var inputs = (await p.Collection()).Where(i => !i.GetProperty("soulbound").GetBoolean()).Select(i => i.GetProperty("id").GetGuid()).ToList();
        for (var i = 0; i < 5; i++) Assert.True((await Forge(p, inputs.Skip(i * 2).Take(2))).Ok, $"lần rèn {i + 1}");
        var sixth = await Forge(p, inputs.Skip(10).Take(2));
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.Status);
        Assert.Equal("FORGE_DAILY_LIMIT", sixth.Code);
        Assert.Equal("Owned", await fx.Scalar<string>("SELECT state FROM collection.card_instance WHERE id=@i", ("i", inputs[10])));
    }

    [Fact(DisplayName = "BR-FRG-06: lật thẻ chưa lật cấp một thẻ mới số #n/N; lật lần hai bị từ chối")]
    public async Task RevealIssuesCard()
    {
        var (p, cards) = await Setup(); await p.GetCoin(500);
        var sealedId = (await Forge(p, cards.Take(2)))["sealedCardId"].GetGuid();
        var before = (await p.Collection()).Count;
        var r = await p.Post($"/v1/forge/sealed/{sealedId}/reveal");
        Assert.True(r.Ok, r.Body.ToString());
        Assert.Equal(before + 1, (await p.Collection()).Count);
        Assert.False(r["card"].GetProperty("soulbound").GetBoolean());
        Assert.Matches(@"^#\d+/\d+$", r["card"].GetProperty("edition").GetString());
        var again = await p.Post($"/v1/forge/sealed/{sealedId}/reveal");
        Assert.Equal("SEALED_CARD_ALREADY_REVEALED", again.Code);
        Assert.Empty((await p.Get("/v1/me/sealed")).Body.EnumerateArray());
        var other = await fx.NewPlayer();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Post($"/v1/forge/sealed/{sealedId}/reveal")).Status);
    }

    [Fact(DisplayName = "SC-FRG-05: pity của pack không đổi khi lật thẻ rèn")]
    public async Task ForgeDoesNotTouchPity()
    {
        var (p, cards) = await Setup(); await p.GetCoin(500);
        await fx.Sql("INSERT INTO gacha.pity_counter(account_id,pack_code,count) VALUES(@a,'awakening-standard',49) ON CONFLICT (account_id,pack_code) DO UPDATE SET count=49", ("a", p.Id));
        var sealedId = (await Forge(p, cards.Take(2)))["sealedCardId"].GetGuid();
        Assert.True((await p.Post($"/v1/forge/sealed/{sealedId}/reveal")).Ok);
        Assert.Equal(49, (await p.Get("/v1/me/pity/awakening-standard"))["count"].GetInt32());
    }

    [Fact(DisplayName = "SC-FRG-06: lật thẻ rèn dùng commit–reveal (slot 0, đúng một nonce) và tính lại được")]
    public async Task RevealUsesCommitReveal()
    {
        var (p, cards) = await Setup(); await p.GetCoin(500);
        await p.Put("/v1/fairness/client-seed", new { clientSeed = "forge-check" });
        var nonceBefore = (await p.Get("/v1/fairness"))["nextNonce"].GetInt32();
        var sealedId = (await Forge(p, cards.Take(2)))["sealedCardId"].GetGuid();
        Assert.Equal(nonceBefore, (await p.Get("/v1/fairness"))["nextNonce"].GetInt32());          // rèn chưa dùng nonce

        var r = await p.Post($"/v1/forge/sealed/{sealedId}/reveal");
        var f = r["fairness"];
        Assert.Equal(nonceBefore, f.GetProperty("nonce").GetInt32());
        Assert.Equal(nonceBefore + 1, (await p.Get("/v1/fairness"))["nextNonce"].GetInt32());

        var prev = (await p.Post("/v1/fairness/rotate"))["previous"];
        var v = await new Player.Anon(fx).Post("/v1/fairness/verify", new { serverSeed = prev.GetProperty("serverSeed").GetString(), clientSeed = "forge-check", nonce = nonceBefore, slots = 1 });
        Assert.Equal(f.GetProperty("rarityRoll").GetInt32(), v["rolls"][0].GetInt32());
        Assert.Equal(f.GetProperty("cardRoll").GetInt32(), v["cardRolls"][0].GetInt32());
    }

    [Fact(DisplayName = "GET /v1/forge/info: phí, hạn mức, tỷ lệ rèn công khai cho người chơi")]
    public async Task ForgeInfo()
    {
        var p = await fx.NewPlayer();
        var r = await p.Get("/v1/forge/info");
        Assert.Equal(50, r["feeCoin"].GetInt64()); Assert.Equal(5, r["feeGem"].GetInt64());
        Assert.Equal(5, r["dailyLimit"].GetInt64()); Assert.Equal(0, r["usedToday"].GetInt64());
        Assert.Equal(6, r["odds"].GetProperty("entries").GetArrayLength());
    }
}
