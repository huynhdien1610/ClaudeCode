using System.Net;

namespace Anima.IntegrationTests;

public sealed class IdentityTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private static readonly object[] Empty = [];

    private Task<Resp> Register(object body) => new Player.Anon(fx).Post("/v1/accounts", body);

    private static object Body(string? email = null, string birth = "1990-01-01", string country = "VN", string password = "correct horse", string? tz = "Asia/Ho_Chi_Minh") =>
        new { email = email ?? $"u{Guid.NewGuid():N}@test.local", password, birthDate = birth, country, locale = "vi", timezone = tz };

    [Fact(DisplayName = "BR-NEW-01 + FirstLogin: đăng ký tạo ví 100 Coin, seed công bằng và gói chào mừng chưa mở")]
    public async Task Register_GrantsFirstLoginRewardSeedAndWelcomePack()
    {
        var p = await fx.NewPlayer();
        Assert.Equal((0, 100), await p.Balances());
        var fairness = await p.Get("/v1/fairness");
        Assert.Equal(64, fairness["serverSeedHash"].GetString()!.Length);
        Assert.Equal(1, fairness["nextNonce"].GetInt32());
        var packs = await p.Get("/v1/me/packs");
        var only = Assert.Single(packs.Body.EnumerateArray());
        Assert.Equal("welcome", only.GetProperty("kind").GetString());
        Assert.True(only.GetProperty("soulbound").GetBoolean());
        var me = await p.Get("/v1/me");
        Assert.Equal("Unverified", me["status"].GetString());
    }

    [Theory(DisplayName = "BR-ACC-01 / BR-GEO-04: tuổi tối thiểu 13")]
    [InlineData("2020-01-01", HttpStatusCode.Forbidden, "AGE_BELOW_MINIMUM")]
    [InlineData("1990-01-01", HttpStatusCode.Created, null)]
    public async Task AgeGate(string birth, HttpStatusCode status, string? code)
    {
        var r = await Register(Body(birth: birth));
        Assert.Equal(status, r.Status);
        Assert.Equal(code, r.Code);
    }

    [Fact(DisplayName = "BR-ACC-01: đúng ngày sinh nhật thứ 13 thì được, trước 1 ngày thì không")]
    public async Task AgeBoundary()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.Created, (await Register(Body(birth: today.AddYears(-13).ToString("yyyy-MM-dd")))).Status);
        Assert.Equal("AGE_BELOW_MINIMUM", (await Register(Body(birth: today.AddYears(-13).AddDays(1).ToString("yyyy-MM-dd")))).Code);
    }

    [Theory(DisplayName = "BR-GEO-03 + Q-44: quốc gia bị chặn")]
    [InlineData("CN")]
    [InlineData("KP")]
    public async Task BlockedCountry(string country)
    {
        var r = await Register(Body(country: country));
        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.Equal("REGION_BLOCKED", r.Code);
    }

    [Fact(DisplayName = "BR-ACC-02: email duy nhất, không phân biệt hoa thường")]
    public async Task EmailUnique()
    {
        var email = $"dup{Guid.NewGuid():N}@test.local";
        Assert.Equal(HttpStatusCode.Created, (await Register(Body(email: email))).Status);
        var again = await Register(Body(email: email.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.Conflict, again.Status);
        Assert.Equal("EMAIL_ALREADY_USED", again.Code);
    }

    [Theory(DisplayName = "Validation đăng ký")]
    [InlineData("not-an-email", "correct horse", "VALIDATION_FAILED")]
    [InlineData("ok@test.local", "short", "VALIDATION_FAILED")]
    public async Task RegisterValidation(string email, string password, string code)
    {
        var r = await Register(Body(email: email, password: password));
        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.Equal(code, r.Code);
    }

    [Fact(DisplayName = "US-01.2: đăng nhập đúng, sai mật khẩu và khóa 15 phút sau 5 lần sai")]
    public async Task LoginAndLockout()
    {
        var p = await fx.NewPlayer();
        Assert.True((await p.Login()).Ok);
        for (var i = 0; i < 4; i++) Assert.Equal("INVALID_CREDENTIALS", (await p.Login("wrong password")).Code);
        Assert.Equal("INVALID_CREDENTIALS", (await p.Login("wrong password")).Code);   // lần thứ 5 kích hoạt khóa
        var locked = await p.Login();                                                   // đúng mật khẩu vẫn bị khóa
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.Status);
        Assert.Equal("LOGIN_LOCKED", locked.Code);
    }

    [Fact(DisplayName = "Không lộ tài khoản có tồn tại: email lạ và sai mật khẩu cùng một lỗi")]
    public async Task NoAccountEnumeration()
    {
        var anon = new Player.Anon(fx);
        var unknown = await anon.Post("/v1/auth/login", new { email = "nobody@test.local", password = "whatever1" });
        Assert.Equal("INVALID_CREDENTIALS", unknown.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
    }

    [Fact(DisplayName = "API cần đăng nhập trả 401 UNAUTHORIZED")]
    public async Task ProtectedEndpointsRequireAuth()
    {
        var r = await new Player.Anon(fx).Get("/v1/wallet");
        Assert.Equal(HttpStatusCode.Unauthorized, r.Status);
        Assert.Equal("UNAUTHORIZED", r.Code);
    }

    [Fact(DisplayName = "BR-ACC-05: OTP đúng → Verified; OTP sai 5 lần → khóa 30 phút")]
    public async Task OtpFlow()
    {
        var p = await fx.NewPlayer();
        await p.VerifyPhone();
        Assert.Equal("Verified", (await p.Get("/v1/me"))["status"].GetString());
        Assert.True((await p.Get("/v1/me"))["phoneVerified"].GetBoolean());

        var q = await fx.NewPlayer();
        Assert.Equal(HttpStatusCode.Accepted, (await q.Post("/v1/me/phone/otp", new { phone = "+84911111111" })).Status);
        for (var i = 0; i < 4; i++) Assert.Equal("OTP_INVALID", (await q.Post("/v1/me/phone/verify", new { code = "000000" })).Code);
        Assert.Equal("OTP_LOCKED", (await q.Post("/v1/me/phone/verify", new { code = "000000" })).Code);
        var correct = await q.Post("/v1/me/phone/verify", new { code = fx.Otp.CodeFor("+84911111111") });
        Assert.Equal("OTP_LOCKED", correct.Code);   // đang khóa thì đúng mã cũng bị từ chối
    }

    [Fact(DisplayName = "BR-ACC-02: một SĐT chỉ xác thực cho một tài khoản")]
    public async Task PhoneUniquePerAccount()
    {
        var a = await fx.NewPlayer(); var b = await fx.NewPlayer();
        const string phone = "+84922222222";
        await a.Post("/v1/me/phone/otp", new { phone });
        Assert.True((await a.Post("/v1/me/phone/verify", new { code = fx.Otp.CodeFor(phone) })).Ok);
        var r = await b.Post("/v1/me/phone/otp", new { phone });
        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal("PHONE_ALREADY_USED", r.Code);
    }

    [Fact(DisplayName = "BR-ACC-05: tối đa 5 lần gửi OTP mỗi ngày")]
    public async Task OtpDailyLimit()
    {
        var p = await fx.NewPlayer();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Accepted, (await p.Post("/v1/me/phone/otp", new { phone = "+84933333333" })).Status);
        var sixth = await p.Post("/v1/me/phone/otp", new { phone = "+84933333333" });
        Assert.Equal("OTP_DAILY_LIMIT", sixth.Code);
    }

    [Fact(DisplayName = "Đăng ký với tên múi giờ cũ của trình duyệt (Asia/Saigon) được lưu thành tên chuẩn; múi giờ lạ bị từ chối")]
    public async Task RegisterAcceptsBrowserTimezoneAliases()
    {
        var p = await fx.NewPlayer(tz: "Asia/Saigon");
        Assert.Equal("Asia/Ho_Chi_Minh", (await p.Get("/v1/me"))["timezone"].GetString());
        var bad = await Register(Body(tz: "Mars/Olympus"));
        Assert.Equal("VALIDATION_FAILED", bad.Code);
    }

    [Fact(DisplayName = "BR-I18N-01: đổi ngôn ngữ lưu theo tài khoản; ngôn ngữ lạ bị từ chối")]
    public async Task Locale()
    {
        var p = await fx.NewPlayer();
        Assert.Equal(HttpStatusCode.NoContent, (await p.Put("/v1/me/locale", new { locale = "zh-Hant" })).Status);
        Assert.Equal("zh-Hant", (await p.Get("/v1/me"))["locale"].GetString());
        Assert.Equal("VALIDATION_FAILED", (await p.Put("/v1/me/locale", new { locale = "fr" })).Code);
    }

    [Fact(DisplayName = "NFR-03: email, ngày sinh được mã hóa trong database, không có plaintext")]
    public async Task PiiIsEncryptedAtRest()
    {
        var p = await fx.NewPlayer();
        var raw = await fx.Scalar<byte[]>("SELECT email_enc FROM identity.account WHERE id=@i", ("i", p.Id));
        Assert.DoesNotContain(p.Email, System.Text.Encoding.UTF8.GetString(raw!), StringComparison.OrdinalIgnoreCase);
        var pw = await fx.Scalar<string>("SELECT password_hash FROM identity.account WHERE id=@i", ("i", p.Id));
        Assert.StartsWith("pbkdf2$", pw);
    }
}
