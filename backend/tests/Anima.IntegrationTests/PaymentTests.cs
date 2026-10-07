using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Anima.Payment;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Nạp Gem qua cổng thanh toán web: BR-WEB-04, BR-WAL-02/04, SC-WAL-01/02/06/10/11/12/13/14/15 (cổng giả lập, webhook có chữ ký thật).</summary>
public sealed class PaymentTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private sealed record Order(Guid Id, string Url);

    private static async Task<Order> CreateOrder(Player p, string package)
    {
        var r = await p.Post("/v1/payments/orders", new { packageCode = package });
        Assert.True(r.Ok, r.Body.ToString());
        return new Order(r["order"].GetProperty("id").GetGuid(), r["checkoutUrl"].GetString()!);
    }

    private async Task<(HttpStatusCode Status, string Outcome)> Webhook(string type, Guid orderId, string txn, long amount, string currency = "VND", string? signWith = null, bool unsigned = false)
    {
        var body = JsonSerializer.Serialize(new { type, orderId, gatewayTxnId = txn, amount, currency });
        using var http = fx.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/payments/webhook/sandbox") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (!unsigned) req.Headers.Add(SandboxGateway.SignatureHeader, SandboxGateway.Sign(signWith ?? ApiFixture.TestWebhookSecret, body));
        using var res = await http.SendAsync(req);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        return (res.StatusCode, json.GetProperty("outcome").GetString()!);
    }

    private static async Task<long> Price(Player p, string package) =>
        (await p.Get("/v1/payments/packages")).Body.EnumerateArray().First(x => x.GetProperty("code").GetString() == package).GetProperty("priceMinor").GetInt64();

    private static async Task<string> Status(Player p) => (await p.Get("/v1/me"))["status"].GetString()!;
    private async Task<string?> RestrictionReason(Player p) => await fx.Scalar<string>("SELECT restriction_reason FROM identity.account WHERE id=@i", ("i", p.Id));

    [Fact(DisplayName = "Bảng giá gói Gem công khai (không cần đăng nhập)")]
    public async Task PublicPriceList()
    {
        var r = await new Player.Anon(fx).Get("/v1/payments/packages");
        Assert.True(r.Ok); Assert.True(r.Body.GetArrayLength() >= 3);
        Assert.All(r.Body.EnumerateArray(), x => { Assert.True(x.GetProperty("gem").GetInt64() > 0); Assert.True(x.GetProperty("priceMinor").GetInt64() > 0); });
    }

    [Fact(DisplayName = "SC-WAL-06 / BR-WEB-04: chỉ webhook có chữ ký hợp lệ mới cộng Gem; tạo đơn và trang quay về thì không")]
    public async Task OnlySignedWebhookCredits()
    {
        var p = await fx.NewPlayer();
        var o = await CreateOrder(p, "gem_550");
        Assert.Contains($"/pay/sandbox?order={o.Id}", o.Url);
        Assert.Equal(0, (await p.Balances()).Gem);
        Assert.Equal("Created", (await p.Get($"/v1/payments/orders/{o.Id}"))["status"].GetString());       // quay về trình duyệt không cộng

        var price = await Price(p, "gem_550");
        var w = await Webhook("payment.succeeded", o.Id, "GW-1001", price);
        Assert.Equal((HttpStatusCode.OK, "CREDITED"), w);
        Assert.Equal(550, (await p.Balances()).Gem);
        var ledger = (await p.Get("/v1/wallet/ledger")).Body.EnumerateArray().First(e => e.GetProperty("reason").GetString() == "GEM_TOPUP");
        Assert.Equal(550, ledger.GetProperty("amount").GetInt64()); Assert.Equal("GW-1001", ledger.GetProperty("refId").GetString());
        Assert.Equal("Paid", (await p.Get($"/v1/payments/orders/{o.Id}"))["status"].GetString());
    }

    [Fact(DisplayName = "SC-WAL-01: webhook gửi trùng không cộng lại; mã giao dịch của cổng không dùng lại cho đơn khác")]
    public async Task DuplicateWebhook()
    {
        var p = await fx.NewPlayer();
        var a = await CreateOrder(p, "gem_100"); var b = await CreateOrder(p, "gem_100");
        var price = await Price(p, "gem_100");
        Assert.Equal("CREDITED", (await Webhook("payment.succeeded", a.Id, "GW-2001", price)).Outcome);
        Assert.Equal((HttpStatusCode.OK, "DUPLICATE"), await Webhook("payment.succeeded", a.Id, "GW-2001", price));
        Assert.Equal(100, (await p.Balances()).Gem);
        Assert.Equal((HttpStatusCode.Conflict, "ANOMALY_TXN_REUSED"), await Webhook("payment.succeeded", b.Id, "GW-2001", price));
        Assert.Equal((HttpStatusCode.Conflict, "ANOMALY_SECOND_PAYMENT"), await Webhook("payment.succeeded", a.Id, "GW-2002", price));
        Assert.Equal(100, (await p.Balances()).Gem);
        Assert.Equal(1, await fx.Scalar<long>("SELECT count(*) FROM wallet.ledger_entry WHERE account_id=@a AND reason='GEM_TOPUP'", ("a", p.Id)));
    }

    [Fact(DisplayName = "SC-WAL-07: chữ ký sai/thiếu bị từ chối và ghi nhật ký; số tiền lệch bị từ chối; không cộng Gem")]
    public async Task InvalidWebhooks()
    {
        var p = await fx.NewPlayer(); var o = await CreateOrder(p, "gem_100"); var price = await Price(p, "gem_100");
        Assert.Equal((HttpStatusCode.Unauthorized, "INVALID_SIGNATURE"), await Webhook("payment.succeeded", o.Id, "FAKE-1", price, signWith: "wrong-secret"));
        Assert.Equal((HttpStatusCode.Unauthorized, "INVALID_SIGNATURE"), await Webhook("payment.succeeded", o.Id, "FAKE-2", price, unsigned: true));
        Assert.Equal((HttpStatusCode.BadRequest, "PAYMENT_AMOUNT_MISMATCH"), await Webhook("payment.succeeded", o.Id, "GW-3001", price - 1));
        Assert.Equal((HttpStatusCode.BadRequest, "PAYMENT_AMOUNT_MISMATCH"), await Webhook("payment.succeeded", o.Id, "GW-3002", price, currency: "USD"));
        Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), await Webhook("payment.succeeded", Guid.NewGuid(), "GW-3003", price));
        Assert.Equal(0, (await p.Balances()).Gem);
        Assert.Equal("Created", (await p.Get($"/v1/payments/orders/{o.Id}"))["status"].GetString());
        Assert.True(await fx.Scalar<long>("SELECT count(*) FROM payment.webhook_log WHERE signature_valid=false AND outcome='INVALID_SIGNATURE'") >= 2);
    }

    [Fact(DisplayName = "SC-WAL-11/12/15: hoàn tiền khi Gem còn đủ thì thu hồi đúng số Gem, trạng thái giữ nguyên; thông báo trùng không đổi gì")]
    public async Task RefundWithEnoughGem()
    {
        var p = await fx.NewPlayer(); await p.TopUp(250);
        var o = await CreateOrder(p, "gem_550"); var price = await Price(p, "gem_550");
        await Webhook("payment.succeeded", o.Id, "GW-4001", price);
        Assert.Equal(800, (await p.Balances()).Gem);
        Assert.Equal((HttpStatusCode.OK, "REFUNDED"), await Webhook("payment.refunded", o.Id, "GW-4001", price));
        Assert.Equal(250, (await p.Balances()).Gem);
        Assert.Equal("Unverified", await Status(p));
        Assert.Equal((HttpStatusCode.OK, "DUPLICATE"), await Webhook("payment.refunded", o.Id, "GW-4001", price));
        Assert.Equal(250, (await p.Balances()).Gem);
        Assert.Equal("Refunded", (await p.Get($"/v1/payments/orders/{o.Id}"))["status"].GetString());

        var exact = await fx.NewPlayer(); var o2 = await CreateOrder(exact, "gem_550");
        await Webhook("payment.succeeded", o2.Id, "GW-4002", price);
        await Webhook("payment.refunded", o2.Id, "GW-4002", price);
        Assert.Equal(0, (await exact.Balances()).Gem);                       // vừa đúng → 0, không bị hạn chế
        Assert.Equal("Unverified", await Status(exact));
    }

    [Fact(DisplayName = "SC-WAL-02/10/13/14: hoàn tiền khi Gem đã tiêu → số dư âm, Restricted NEGATIVE_GEM, không mua pack; nạp bù chưa đủ vẫn bị hạn chế, đủ thì tự gỡ")]
    public async Task RefundAfterSpending()
    {
        var p = await fx.NewPlayer(); await p.VerifyPhone();
        var o = await CreateOrder(p, "gem_550"); var price550 = await Price(p, "gem_550");
        await Webhook("payment.succeeded", o.Id, "GW-5001", price550);
        await p.Buy(4);                                                      // tiêu 400 Gem → còn 150
        Assert.Equal(150, (await p.Balances()).Gem);
        Assert.Equal((HttpStatusCode.OK, "REFUNDED_NEGATIVE"), await Webhook("payment.refunded", o.Id, "GW-5001", price550));
        Assert.Equal(-400, (await p.Balances()).Gem);
        Assert.Equal("Restricted", await Status(p)); Assert.Equal("NEGATIVE_GEM", await RestrictionReason(p));

        var buy = await p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 });
        Assert.Equal("ACCOUNT_RESTRICTED", buy.Code);                        // SC-WAL-10

        var small = await CreateOrder(p, "gem_100");                         // NEGATIVE_GEM được phép nạp bù
        Assert.Equal((HttpStatusCode.OK, "CREDITED"), await Webhook("payment.succeeded", small.Id, "GW-5002", await Price(p, "gem_100")));
        Assert.Equal(-300, (await p.Balances()).Gem); Assert.Equal("Restricted", await Status(p));      // SC-WAL-14

        var big = await CreateOrder(p, "gem_550");
        await Webhook("payment.succeeded", big.Id, "GW-5003", price550);
        Assert.Equal(250, (await p.Balances()).Gem);
        Assert.Equal("Verified", await Status(p)); Assert.Null(await RestrictionReason(p));             // SC-WAL-13: về trạng thái trước đó
        Assert.Equal(0, await fx.Scalar<long>("SELECT count(*) FROM wallet.balance_mismatch"));
    }

    [Fact(DisplayName = "Tài khoản Restricted vì FRAUD hoặc Banned không tạo được đơn nạp (BRD 8.1)")]
    public async Task FraudRestrictedCannotTopUp()
    {
        var p = await fx.NewPlayer();
        await fx.Sql("UPDATE identity.account SET status='Restricted', restriction_reason='FRAUD' WHERE id=@i", ("i", p.Id));
        Assert.Equal("ACCOUNT_RESTRICTED", (await p.Post("/v1/payments/orders", new { packageCode = "gem_100" })).Code);
        Assert.Equal("NOT_FOUND", (await (await fx.NewPlayer()).Post("/v1/payments/orders", new { packageCode = "nope" })).Code);
    }

    [Fact(DisplayName = "Tạo đơn idempotent theo Idempotency-Key; thanh toán lỗi rồi thành công muộn vẫn được cộng; người khác không xem được đơn của mình")]
    public async Task OrderLifecycle()
    {
        var p = await fx.NewPlayer(); var key = Guid.NewGuid().ToString("N");
        var a = await p.Post("/v1/payments/orders", new { packageCode = "gem_100" }, key);
        var b = await p.Post("/v1/payments/orders", new { packageCode = "gem_100" }, key);
        Assert.Equal(a["order"].GetProperty("id").GetGuid(), b["order"].GetProperty("id").GetGuid());
        var id = a["order"].GetProperty("id").GetGuid(); var price = await Price(p, "gem_100");

        Assert.Equal("FAILED", (await Webhook("payment.failed", id, "GW-6001", price)).Outcome);
        Assert.Equal("Failed", (await p.Get($"/v1/payments/orders/{id}"))["status"].GetString());
        Assert.Equal("CREDITED", (await Webhook("payment.succeeded", id, "GW-6001", price)).Outcome);
        Assert.Equal(100, (await p.Balances()).Gem);

        var other = await fx.NewPlayer();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Get($"/v1/payments/orders/{id}")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await new Player.Anon(fx).Get("/v1/payments/orders")).Status);
    }

    [Fact(DisplayName = "Sandbox: mô phỏng IPN cho đơn của chính mình đi qua đường webhook thật; không mô phỏng được đơn người khác")]
    public async Task SandboxSimulate()
    {
        var p = await fx.NewPlayer(); var o = await CreateOrder(p, "gem_100");
        var other = await fx.NewPlayer();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Post($"/v1/payments/sandbox/{o.Id}/simulate", new { outcome = "succeeded" })).Status);
        Assert.Equal("VALIDATION_FAILED", (await p.Post($"/v1/payments/sandbox/{o.Id}/simulate", new { outcome = "boom" })).Code);
        var r = await p.Post($"/v1/payments/sandbox/{o.Id}/simulate", new { outcome = "succeeded" });
        Assert.True(r.Ok, r.Body.ToString()); Assert.Equal("CREDITED", r["outcome"].GetString());
        Assert.Equal(100, (await p.Balances()).Gem);
        Assert.Equal("REFUNDED", (await p.Post($"/v1/payments/sandbox/{o.Id}/simulate", new { outcome = "refunded" }))["outcome"].GetString());
        Assert.Equal(0, (await p.Balances()).Gem);
    }
}
