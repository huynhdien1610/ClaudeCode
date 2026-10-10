using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anima.Contracts;
using Anima.Identity.Contracts;
using Anima.SharedKernel;
using Anima.Wallet;
using Microsoft.Extensions.Options;

namespace Anima.Payment;

public sealed class PaymentOptions
{
    /// <summary>Chỉ "sandbox" được cài sẵn. Cổng thật (VNPay, MoMo, Stripe...) cài <see cref="IPaymentGateway"/> rồi đổi giá trị này.</summary>
    public string Provider { get; set; } = "sandbox";
    /// <summary>Khóa HMAC-SHA256 ký webhook. Bắt buộc đặt riêng ở môi trường thật.</summary>
    public string WebhookSecret { get; set; } = "";
    /// <summary>Bật cổng giả lập: trang thanh toán thử và endpoint mô phỏng IPN. Không bao giờ bật ở production.</summary>
    public bool Sandbox { get; set; }
    /// <summary>Địa chỉ website người chơi, dùng dựng URL thanh toán và URL quay về.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:3000";
}

public sealed record GemPackage(string Code, long Gem, long PriceMinor, string Currency);
public sealed record OrderDto(Guid Id, string PackageCode, long Gem, long AmountMinor, string Currency, string Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, DateTimeOffset? RefundedAt);
public sealed record CreateOrderRequest(string? PackageCode);
public sealed record CheckoutResult(OrderDto Order, string CheckoutUrl);
public sealed record SimulateRequest(string? Outcome);

/// <summary>Sự kiện đã xác thực từ cổng thanh toán, đã chuẩn hóa.</summary>
public sealed record GatewayEvent(string Type, Guid OrderId, string GatewayTxnId, long AmountMinor, string Currency);

public interface IPaymentGateway
{
    string Name { get; }
    /// <summary>Tạo phiên thanh toán ở cổng và trả về URL để chuyển người chơi sang.</summary>
    Task<string> CreateCheckoutAsync(OrderDto order, CancellationToken ct);
    /// <summary>Xác thực chữ ký rồi phân tích webhook. Trả về null nếu chữ ký không hợp lệ.</summary>
    GatewayEvent? VerifyWebhook(string rawBody, IReadOnlyDictionary<string, string> headers);
}

/// <summary>Cổng giả lập dùng cho dev/test: cùng đường xử lý (chữ ký HMAC, idempotent) như cổng thật.</summary>
public sealed class SandboxGateway(IOptions<PaymentOptions> opt) : IPaymentGateway
{
    public const string SignatureHeader = "X-Signature";
    public string Name => "sandbox";

    public Task<string> CreateCheckoutAsync(OrderDto order, CancellationToken ct) =>
        Task.FromResult($"{opt.Value.PublicBaseUrl.TrimEnd('/')}/pay/sandbox?order={order.Id}");

    public static string Sign(string secret, string body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    public GatewayEvent? VerifyWebhook(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        var secret = opt.Value.WebhookSecret;
        if (secret.Length == 0 || !headers.TryGetValue(SignatureHeader, out var given)) return null;
        var expected = Encoding.ASCII.GetBytes(Sign(secret, rawBody));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(given.Trim().ToLowerInvariant()))) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var r = doc.RootElement;
            return new GatewayEvent(r.GetProperty("type").GetString()!, r.GetProperty("orderId").GetGuid(), r.GetProperty("gatewayTxnId").GetString()!, r.GetProperty("amount").GetInt64(), r.GetProperty("currency").GetString()!);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }
}

