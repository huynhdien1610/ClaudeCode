using System.Net;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Đồng ý của người giám hộ khi nạp Gem: BR-ACC-01, BR-WEB-07, SC-ACC-04 (cơ chế tối thiểu, Q-21).</summary>
public sealed class GuardianTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private static string Birth(int yearsAgo, int extraDays = 0) => DateTime.UtcNow.Date.AddYears(-yearsAgo).AddDays(extraDays).ToString("yyyy-MM-dd");

    [Fact(DisplayName = "SC-ACC-04: người dưới 18 chưa có đồng ý của người giám hộ thì không nạp được; có đồng ý rồi thì nạp được")]
    public async Task MinorNeedsConsent()
    {
        var p = await fx.NewPlayer(birthDate: Birth(16));
        var me = await p.Get("/v1/me");
        Assert.True(me["isMinor"].GetBoolean()); Assert.False(me["guardianConsent"].GetBoolean());
        var blocked = await p.Post("/v1/payments/orders", new { packageCode = "gem_100" });
        Assert.Equal("GUARDIAN_CONSENT_REQUIRED", blocked.Code); Assert.Equal(HttpStatusCode.Forbidden, blocked.Status);
        Assert.Equal(0, (await p.Balances()).Gem);

        var guardianEmail = $"parent{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.Accepted, (await p.Post("/v1/me/guardian-consent", new { guardianEmail })).Status);
        Assert.Equal("GUARDIAN_CONSENT_REQUIRED", (await p.Post("/v1/payments/orders", new { packageCode = "gem_100" })).Code);      // mới xin, chưa đồng ý

        var anon = new Player.Anon(fx);
        Assert.Equal("GUARDIAN_TOKEN_INVALID", (await anon.Post("/v1/guardian/confirm", new { token = "nope" })).Code);
        var token = fx.Guardian.TokenFor(guardianEmail);
        Assert.Equal(HttpStatusCode.NoContent, (await anon.Post("/v1/guardian/confirm", new { token })).Status);
        Assert.Equal("GUARDIAN_TOKEN_INVALID", (await anon.Post("/v1/guardian/confirm", new { token })).Code);                         // dùng một lần
        Assert.True((await p.Get("/v1/me"))["guardianConsent"].GetBoolean());
        Assert.True((await p.Post("/v1/payments/orders", new { packageCode = "gem_100" })).Ok);
    }

    [Fact(DisplayName = "Đúng sinh nhật 18 tuổi là người lớn; thiếu một ngày vẫn là vị thành niên; người lớn không cần xin đồng ý")]
    public async Task AgeBoundary()
    {
        var adult = await fx.NewPlayer(birthDate: Birth(18));
        Assert.False((await adult.Get("/v1/me"))["isMinor"].GetBoolean());
        Assert.True((await adult.Post("/v1/payments/orders", new { packageCode = "gem_100" })).Ok);
        Assert.Equal("VALIDATION_FAILED", (await adult.Post("/v1/me/guardian-consent", new { guardianEmail = "p@example.com" })).Code);

        var almost = await fx.NewPlayer(birthDate: Birth(18, 1));
        Assert.True((await almost.Get("/v1/me"))["isMinor"].GetBoolean());
        Assert.Equal("GUARDIAN_CONSENT_REQUIRED", (await almost.Post("/v1/payments/orders", new { packageCode = "gem_100" })).Code);
    }

    [Fact(DisplayName = "Mã xác nhận hết hạn sau 7 ngày; email người giám hộ phải hợp lệ; xin lại thì mã cũ mất hiệu lực")]
    public async Task TokenExpiryAndReissue()
    {
        var p = await fx.NewPlayer(birthDate: Birth(15));
        Assert.Equal("VALIDATION_FAILED", (await p.Post("/v1/me/guardian-consent", new { guardianEmail = "not-an-email" })).Code);
        var email = $"parent{Guid.NewGuid():N}@example.com";
        await p.Post("/v1/me/guardian-consent", new { guardianEmail = email });
        var first = fx.Guardian.TokenFor(email);
        await p.Post("/v1/me/guardian-consent", new { guardianEmail = email });
        var second = fx.Guardian.TokenFor(email);
        Assert.NotEqual(first, second);
        var anon = new Player.Anon(fx);
        Assert.Equal("GUARDIAN_TOKEN_INVALID", (await anon.Post("/v1/guardian/confirm", new { token = first })).Code);
        await fx.Sql("UPDATE identity.guardian_request SET expires_at = now() - interval '1 minute' WHERE account_id=@a", ("a", p.Id));
        Assert.Equal("GUARDIAN_TOKEN_INVALID", (await anon.Post("/v1/guardian/confirm", new { token = second })).Code);
        Assert.False((await p.Get("/v1/me"))["guardianConsent"].GetBoolean());
    }
}
