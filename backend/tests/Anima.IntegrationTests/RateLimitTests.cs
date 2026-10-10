using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Giới hạn tần suất (NFR-16): host riêng với ngưỡng thấp để kiểm.</summary>
public sealed class RateLimitTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fx.WithWebHostBuilder(b =>
    {
        b.UseSetting("RateLimit:Enabled", "true");
        b.UseSetting("RateLimit:AuthPermit", "3");
        b.UseSetting("RateLimit:EconomyPermit", "5");
        b.UseSetting("RateLimit:DefaultPermit", "8");
    }).CreateClient();

    private static async Task<string> Register(HttpClient http, string email)
    {
        var r = await http.PostAsJsonAsync("/v1/accounts", new { email, password = "correct horse", birthDate = "1990-01-01", country = "VN", locale = "vi", timezone = "Asia/Ho_Chi_Minh" });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Req(HttpMethod m, string url, string? token = null, object? body = null)
    {
        var r = new HttpRequestMessage(m, url);
        if (token is not null) r.Headers.Authorization = new("Bearer", token);
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }

    [Fact(DisplayName = "Nhóm auth: quá 3 lần/phút theo IP thì 429 RATE_LIMITED kèm Retry-After")]
    public async Task AuthIsLimitedPerIp()
    {
        using var http = Client();
        await Register(http, $"rl{Guid.NewGuid():N}@test.local");                                   // lần 1
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(Req(HttpMethod.Post, "/v1/auth/login", body: new { email = "no@test.local", password = "x" }))).StatusCode);
        var limited = await http.SendAsync(Req(HttpMethod.Post, "/v1/auth/login", body: new { email = "no@test.local", password = "x" }));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta?.TotalSeconds > 0 || limited.Headers.Contains("Retry-After"));
        Assert.Equal("RATE_LIMITED", (await limited.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Nhóm kinh tế: giới hạn theo tài khoản — tài khoản này bị chặn không ảnh hưởng tài khoản khác")]
    public async Task EconomyIsLimitedPerAccount()
    {
        using var http = Client();
        // AuthPermit=3 cho cả host: tạo 2 tài khoản dùng 2 lượt.
        var a = await Register(http, $"rla{Guid.NewGuid():N}@test.local"); var b = await Register(http, $"rlb{Guid.NewGuid():N}@test.local");
        for (var i = 0; i < 5; i++) Assert.NotEqual(HttpStatusCode.TooManyRequests, (await http.SendAsync(Req(HttpMethod.Post, "/v1/wallet/convert", a, new { direction = "GEM_TO_COIN", amount = 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await http.SendAsync(Req(HttpMethod.Post, "/v1/wallet/convert", a, new { direction = "GEM_TO_COIN", amount = 1 }))).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await http.SendAsync(Req(HttpMethod.Post, "/v1/wallet/convert", b, new { direction = "GEM_TO_COIN", amount = 1 }))).StatusCode);
    }

    [Fact(DisplayName = "Nhóm mặc định giới hạn riêng; /healthz và webhook cổng thanh toán được miễn")]
    public async Task DefaultAndExemptions()
    {
        using var http = Client();
        var token = await Register(http, $"rld{Guid.NewGuid():N}@test.local");
        for (var i = 0; i < 8; i++) Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Req(HttpMethod.Get, "/v1/wallet", token))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await http.SendAsync(Req(HttpMethod.Get, "/v1/wallet", token))).StatusCode);
        for (var i = 0; i < 30; i++) Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/healthz")).StatusCode);
        for (var i = 0; i < 12; i++) Assert.NotEqual(HttpStatusCode.TooManyRequests, (await http.SendAsync(Req(HttpMethod.Post, "/payments/webhook/sandbox", body: new { }))).StatusCode);
    }

    [Fact(DisplayName = "Mặc định của các test khác tắt giới hạn, và khi bật thì ngưỡng mặc định đủ rộng cho một người chơi bình thường")]
    public void DefaultsAreGenerous()
    {
        var o = new Anima.Api.RateLimitOptions();
        Assert.True(o.Enabled); Assert.True(o.AuthPermit >= 20); Assert.True(o.EconomyPermit >= 60); Assert.True(o.DefaultPermit >= 300);
    }
}