public sealed class PaymentModule : IModule
{
    public string Name => "payment";
    public System.Reflection.Assembly MigrationAssembly => typeof(PaymentModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddOptions<PaymentOptions>().Configure(o => c.GetSection("Payment").Bind(o));
        s.AddScoped<IPaymentGateway, SandboxGateway>();
        s.AddScoped<PaymentService>();
        s.AddScoped<IStatsContributor, PaymentStats>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Bảng giá công khai (BR-WEB-05).
        app.MapGet("/v1/payments/packages", async (PaymentService s, CancellationToken ct) => Results.Ok(await s.PackagesAsync(ct)));

        var g = app.MapGroup("/v1/payments").RequireAuthorization();
        g.MapPost("/orders", async (HttpRequest req, ClaimsPrincipal u, CreateOrderRequest r, PaymentService s, IIdempotency idem, CancellationToken ct) =>
        {
            var key = req.RequireIdempotencyKey(); var acc = u.AccountId();
            return Results.Ok(await idem.RunAsync(acc, key, "payment.order", Idempotency.Hash(r.PackageCode), () => s.CreateOrderAsync(acc, r.PackageCode, ct), ct));
        });
        g.MapGet("/orders", async (ClaimsPrincipal u, PaymentService s, CancellationToken ct) => Results.Ok(await s.ListOrdersAsync(u.AccountId(), ct)));
        g.MapGet("/orders/{id:guid}", async (ClaimsPrincipal u, Guid id, PaymentService s, CancellationToken ct) => Results.Ok(await s.GetOrderAsync(u.AccountId(), id, ct)));

        // IPN/webhook của cổng: không dùng JWT, tin cậy bằng chữ ký trên nội dung gốc (BR-WEB-04).
        app.MapPost("/payments/webhook/{provider}", async (HttpRequest req, string provider, PaymentService s, CancellationToken ct) =>
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            var headers = req.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            var (status, outcome) = await s.HandleWebhookAsync(provider, body, headers, ct);
            return Results.Json(new { outcome }, statusCode: status);
        }).AllowAnonymous();

        if (app is IApplicationBuilder ab && ab.ApplicationServices.GetRequiredService<IConfiguration>().GetValue<bool>("Payment:Sandbox"))
        {
            // Chỉ sandbox: mô phỏng cổng gửi IPN cho đơn của chính người gọi, đi qua đúng đường webhook có chữ ký.
            g.MapPost("/sandbox/{orderId:guid}/simulate", async (ClaimsPrincipal u, Guid orderId, SimulateRequest r, PaymentService s, CancellationToken ct) =>
                Results.Ok(await s.SimulateAsync(u.AccountId(), orderId, r.Outcome, ct)));
        }
    }
}

