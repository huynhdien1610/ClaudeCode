using System.Net;
using System.Text.Json;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Chợ P2P giá cố định: BR-MKT-01..06, SC-MKT-01..04, 07..15.</summary>
public sealed class MarketTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private async Task<Player> Trader(int packs = 3)
    {
        var p = await fx.NewPlayer(); await p.VerifyPhone();
        if (packs > 0) await p.BuyAndOpen(packs);
        return p;
    }

    /// <summary>Mặc định chọn thẻ Common để giá thử nằm trong khoảng cho phép của mọi test (Common 10–5.000 Coin).</summary>
    private static async Task<Guid> TradableCard(Player p, string? rarity = "common")
    {
        var items = (await p.Collection()).Where(i => !i.GetProperty("soulbound").GetBoolean() && i.GetProperty("state").GetString() == "Owned" && (rarity is null || i.GetProperty("card").GetProperty("rarity").GetString() == rarity));
        return items.First().GetProperty("id").GetGuid();
    }

    private static Task<Resp> List(Player p, Guid card, long price, string currency = "COIN", int days = 3) =>
        p.Post("/v1/market/listings", new { cardInstanceId = card, currency, price, days });

    private static Task<Resp> Buy(Player p, Guid listing, string? idem = null) => p.Post($"/v1/market/listings/{listing}/buy", null, idem);

    private async Task Coin(Player p, long target) { var now = (await p.Balances()).Coin; if (now < target) await p.GetCoin(target - now); }

    [Fact(DisplayName = "Niêm yết: thẻ chuyển Listed, hiện trên chợ cho người khác kèm thông tin thẻ, phí và số tiền người bán nhận")]
    public async Task ListAndBrowse()
    {
        var s = await Trader(); var b = await Trader(0);
        var card = await TradableCard(s);
        var l = await List(s, card, 1000);
        Assert.Equal(HttpStatusCode.Created, l.Status); Assert.Equal("Active", l["status"].GetString());
        Assert.Equal(50, l["fee"].GetInt64()); Assert.Equal(950, l["sellerReceives"].GetInt64());
        Assert.Equal("Listed", (await s.Collection()).First(i => i.GetProperty("id").GetGuid() == card).GetProperty("state").GetString());

        var browse = await b.Get("/v1/market/listings?currency=COIN&sort=newest");
        var item = browse["items"].EnumerateArray().First(x => x.GetProperty("id").GetGuid() == l["id"].GetGuid());
        Assert.False(item.GetProperty("mine").GetBoolean()); Assert.Equal(1000, item.GetProperty("price").GetInt64());
        Assert.Equal(card, item.GetProperty("cardInstanceId").GetGuid()); Assert.True(item.GetProperty("card").GetProperty("atk").ValueKind != JsonValueKind.Undefined);
        Assert.True((await s.Get("/v1/market/listings"))["items"].EnumerateArray().First(x => x.GetProperty("id").GetGuid() == l["id"].GetGuid()).GetProperty("mine").GetBoolean());
        var rules = await b.Get("/v1/market/rules"); Assert.Equal(5, rules["feePercent"].GetInt32());
    }

    [Fact(DisplayName = "Điều kiện niêm yết: chưa xác thực SĐT, bị hạn chế, thẻ gắn chặt, thẻ đang niêm yết, thẻ người khác, tiền/ngày sai")]
    public async Task ListingPreconditions()
    {
        var s = await Trader(5); var card = await TradableCard(s);
        var unverified = await fx.NewPlayer(); await unverified.BuyAndOpen(1);
        Assert.Equal("PHONE_VERIFICATION_REQUIRED", (await List(unverified, await TradableCard(unverified, null), 100)).Code);

        var restricted = await Trader(); var rc = await TradableCard(restricted, null);
        await fx.Sql("UPDATE identity.account SET status='Restricted', restriction_reason='FRAUD' WHERE id=@i", ("i", restricted.Id));
        Assert.Equal("ACCOUNT_RESTRICTED", (await List(restricted, rc, 100)).Code);

        var fresh = await fx.NewPlayer(); await fresh.VerifyPhone(); await fresh.OpenWelcomePackAsync();
        var bound = (await fresh.Collection()).First().GetProperty("id").GetGuid();
        Assert.Equal("CARD_NOT_TRADABLE", (await List(fresh, bound, 100)).Code);

        Assert.True((await List(s, card, 100)).Ok);
        Assert.Equal("CARD_LOCKED", (await List(s, card, 100)).Code);                                            // SC-MKT-08
        var other = await Trader();
        Assert.Equal("CARD_NOT_OWNED", (await List(other, card, 100)).Code);
        Assert.Equal("VALIDATION_FAILED", (await List(s, await TradableCard(s), 100, "XXX")).Code);
        Assert.Equal("VALIDATION_FAILED", (await List(s, await TradableCard(s), 100, days: 2)).Code);
    }

    [Fact(DisplayName = "BR-MKT-04: giá ngoài khoảng sàn/trần theo rarity bị từ chối (Common 10–5.000 Coin, Gem ÷ 10)")]
    public async Task PriceRange()
    {
        var s = await Trader(5); var cards = (await s.Collection()).Where(i => i.GetProperty("card").GetProperty("rarity").GetString() == "common").Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.True(cards.Count >= 4);
        Assert.Equal("PRICE_OUT_OF_RANGE", (await List(s, cards[0], 9)).Code);
        Assert.Equal("PRICE_OUT_OF_RANGE", (await List(s, cards[0], 5001)).Code);
        Assert.True((await List(s, cards[0], 10)).Ok); Assert.True((await List(s, cards[1], 5000)).Ok);
        Assert.Equal("PRICE_OUT_OF_RANGE", (await List(s, cards[2], 501, "GEM")).Code);
        Assert.True((await List(s, cards[2], 500, "GEM")).Ok);
        Assert.Equal("PRICE_OUT_OF_RANGE", (await List(s, cards[3], 0)).Code);
    }

    [Fact(DisplayName = "SC-MKT-12/01: mua thành công — trừ giá, người bán nhận giá − phí 5%, thẻ đổi chủ, listing Sold, ledger khớp")]
    public async Task BuySucceeds()
    {
        var s = await Trader(); var b = await Trader(0); await Coin(b, 5000);
        var card = await TradableCard(s); var l = await List(s, card, 1000);
        var sBefore = (await s.Balances()).Coin; var bBefore = (await b.Balances()).Coin;
        var r = await Buy(b, l["id"].GetGuid());
        Assert.True(r.Ok, r.Body.ToString()); Assert.Equal(50, r["fee"].GetInt64());
        Assert.Equal(bBefore - 1000, (await b.Balances()).Coin); Assert.Equal(sBefore + 950, (await s.Balances()).Coin);
        Assert.Contains(card, (await b.Collection()).Select(i => i.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(card, (await s.Collection()).Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal("Owned", (await b.Collection()).First(i => i.GetProperty("id").GetGuid() == card).GetProperty("state").GetString());
        Assert.Equal("Sold", await fx.Scalar<string>("SELECT status FROM market.listing WHERE id=@i", ("i", l["id"].GetGuid())));
        Assert.Equal(0, await fx.Scalar<long>("SELECT count(*) FROM wallet.balance_mismatch"));
        Assert.Equal("CARD_NOT_OWNED", (await List(s, card, 100)).Code);                                         // người bán không còn thẻ
        var reasons = (await b.Get("/v1/wallet/ledger")).Body.EnumerateArray().Select(e => e.GetProperty("reason").GetString()).ToList();
        Assert.Contains("MARKET_BUY", reasons);
        Assert.Contains("MARKET_SALE", (await s.Get("/v1/wallet/ledger")).Body.EnumerateArray().Select(e => e.GetProperty("reason").GetString()));
    }

    [Fact(DisplayName = "SC-MKT-11/04/13: thiếu đúng loại tiền thì INSUFFICIENT_BALANCE và không trừ gì; chưa xác thực SĐT; tự mua listing của mình")]
    public async Task BuyRejections()
    {
        var s = await Trader(); var card = await TradableCard(s); var l = (await List(s, card, 30, "GEM"))["id"].GetGuid();
        var b = await Trader(0); await Coin(b, 100_000);
        var coin = (await b.Balances()).Coin;
        var r = await Buy(b, l);
        Assert.Equal("INSUFFICIENT_BALANCE", r.Code); Assert.Equal(coin, (await b.Balances()).Coin);
        Assert.Equal("SELF_PURCHASE_FORBIDDEN", (await Buy(s, l)).Code);
        var unverified = await fx.NewPlayer();
        Assert.Equal("PHONE_VERIFICATION_REQUIRED", (await Buy(unverified, l)).Code);
        Assert.Equal("Active", await fx.Scalar<string>("SELECT status FROM market.listing WHERE id=@i", ("i", l)));
        Assert.Equal(HttpStatusCode.NotFound, (await Buy(b, Guid.NewGuid())).Status);
    }

    [Fact(DisplayName = "SC-MKT-02: hai người mua cùng lúc — đúng một người thành công, người kia LISTING_NOT_AVAILABLE và giữ nguyên số dư")]
    public async Task ConcurrentBuy()
    {
        var s = await Trader(); var card = await TradableCard(s); var l = (await List(s, card, 3000))["id"].GetGuid();
        var b1 = await Trader(0); var b2 = await Trader(0); await Coin(b1, 5000); await Coin(b2, 5000);
        var before = ((await b1.Balances()).Coin, (await b2.Balances()).Coin);
        var results = await Task.WhenAll(Buy(b1, l), Buy(b2, l));
        Assert.Equal(1, results.Count(r => r.Ok));
        var loser = results.Single(r => !r.Ok); Assert.Equal("LISTING_NOT_AVAILABLE", loser.Code);
        var after = ((await b1.Balances()).Coin, (await b2.Balances()).Coin);
        Assert.Equal(1, new[] { after.Item1 == before.Item1 - 3000, after.Item2 == before.Item2 - 3000 }.Count(x => x));
        Assert.Equal(1, new[] { after.Item1 == before.Item1, after.Item2 == before.Item2 }.Count(x => x));
        Assert.Equal(1, await fx.Scalar<long>("SELECT count(*) FROM collection.card_instance WHERE id=@i AND owner_id IN (@a,@b)", ("i", card), ("a", b1.Id), ("b", b2.Id)));
    }

    [Fact(DisplayName = "SC-MKT-14: gửi trùng yêu cầu mua với cùng Idempotency-Key chỉ trừ tiền một lần và trả cùng kết quả")]
    public async Task IdempotentBuy()
    {
        var s = await Trader(); var l = (await List(s, await TradableCard(s), 1000))["id"].GetGuid();
        var b = await Trader(0); await Coin(b, 5000); var before = (await b.Balances()).Coin; var key = "M-" + Guid.NewGuid().ToString("N");
        var r1 = await Buy(b, l, key); var r2 = await Buy(b, l, key);
        Assert.True(r1.Ok && r2.Ok); Assert.Equal(before - 1000, (await b.Balances()).Coin);
        Assert.Equal(r1["listingId"].GetGuid(), r2["listingId"].GetGuid());
    }

    [Fact(DisplayName = "SC-MKT-15: hủy niêm yết trả thẻ về chủ và niêm yết lại được; đã Sold thì không hủy được; người khác không hủy được")]
    public async Task CancelRules()
    {
        var s = await Trader(); var card = await TradableCard(s); var l = (await List(s, card, 500))["id"].GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await (await Trader(0)).Post($"/v1/market/listings/{l}/cancel")).Status);
        var c = await s.Post($"/v1/market/listings/{l}/cancel"); Assert.True(c.Ok); Assert.Equal("Cancelled", c["status"].GetString());
        Assert.Equal("Owned", (await s.Collection()).First(i => i.GetProperty("id").GetGuid() == card).GetProperty("state").GetString());
        Assert.Equal("LISTING_NOT_AVAILABLE", (await s.Post($"/v1/market/listings/{l}/cancel")).Code);
        var again = await List(s, card, 600); Assert.True(again.Ok);                                              // niêm yết lại được

        var b = await Trader(0); await Coin(b, 2000);
        Assert.True((await Buy(b, again["id"].GetGuid())).Ok);
        Assert.Equal("LISTING_NOT_AVAILABLE", (await s.Post($"/v1/market/listings/{again["id"].GetGuid()}/cancel")).Code);   // Sold giữ nguyên
        Assert.Equal("Sold", await fx.Scalar<string>("SELECT status FROM market.listing WHERE id=@i", ("i", again["id"].GetGuid())));
    }

    [Fact(DisplayName = "SC-MKT-15: listing hết hạn thành Expired, thẻ về Owned, không mua được nữa")]
    public async Task Expiry()
    {
        var s = await Trader(); var card = await TradableCard(s); var l = (await List(s, card, 500, days: 1))["id"].GetGuid();
        await fx.Sql("UPDATE market.listing SET expires_at = now() + interval '1 hour' WHERE id=@i", ("i", l));
        Assert.Equal("Active", (await s.Get("/v1/market/mine"))["items"].EnumerateArray().First(x => x.GetProperty("id").GetGuid() == l).GetProperty("status").GetString());
        await fx.Sql("UPDATE market.listing SET expires_at = now() - interval '1 second' WHERE id=@i", ("i", l));
        var b = await Trader(0); await Coin(b, 1000);
        Assert.Equal("LISTING_NOT_AVAILABLE", (await Buy(b, l)).Code);                                           // quá hạn thì không mua được dù chưa kịp quét
        Assert.NotEqual("Sold", await fx.Scalar<string>("SELECT status FROM market.listing WHERE id=@i", ("i", l)));
        await s.Get("/v1/market/mine");                                                                         // mọi lần duyệt chợ đều quét listing quá hạn
        Assert.Equal("Expired", await fx.Scalar<string>("SELECT status FROM market.listing WHERE id=@i", ("i", l)));
        Assert.Equal("Owned", (await s.Collection()).First(i => i.GetProperty("id").GetGuid() == card).GetProperty("state").GetString());
    }

    [Fact(DisplayName = "Lọc và sắp xếp: theo độ hiếm, tiền tệ, hệ, tên, giá tăng/giảm; phân trang")]
    public async Task BrowseFilters()
    {
        var s = await Trader(5); var cards = (await s.Collection()).Where(i => !i.GetProperty("soulbound").GetBoolean() && i.GetProperty("card").GetProperty("rarity").GetString() == "common").Take(3).ToList();
        Assert.Equal(3, cards.Count);
        await List(s, cards[0].GetProperty("id").GetGuid(), 300); await List(s, cards[1].GetProperty("id").GetGuid(), 100); await List(s, cards[2].GetProperty("id").GetGuid(), 50, "GEM");
        var b = await fx.NewPlayer();
        var asc = (await b.Get($"/v1/market/listings?rarity=common&currency=COIN&sort=price_asc&limit=60"))["items"].EnumerateArray().Select(x => x.GetProperty("price").GetInt64()).ToList();
        Assert.Equal(asc.OrderBy(x => x), asc);
        var desc = (await b.Get("/v1/market/listings?currency=COIN&sort=price_desc&limit=60"))["items"].EnumerateArray().Select(x => x.GetProperty("price").GetInt64()).ToList();
        Assert.Equal(desc.OrderByDescending(x => x), desc);
        var gem = (await b.Get("/v1/market/listings?currency=GEM&limit=60"))["items"].EnumerateArray().ToList();
        Assert.All(gem, x => Assert.Equal("GEM", x.GetProperty("currency").GetString()));
        var el = cards[0].GetProperty("card").GetProperty("element").GetString();
        Assert.All((await b.Get($"/v1/market/listings?element={el}&limit=60"))["items"].EnumerateArray(), x => Assert.Equal(el, x.GetProperty("card").GetProperty("element").GetString()));
        Assert.Equal(1, (await b.Get("/v1/market/listings?limit=1"))["items"].GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await new Player.Anon(fx).Get("/v1/market/listings")).Status);
    }

    [Fact(DisplayName = "Thẻ đang niêm yết bị khóa: không rèn được (CARD_LOCKED) và rời khỏi bộ bài")]
    public async Task ListedCardIsLocked()
    {
        var s = await Trader(5); var items = (await s.Collection()).Where(i => !i.GetProperty("soulbound").GetBoolean() && i.GetProperty("card").GetProperty("rarity").GetString() == "common").Take(2).Select(i => i.GetProperty("id").GetGuid()).ToList();
        var deck = (await s.Post("/v1/decks", new { name = "d", cardInstanceIds = items }))["id"].GetGuid();
        Assert.True((await List(s, items[0], 100)).Ok);
        Assert.Equal(1, (await s.Get($"/v1/decks/{deck}"))["count"].GetInt32());
        await s.TopUp(100); await s.GetCoin(100);
        var forge = await s.Post("/v1/forge", new { cardInstanceIds = items, feeCurrency = "COIN" });
        Assert.Equal("CARD_LOCKED", forge.Code);
    }
}
