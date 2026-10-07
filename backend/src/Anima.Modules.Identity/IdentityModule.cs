using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Anima.Contracts;
using Anima.Identity.Contracts;
using Anima.SharedKernel;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Anima.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "identity";
    public System.Reflection.Assembly MigrationAssembly => typeof(IdentityModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddOptions<IdentityOptions>().Configure(o =>
        {
            c.GetSection("Identity").Bind(o);
        });
        s.AddScoped<IdentityService>();
        s.AddScoped<IIdentityApi>(sp => sp.GetRequiredService<IdentityService>());
        s.AddScoped<IdentityAdminService>();
        s.AddScoped<IIdentityAdminApi>(sp => sp.GetRequiredService<IdentityAdminService>());
        s.AddScoped<IStatsContributor>(sp => sp.GetRequiredService<IdentityAdminService>());
        s.AddSingleton<IOtpSender, LoggingOtpSender>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var anon = app.MapGroup("/v1");
        anon.MapPost("/accounts", async (RegisterRequest r, IdentityService svc, CancellationToken ct) => Results.Created("/v1/me", await svc.RegisterAsync(r, ct)));
        anon.MapPost("/auth/login", async (LoginRequest r, IdentityService svc, CancellationToken ct) => Results.Ok(await svc.LoginAsync(r, ct)));

        var me = app.MapGroup("/v1/me").RequireAuthorization();
        me.MapGet("", async (ClaimsPrincipal u, IdentityService svc, CancellationToken ct) => Results.Ok(await svc.GetAsync(u.AccountId(), ct)));
        me.MapPut("/locale", async (ClaimsPrincipal u, LocaleRequest r, IdentityService svc, CancellationToken ct) =>
        { await svc.SetLocaleAsync(u.AccountId(), r.Locale, ct); return Results.NoContent(); });
        me.MapPost("/phone/otp", async (ClaimsPrincipal u, PhoneRequest r, IdentityService svc, CancellationToken ct) =>
        { await svc.SendOtpAsync(u.AccountId(), r.Phone, ct); return Results.Accepted(); });
        me.MapPost("/phone/verify", async (ClaimsPrincipal u, OtpVerifyRequest r, IdentityService svc, CancellationToken ct) =>
            Results.Ok(await svc.VerifyOtpAsync(u.AccountId(), r.Code, ct)));
    }
}

public sealed class IdentityOptions
{
    public int MinAge { get; set; } = 13;
    public string[] BlockedCountries { get; set; } = ["KP", "IR", "SY", "CU", "CN"]; // sanctions + BRD Q-44
    public int Pbkdf2Iterations { get; set; } = 600_000;   // khuyến nghị OWASP cho PBKDF2-HMAC-SHA256
    public string JwtKey { get; set; } = "anima-dev-jwt-signing-key-change-me-0123456789";
    public string JwtIssuer { get; set; } = "anima";
    public string JwtAudience { get; set; } = "anima-web";
    public int TokenHours { get; set; } = 12;
    /// <summary>Chỉ dev: ghi mã OTP ra log để xác thực SĐT khi chưa có nhà cung cấp SMS.</summary>
    public bool DevLogOtp { get; set; }
}

public sealed record RegisterRequest(string? Email, string? Password, string? BirthDate, string? Country, string? Locale, string? Timezone);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record LocaleRequest(string? Locale);
public sealed record PhoneRequest(string? Phone);
public sealed record OtpVerifyRequest(string? Code);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AccountInfo Account);

public interface IOtpSender { Task SendAsync(string phoneE164, string code, CancellationToken ct); }

/// <summary>Bản dev: không gửi SMS. Chỉ ghi mã ra log khi Identity:DevLogOtp = true. Thay bằng nhà cung cấp SMS thật khi chọn (T026).</summary>
public sealed partial class LoggingOtpSender(ILogger<LoggingOtpSender> log, Microsoft.Extensions.Options.IOptions<IdentityOptions> opt) : IOtpSender
{
    public Task SendAsync(string phoneE164, string code, CancellationToken ct)
    {
        if (opt.Value.DevLogOtp) LogOtp(log, phoneE164, code);
        else LogNotSent(log, phoneE164[..Math.Min(4, phoneE164.Length)] + "****");
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DEV OTP for {Phone}: {Code}")]
    private static partial void LogOtp(ILogger l, string phone, string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "No SMS provider configured: OTP for {Phone} was not sent")]
    private static partial void LogNotSent(ILogger l, string phone);
}

