using System.Threading.RateLimiting;
using Anima.Contracts;

namespace Anima.Api;

public sealed class RateLimitOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>Tin header X-Forwarded-For để lấy IP thật. Chỉ bật khi API nằm sau proxy/LB tin cậy; nếu không, ai cũng giả được IP.</summary>
    public bool TrustForwardedFor { get; set; }
    public int WindowSeconds { get; set; } = 60;
    /// <summary>Đăng ký, đăng nhập, OTP, đăng nhập admin — theo IP, chống dò mật khẩu/spam (bổ sung cho khóa tài khoản).</summary>
    public int AuthPermit { get; set; } = 30;
    /// <summary>API kinh tế và hành động ghi (mua/mở pack, đổi tiền, rèn, nạp, nhiệm vụ, trận) — theo tài khoản (NFR-16).</summary>
    public int EconomyPermit { get; set; } = 120;
    /// <summary>Mọi API còn lại.</summary>
    public int DefaultPermit { get; set; } = 600;
}

/// <summary>
/// Giới hạn tần suất (NFR-16). Một bộ giới hạn toàn cục chọn nhóm theo đường dẫn:
/// auth (theo IP), economy (theo tài khoản), mặc định (theo tài khoản hoặc IP). Webhook của cổng thanh toán và /healthz được miễn.
/// </summary>
public static class RateLimiting
{
    private static readonly string[] AuthPrefixes = ["/v1/auth", "/v1/accounts", "/v1/me/phone", "/admin/v1/auth", "/v1/guardian"];
    private static readonly string[] EconomyPrefixes = ["/v1/packs", "/v1/pack-instances", "/v1/wallet/convert", "/v1/forge", "/v1/payments", "/v1/me/quests", "/v1/battles", "/v1/decks", "/v1/dev"];

    public static string Group(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (path == "/healthz" || path.StartsWith("/payments/webhook", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)) return "exempt";
        if (HttpMethods.IsPost(ctx.Request.Method) || HttpMethods.IsPut(ctx.Request.Method) || HttpMethods.IsDelete(ctx.Request.Method))
        {
            if (AuthPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return "auth";
            if (EconomyPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return "economy";
        }
        return "default";
    }

    private static string ClientIp(HttpContext ctx, RateLimitOptions o)
    {
        if (o.TrustForwardedFor && ctx.Request.Headers["X-Forwarded-For"].ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { } xff) return xff;
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "local";
    }

    public static void Add(IServiceCollection services, IConfiguration config)
    {
        var o = new RateLimitOptions(); config.GetSection("RateLimit").Bind(o);
        services.AddRateLimiter(rl =>
        {
            rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rl.OnRejected = async (c, ct) =>
            {
                if (c.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)) c.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retry.TotalSeconds)).ToString();
                c.HttpContext.Response.StatusCode = 429;
                await c.HttpContext.Response.WriteAsJsonAsync(new { code = ErrorCodes.RateLimited, message = "Too many requests. Please slow down and try again shortly." }, ct);
            };
            rl.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                if (!o.Enabled) return RateLimitPartition.GetNoLimiter("off");
                var group = Group(ctx);
                if (group == "exempt") return RateLimitPartition.GetNoLimiter("exempt");
                var account = ctx.User.FindFirst("sub")?.Value;
                var (key, permit) = group switch
                {
                    "auth" => ($"auth:{ClientIp(ctx, o)}", o.AuthPermit),
                    "economy" => ($"eco:{account ?? ClientIp(ctx, o)}", o.EconomyPermit),
                    _ => ($"def:{account ?? ClientIp(ctx, o)}", o.DefaultPermit),
                };
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromSeconds(o.WindowSeconds), QueueLimit = 0 });
            });
        });
    }
}