public sealed class PaymentService(IUnitOfWork uow, IClock clock, IOptions<PaymentOptions> opt, IEnumerable<IPaymentGateway> gateways, IIdentityApi identity, IWalletApi wallet)
{
    private const string OrderCols = "id,package_code,gem,amount_minor,currency,status,created_at,paid_at,refunded_at";
    private static OrderDto Map(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3), r.GetString(4), r.GetString(5), r.GetFieldValue<DateTimeOffset>(6),
        r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8));

    private IPaymentGateway Gateway() =>
        gateways.FirstOrDefault(g => g.Name == opt.Value.Provider) ?? throw new DomainException(ErrorCodes.PaymentUnavailable, "Online top-up is not available right now", 503);

    public async Task<IReadOnlyList<GemPackage>> PackagesAsync(CancellationToken ct) =>
        await uow.QueryAsync("SELECT code,gem,price_minor,currency FROM payment.gem_package WHERE active ORDER BY sort", r => new GemPackage(r.GetString(0), r.GetInt64(1), r.GetInt64(2), r.GetString(3)), ct);

    public Task<CheckoutResult> CreateOrderAsync(Guid acc, string? packageCode, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var gw = Gateway();
        var pkg = (await PackagesAsync(ct)).FirstOrDefault(p => p.Code == packageCode) ?? throw DomainException.NotFound("Gem package");
        // BRD 8.1: Restricted vì FRAUD không được nạp; Restricted vì NEGATIVE_GEM thì được nạp bù.
        var info = await identity.GetAsync(acc, ct);
        if (info.IsMinor && !info.GuardianConsent) throw new DomainException(ErrorCodes.GuardianConsentRequired, "A parent or guardian must agree before this account can top up", 403);   // BR-ACC-01, BR-WEB-07
        if (info.Status is "Banned" or "PendingDeletion" or "Deleted" || (info.Status == "Restricted" && info.RestrictionReason != "NEGATIVE_GEM"))
            throw new DomainException(ErrorCodes.AccountRestricted, "This account cannot top up", 403);
        var order = (await uow.QueryAsync($@"INSERT INTO payment.payment_order(id,account_id,package_code,gem,amount_minor,currency,provider,status,created_at)
            VALUES(@id,@a,@p,@g,@m,@c,@pr,'Created',@now) RETURNING {OrderCols}", Map, ct,
            ("id", Guid.NewGuid()), ("a", acc), ("p", pkg.Code), ("g", pkg.Gem), ("m", pkg.PriceMinor), ("c", pkg.Currency), ("pr", gw.Name), ("now", clock.UtcNow)))[0];
        return new CheckoutResult(order, await gw.CreateCheckoutAsync(order, ct));
    }, ct);

    public async Task<IReadOnlyList<OrderDto>> ListOrdersAsync(Guid acc, CancellationToken ct) =>
        await uow.QueryAsync($"SELECT {OrderCols} FROM payment.payment_order WHERE account_id=@a ORDER BY created_at DESC LIMIT 50", Map, ct, ("a", acc));

    public async Task<OrderDto> GetOrderAsync(Guid acc, Guid id, CancellationToken ct) =>
        (await uow.QueryAsync($"SELECT {OrderCols} FROM payment.payment_order WHERE id=@i AND account_id=@a", Map, ct, ("i", id), ("a", acc))).FirstOrDefault() ?? throw DomainException.NotFound("Order");

    // ---------- Webhook (BR-WEB-04, BR-WAL-02/04) ----------
    private Task Log(string provider, bool sigOk, GatewayEvent? e, string outcome, string body, CancellationToken ct) =>
        uow.ExecAsync("INSERT INTO payment.webhook_log(received_at,provider,signature_valid,event_type,order_id,gateway_txn_id,outcome,body) VALUES(@now,@p,@s,@t,@o,@g,@out,@b)", ct,
            ("now", clock.UtcNow), ("p", provider), ("s", sigOk), ("t", e?.Type), ("o", e?.OrderId), ("g", e?.GatewayTxnId), ("out", outcome), ("b", body.Length > 8000 ? body[..8000] : body));

    /// <summary>Trả về (mã HTTP, kết quả). Không ném lỗi nghiệp vụ để nhật ký luôn được ghi, kể cả khi bị từ chối.</summary>
    public async Task<(int Status, string Outcome)> HandleWebhookAsync(string provider, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        var gw = gateways.FirstOrDefault(g => g.Name == provider);
        if (gw is null) return (404, "UNKNOWN_PROVIDER");
        var ev = gw.VerifyWebhook(body, headers);
        if (ev is null) { await Log(provider, false, null, "INVALID_SIGNATURE", body, ct); return (401, ErrorCodes.InvalidSignature); }

        try
        {
            var outcome = await uow.RunAsync(() => ApplyEventAsync(provider, ev, ct), ct);
            await Log(provider, true, ev, outcome, body, ct);
            return (outcome.StartsWith("ANOMALY", StringComparison.Ordinal) ? 409 : 200, outcome);
        }
        catch (DomainException e)
        {
            await Log(provider, true, ev, e.Code, body, ct);
            return (e.Status, e.Code);
        }
    }

    private async Task<string> ApplyEventAsync(string provider, GatewayEvent ev, CancellationToken ct)
    {
        var o = (await uow.QueryAsync("SELECT account_id,status,gem,amount_minor,currency,gateway_txn_id FROM payment.payment_order WHERE id=@i FOR UPDATE",
            r => (Acc: r.GetGuid(0), Status: r.GetString(1), Gem: r.GetInt64(2), Amount: r.GetInt64(3), Cur: r.GetString(4), Txn: r.IsDBNull(5) ? null : r.GetString(5)), ct, ("i", ev.OrderId))).FirstOrDefault();
        if (o.Cur is null) throw DomainException.NotFound("Order");
        if (ev.AmountMinor != o.Amount || ev.Currency != o.Cur) throw new DomainException(ErrorCodes.PaymentAmountMismatch, "Paid amount does not match the order", 400);

        switch (ev.Type)
        {
            case "payment.succeeded":
                if (o.Status is "Paid" or "Refunded") return o.Txn == ev.GatewayTxnId ? "DUPLICATE" : "ANOMALY_SECOND_PAYMENT";
                if (await uow.ScalarAsync<int?>("SELECT 1 FROM payment.payment_order WHERE provider=@p AND gateway_txn_id=@t", ct, ("p", provider), ("t", ev.GatewayTxnId)) is not null) return "ANOMALY_TXN_REUSED";
                var credit = await wallet.CreditAsync(o.Acc, Currencies.Gem, o.Gem, "GEM_TOPUP", "payment", ev.GatewayTxnId, $"payment:{provider}:{ev.GatewayTxnId}", ct);
                await uow.ExecAsync("UPDATE payment.payment_order SET status='Paid', gateway_txn_id=@t, credit_entry_id=@e, paid_at=@now WHERE id=@i", ct, ("t", ev.GatewayTxnId), ("e", credit.Id), ("now", clock.UtcNow), ("i", ev.OrderId));
                // SC-WAL-13/14: nạp bù đủ (số dư Gem ≥ 0) thì tự gỡ hạn chế NEGATIVE_GEM.
                if (credit.BalanceAfter >= 0) await identity.SetNegativeGemRestrictionAsync(o.Acc, false, ct);
                return "CREDITED";

            case "payment.failed":
                if (o.Status != "Created") return "IGNORED";
                await uow.ExecAsync("UPDATE payment.payment_order SET status='Failed' WHERE id=@i", ct, ("i", ev.OrderId));
                return "FAILED";

            case "payment.refunded":
                if (o.Status == "Refunded") return "DUPLICATE";                      // SC-WAL-15
                if (o.Status != "Paid" || o.Txn != ev.GatewayTxnId) throw new DomainException(ErrorCodes.InvalidState, "There is no matching paid transaction to refund", 409);
                var claw = await wallet.ClawbackGemAsync(o.Acc, o.Gem, "GEM_REFUND", "payment", ev.GatewayTxnId, $"refund:{provider}:{ev.GatewayTxnId}", ct);
                await uow.ExecAsync("UPDATE payment.payment_order SET status='Refunded', clawback_entry_id=@e, refunded_at=@now WHERE id=@i", ct, ("e", claw.Id), ("now", clock.UtcNow), ("i", ev.OrderId));
                if (claw.BalanceAfter < 0) await identity.SetNegativeGemRestrictionAsync(o.Acc, true, ct);
                return claw.BalanceAfter < 0 ? "REFUNDED_NEGATIVE" : "REFUNDED";

            default: return "IGNORED";
        }
    }

    /// <summary>Sandbox: dựng đúng webhook mà cổng sẽ gửi (có chữ ký) rồi đưa qua đường xử lý thật.</summary>
    public async Task<object> SimulateAsync(Guid acc, Guid orderId, string? outcome, CancellationToken ct)
    {
        var type = outcome switch { "succeeded" => "payment.succeeded", "failed" => "payment.failed", "refunded" => "payment.refunded", _ => throw DomainException.Validation("outcome must be succeeded, failed or refunded") };
        var order = await GetOrderAsync(acc, orderId, ct);
        var txn = await uow.ScalarAsync<string?>("SELECT gateway_txn_id FROM payment.payment_order WHERE id=@i", ct, ("i", orderId)) ?? $"SBX-{orderId:N}";
        var body = JsonSerializer.Serialize(new { type, orderId, gatewayTxnId = txn, amount = order.AmountMinor, currency = order.Currency });
        var (status, result) = await HandleWebhookAsync("sandbox", body, new Dictionary<string, string> { [SandboxGateway.SignatureHeader] = SandboxGateway.Sign(opt.Value.WebhookSecret, body) }, ct);
        return new { status, outcome = result, order = await GetOrderAsync(acc, orderId, ct) };
    }
}

internal sealed class PaymentStats(IUnitOfWork uow) : IStatsContributor
{
    public async Task<IReadOnlyDictionary<string, long>> CollectAsync(CancellationToken ct)
    {
        var v = (await uow.QueryAsync(@"SELECT count(*) FILTER (WHERE status IN ('Paid','Refunded')), count(*) FILTER (WHERE status='Refunded'), COALESCE(sum(amount_minor) FILTER (WHERE status='Paid'),0)::bigint,
            COALESCE(sum(gem) FILTER (WHERE status='Paid'),0)::bigint FROM payment.payment_order", r => new[] { r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3) }, ct))[0];
        return new Dictionary<string, long> { ["topups_paid"] = v[0], ["topups_refunded"] = v[1], ["topup_revenue_minor"] = v[2], ["topup_gem_net"] = v[3] };
    }
}