public sealed partial class IdentityService(IUnitOfWork uow, IFieldCipher cipher, IClock clock, IDomainEventPublisher events,
    Microsoft.Extensions.Options.IOptions<IdentityOptions> opt, IOtpSender otp) : IIdentityApi
{
    private readonly IdentityOptions _o = opt.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")] private static partial Regex EmailRx();
    [GeneratedRegex(@"^\+[1-9][0-9]{7,14}$")] private static partial Regex PhoneRx();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest r, CancellationToken ct)
    {
        var email = (r.Email ?? "").Trim();
        if (!EmailRx().IsMatch(email) || email.Length > 200) throw DomainException.Validation("Invalid email");
        if ((r.Password ?? "").Length < 8 || r.Password!.Length > 200) throw DomainException.Validation("Password must be at least 8 characters");
        var country = (r.Country ?? "").Trim().ToUpperInvariant();
        if (country.Length != 2) throw DomainException.Validation("Country must be an ISO 3166-1 alpha-2 code");
        if (_o.BlockedCountries.Contains(country)) throw new DomainException(ErrorCodes.RegionBlocked, "Service is not available in this country", 403);
        if (!DateOnly.TryParseExact(r.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birth)) throw DomainException.Validation("BirthDate must be yyyy-MM-dd");
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var age = today.Year - birth.Year - (today < birth.AddYears(today.Year - birth.Year) ? 1 : 0);
        if (age < _o.MinAge) throw new DomainException(ErrorCodes.AgeBelowMinimum, $"Minimum age is {_o.MinAge}", 403);
        var tz = "UTC";
        if (!string.IsNullOrWhiteSpace(r.Timezone))
        {
            if (!TimeZones.TryResolve(r.Timezone, out var resolved)) throw DomainException.Validation("Unknown timezone");
            tz = resolved.Id;       // lưu tên mà máy chủ hiểu được, kể cả khi trình duyệt báo tên cũ
        }
        var locale = Locales.Normalize(r.Locale);

        var id = Guid.NewGuid();
        var emailNorm = email.ToLowerInvariant();
        return await uow.RunAsync(async () =>
        {
            var n = await uow.ExecAsync(@"INSERT INTO identity.account(id,email_enc,email_hash,password_hash,birth_date_enc,legal_country,locale,timezone,status,created_at)
                VALUES(@id,@ee,@eh,@pw,@bd,@c,@l,@tz,'Unverified',@now) ON CONFLICT (email_hash) DO NOTHING", ct,
                ("id", id), ("ee", cipher.Encrypt(emailNorm)), ("eh", cipher.LookupHash(emailNorm)), ("pw", HashPassword(r.Password!)),
                ("bd", cipher.Encrypt(birth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))), ("c", country), ("l", locale), ("tz", tz), ("now", clock.UtcNow));
            if (n == 0) throw DomainException.Conflict(ErrorCodes.EmailAlreadyUsed, "Email is already registered");
            await events.PublishAsync(new AccountRegistered(id, country, locale), ct);
            return await IssueAsync(id, ct);
        }, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var email = (r.Email ?? "").Trim().ToLowerInvariant();
        var pw = r.Password ?? "";
        var rows = await uow.QueryAsync("SELECT id, password_hash, failed_logins, locked_until, status FROM identity.account WHERE email_hash=@h",
            x => (Id: x.GetGuid(0), Hash: x.GetString(1), Failed: x.GetInt32(2), Locked: x.IsDBNull(3) ? (DateTimeOffset?)null : x.GetFieldValue<DateTimeOffset>(3), Status: x.GetString(4)), ct, ("h", cipher.LookupHash(email)));
        if (rows.Count == 0) { VerifyPassword(pw, DummyHash); throw new DomainException(ErrorCodes.InvalidCredentials, "Invalid email or password", 401); }
        var a = rows[0];
        if (a.Locked > clock.UtcNow) throw new DomainException(ErrorCodes.LoginLocked, "Too many failed attempts. Try again later", 429);
        if (a.Status is "Banned" or "Deleted") throw new DomainException(ErrorCodes.InvalidCredentials, "Invalid email or password", 401);
        if (!VerifyPassword(pw, a.Hash))
        {
            var failed = a.Failed + 1;
            await uow.ExecAsync("UPDATE identity.account SET failed_logins=@f, locked_until=@u WHERE id=@id", ct,
                ("f", failed >= 5 ? 0 : failed), ("u", failed >= 5 ? clock.UtcNow.AddMinutes(15) : null), ("id", a.Id));
            throw new DomainException(ErrorCodes.InvalidCredentials, "Invalid email or password", 401);
        }
        await uow.ExecAsync("UPDATE identity.account SET failed_logins=0, locked_until=NULL WHERE id=@id", ct, ("id", a.Id));
        return await IssueAsync(a.Id, ct);
    }

    private async Task<AuthResponse> IssueAsync(Guid id, CancellationToken ct)
    {
        var info = await GetAsync(id, ct);
        var exp = clock.UtcNow.AddHours(_o.TokenHours);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.JwtKey));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _o.JwtIssuer,
            Audience = _o.JwtAudience,
            Expires = exp.UtcDateTime,
            IssuedAt = clock.UtcNow.UtcDateTime,
            Claims = new Dictionary<string, object> { ["sub"] = id.ToString() },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
        return new AuthResponse(token, exp, info);
    }

    public async Task<AccountInfo> GetAsync(Guid accountId, CancellationToken ct)
    {
        var rows = await uow.QueryAsync("SELECT id,status,phone_verified_at IS NOT NULL,locale,timezone,legal_country,restriction_reason,created_at FROM identity.account WHERE id=@id",
            r => new AccountInfo(r.GetGuid(0), r.GetString(1), r.GetBoolean(2), r.GetString(3), r.GetString(4), r.GetString(5).Trim(), r.IsDBNull(6) ? null : r.GetString(6), r.GetFieldValue<DateTimeOffset>(7)), ct, ("id", accountId));
        return rows.Count > 0 ? rows[0] : throw new DomainException(ErrorCodes.Unauthorized, "Account not found", 401);
    }

    public async Task SetLocaleAsync(Guid id, string? locale, CancellationToken ct)
    {
        if (Array.IndexOf(Locales.Supported, locale) < 0) throw DomainException.Validation("Unsupported locale");
        await uow.ExecAsync("UPDATE identity.account SET locale=@l WHERE id=@id", ct, ("l", locale), ("id", id));
    }

    // ---- OTP (BR-ACC-05): 6 chữ số, 5 phút, tối đa 5 lần sai → khóa 30 phút, tối đa 5 lần gửi/ngày ----
    public async Task SendOtpAsync(Guid id, string? phone, CancellationToken ct)
    {
        phone = (phone ?? "").Trim();
        if (!PhoneRx().IsMatch(phone)) throw DomainException.Validation("Phone must be in E.164 format");
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var now = clock.UtcNow; var today = DateOnly.FromDateTime(now.UtcDateTime);
        var ph = cipher.LookupHash(phone);
        await uow.RunAsync(async () =>
        {
            await uow.ExecAsync("SELECT 1 FROM identity.account WHERE id=@id FOR UPDATE", ct, ("id", id));
            if (await uow.ScalarAsync<int?>("SELECT 1 FROM identity.account WHERE phone_hash=@h AND phone_verified_at IS NOT NULL AND id<>@id", ct, ("h", ph), ("id", id)) is not null)
                throw DomainException.Conflict(ErrorCodes.PhoneAlreadyUsed, "Phone number is already used");
            var cur = await uow.QueryAsync("SELECT sent_on, sent_count, locked_until FROM identity.otp WHERE account_id=@id",
                r => (On: DateOnly.FromDateTime(r.GetDateTime(0)), Count: r.GetInt32(1), Locked: r.IsDBNull(2) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(2)), ct, ("id", id));
            var sentToday = cur.Count > 0 && cur[0].On == today ? cur[0].Count : 0;
            if (cur.Count > 0 && cur[0].Locked > now) throw new DomainException(ErrorCodes.OtpLocked, "OTP verification is locked", 429);
            if (sentToday >= 5) throw new DomainException(ErrorCodes.OtpDailyLimit, "Daily OTP limit reached", 429);
            // Mỗi OTP mới vô hiệu OTP cũ.
            await uow.ExecAsync(@"INSERT INTO identity.otp(account_id,phone_hash,phone_enc,code_hash,expires_at,attempts,sent_on,sent_count)
                VALUES(@id,@ph,@pe,@ch,@exp,0,@on,@cnt)
                ON CONFLICT (account_id) DO UPDATE SET phone_hash=@ph, phone_enc=@pe, code_hash=@ch, expires_at=@exp, attempts=0, sent_on=@on, sent_count=@cnt", ct,
                ("id", id), ("ph", ph), ("pe", cipher.Encrypt(phone)), ("ch", cipher.LookupHash("otp:" + id + ":" + code)), ("exp", now.AddMinutes(5)), ("on", today.ToDateTime(TimeOnly.MinValue)), ("cnt", sentToday + 1));
        }, ct);
        await otp.SendAsync(phone, code, ct);
    }

    public async Task<AccountInfo> VerifyOtpAsync(Guid id, string? code, CancellationToken ct)
    {
        var now = clock.UtcNow;
        // Đếm lần sai phải được ghi dù lần kiểm tra thất bại, nên không bọc trong transaction ném lỗi.
        var rows = await uow.QueryAsync("SELECT code_hash, phone_hash, phone_enc, expires_at, attempts, locked_until FROM identity.otp WHERE account_id=@id",
            r => (Code: r.GetFieldValue<byte[]>(0), Ph: r.GetFieldValue<byte[]>(1), Pe: r.GetFieldValue<byte[]>(2), Exp: r.GetFieldValue<DateTimeOffset>(3), Att: r.GetInt32(4), Locked: r.IsDBNull(5) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(5)), ct, ("id", id));
        if (rows.Count == 0) throw new DomainException(ErrorCodes.OtpInvalid, "No OTP requested", 400);
        var o = rows[0];
        if (o.Locked > now) throw new DomainException(ErrorCodes.OtpLocked, "OTP verification is locked", 429);
        if (o.Exp < now) throw new DomainException(ErrorCodes.OtpExpired, "OTP expired", 400);
        var ok = code is { Length: 6 } && CryptographicOperations.FixedTimeEquals(o.Code, cipher.LookupHash("otp:" + id + ":" + code));
        if (!ok)
        {
            var att = o.Att + 1;
            await uow.ExecAsync("UPDATE identity.otp SET attempts=@a, locked_until=@l WHERE account_id=@id", ct,
                ("a", att >= 5 ? 0 : att), ("l", att >= 5 ? now.AddMinutes(30) : null), ("id", id));
            throw new DomainException(att >= 5 ? ErrorCodes.OtpLocked : ErrorCodes.OtpInvalid, att >= 5 ? "Too many attempts. Locked for 30 minutes" : "Incorrect OTP", att >= 5 ? 429 : 400);
        }
        await uow.RunAsync(async () =>
        {
            try
            {
                await uow.ExecAsync(@"UPDATE identity.account SET phone_hash=@ph, phone_enc=@pe, phone_verified_at=@now,
                    status=CASE WHEN status='Unverified' THEN 'Verified' ELSE status END WHERE id=@id", ct, ("ph", o.Ph), ("pe", o.Pe), ("now", now), ("id", id));
            }
            catch (Npgsql.PostgresException e) when (e.SqlState == "23505") { throw DomainException.Conflict(ErrorCodes.PhoneAlreadyUsed, "Phone number is already used"); }
            await uow.ExecAsync("DELETE FROM identity.otp WHERE account_id=@id", ct, ("id", id));
            await events.PublishAsync(new PhoneVerified(id), ct);
        }, ct);
        return await GetAsync(id, ct);
    }

    // ---- Mật khẩu: PBKDF2-SHA256 (dùng chung với quản trị viên) ----
    private static readonly string DummyHash = PasswordHasher.Hash("dummy-password", 1000);
    private string HashPassword(string pw) => PasswordHasher.Hash(pw, _o.Pbkdf2Iterations);
    private static bool VerifyPassword(string pw, string stored) => PasswordHasher.Verify(pw, stored);
}
