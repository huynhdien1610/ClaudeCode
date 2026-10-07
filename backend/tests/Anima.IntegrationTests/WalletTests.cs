using System.Net;

namespace Anima.IntegrationTests;

public sealed class WalletTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private static Task<Resp> Convert(Player p, string direction, long amount, string? idem = null) => p.Post("/v1/wallet/convert", new { direction, amount }, idem);

    [Fact(DisplayName = "SC-WAL-16: đổi 100 Gem → 900 Coin")]
    public async Task Convert100Gem()
    {
        var p = await fx.NewPlayer(); await p.TopUp(100);
        var r = await Convert(p, "GEM_TO_COIN", 100);
        Assert.True(r.Ok, r.Body.ToString());
        Assert.Equal((0, 100 + 900), await p.Balances());      // 100 Coin thưởng đăng nhập + 900
    }

    [Fact(DisplayName = "SC-WAL-17: đổi 1 Gem nhận 9 Coin")]
    public async Task Convert1Gem()
    {
        var p = await fx.NewPlayer(); await p.TopUp(1);
        Assert.Equal(9, (await Convert(p, "GEM_TO_COIN", 1))["received"].GetInt64());
    }

    [Theory(DisplayName = "SC-WAL-18 / SC-WAL-21: số lượng không hợp lệ")]
    [InlineData("GEM_TO_COIN", 0)]
    [InlineData("GEM_TO_COIN", -5)]
    [InlineData("COIN_TO_GEM", 10)]     // chưa đủ 11 Coin cho 1 Gem
    public async Task InvalidAmount(string direction, long amount)
    {
        var p = await fx.NewPlayer(); await p.VerifyPhone(); await p.TopUp(10);
        var before = await p.Balances();
        var r = await Convert(p, direction, amount);
        Assert.Equal("INVALID_AMOUNT", r.Code);
        Assert.Equal(before, await p.Balances());
    }

    [Fact(DisplayName = "SC-WAL-19: đổi 9,900 Coin → 900 Gem")]
    public async Task ConvertCoinToGem()
    {
        var p = await fx.NewPlayer(); await p.VerifyPhone(); await p.GetCoin(9_900);
        var (gem0, coin0) = await p.Balances();
        var r = await Convert(p, "COIN_TO_GEM", 9_900);
        Assert.True(r.Ok, r.Body.ToString());
        Assert.Equal((gem0 + 900, coin0 - 9_900), await p.Balances());
    }

    [Fact(DisplayName = "SC-WAL-20: làm tròn xuống, số lẻ không bị trừ (120 Coin → 10 Gem, còn 10 Coin)")]
    public async Task RoundingDown()
    {
        var p = await fx.NewPlayer(); await p.VerifyPhone(); await p.TopUp(2);
        await Convert(p, "GEM_TO_COIN", 2);                 // có 100 + 18 = 118 Coin
        var (g, c) = await p.Balances();
        var r = await Convert(p, "COIN_TO_GEM", 118);       // 118 / 11 = 10 Gem, trừ 110
        Assert.Equal(10, r["received"].GetInt64());
        Assert.Equal(110, r["spent"].GetInt64());
        Assert.Equal((g + 10, c - 110), await p.Balances());
    }

    [Fact(DisplayName = "SC-WAL-23: chưa xác thực SĐT thì không đổi Coin → Gem")]
    public async Task UnverifiedCannotConvertCoinToGem()
    {
        var p = await fx.NewPlayer(); await p.GetCoin(1_100);
        var r = await Convert(p, "COIN_TO_GEM", 1_100);
        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.Equal("PHONE_VERIFICATION_REQUIRED", r.Code);
    }

    [Theory(DisplayName = "SC-WAL-22: hạn mức đổi Coin → Gem 1,000 Gem/ngày")]
    [InlineData(990, true)]
    [InlineData(991, false)]
    public async Task DailyCap(int alreadyConverted, bool succeeds)
    {
        var p = await fx.NewPlayer(); await p.VerifyPhone(); await p.GetCoin(20_000);
        Assert.True((await Convert(p, "COIN_TO_GEM", alreadyConverted * 11L)).Ok);
        var r = await Convert(p, "COIN_TO_GEM", 110);
        if (succeeds) { Assert.True(r.Ok, r.Body.ToString()); Assert.Equal(10, r["received"].GetInt64()); }
        else { Assert.Equal(HttpStatusCode.TooManyRequests, r.Status); Assert.Equal("COIN_TO_GEM_DAILY_LIMIT", r.Code); }
    }

    [Fact(DisplayName = "Idempotency: gửi lại cùng key không đổi tiền hai lần")]
    public async Task ConvertIsIdempotent()
    {
        var p = await fx.NewPlayer(); await p.TopUp(10);
        var first = await Convert(p, "GEM_TO_COIN", 5, "same-key");
        var again = await Convert(p, "GEM_TO_COIN", 5, "same-key");
        Assert.Equal(first.Body.ToString(), again.Body.ToString());
        Assert.Equal((5, 100 + 45), await p.Balances());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await Convert(p, "GEM_TO_COIN", 6, "same-key")).Code);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", (await Convert(p, "GEM_TO_COIN", 1, "")).Code);
    }

    [Fact(DisplayName = "BR-WAL-01 / NFR-11: ledger chỉ thêm, không sửa hay xóa; số dư luôn bằng tổng bút toán")]
    public async Task LedgerIsAppendOnlyAndConsistent()
    {
        var p = await fx.NewPlayer(); await p.TopUp(50); await Convert(p, "GEM_TO_COIN", 20);
        var ledger = await p.Get("/v1/wallet/ledger");
        Assert.True(ledger.Body.GetArrayLength() >= 4);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fx.Sql("UPDATE wallet.ledger_entry SET amount = 1 WHERE account_id=@a", ("a", p.Id)));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fx.Sql("DELETE FROM wallet.ledger_entry WHERE account_id=@a", ("a", p.Id)));
        Assert.Equal(0L, await fx.Scalar<long>("SELECT count(*) FROM wallet.balance_mismatch"));
    }

    [Fact(DisplayName = "BR-WAL-03: Coin không bao giờ âm (ràng buộc ở database)")]
    public async Task CoinNeverNegative()
    {
        var p = await fx.NewPlayer();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fx.Sql("UPDATE wallet.balance SET amount = -1 WHERE account_id=@a AND currency='COIN'", ("a", p.Id)));
    }

    [Fact(DisplayName = "Đồng thời: 10 lần trừ song song không bao giờ làm số dư âm")]
    public async Task ConcurrentSpendNeverOverdraws()
    {
        var p = await fx.NewPlayer(); await p.TopUp(250);           // đủ cho đúng 2 pack (100 Gem/pack), không đủ cho 3
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 })));
        Assert.Equal(2, results.Count(r => r.Ok));
        Assert.All(results.Where(r => !r.Ok), r => Assert.Equal("INSUFFICIENT_BALANCE", r.Code));
        Assert.Equal(50, (await p.Balances()).Gem);
    }

    [Fact(DisplayName = "GET /v1/economy công khai tham số tỷ lệ và phí")]
    public async Task EconomyParamsArePublic()
    {
        var r = await new Player.Anon(fx).Get("/v1/economy");
        Assert.Equal(9, r["gem_to_coin_rate"].GetInt64());
        Assert.Equal(11, r["coin_to_gem_rate"].GetInt64());
        Assert.Equal(50, r["forge_fee_coin"].GetInt64());
    }
}
