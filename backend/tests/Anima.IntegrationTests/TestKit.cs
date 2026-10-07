using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Anima.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Anima.IntegrationTests;

public sealed class CapturingOtpSender : IOtpSender
{
    private readonly ConcurrentDictionary<string, string> _codes = new();
    public Task SendAsync(string phoneE164, string code, CancellationToken ct) { _codes[phoneE164] = code; return Task.CompletedTask; }
    public string CodeFor(string phone) => _codes[phone];
}

/// <summary>
/// Chạy API thật trên PostgreSQL thật (không giả lập DB). Mỗi lớp test có một database riêng, tạo mới với migration và seed.
/// Chuỗi kết nối quản trị lấy từ ANIMA_TEST_PG (mặc định: PostgreSQL do infra/dev-postgres.sh khởi động).
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _admin = Environment.GetEnvironmentVariable("ANIMA_TEST_PG") ?? "Host=127.0.0.1;Port=55432;Username=postgres;Database=postgres";
    private readonly string _dbName = "anima_test_" + Guid.NewGuid().ToString("N")[..12];
    public CapturingOtpSender Otp { get; } = new();
    public NpgsqlDataSource Db { get; private set; } = null!;
    public string ConnectionString => new NpgsqlConnectionStringBuilder(_admin) { Database = _dbName }.ConnectionString;

    public async Task InitializeAsync()
    {
        await using var c = new NpgsqlConnection(_admin);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {_dbName}", c);
        await cmd.ExecuteNonQueryAsync();
        Db = NpgsqlDataSource.Create(ConnectionString);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Db.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var c = new NpgsqlConnection(_admin);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_dbName} WITH (FORCE)", c);
        await cmd.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseEnvironment("Testing");
        b.UseSetting("ConnectionStrings:Anima", ConnectionString);
        b.UseSetting("Seed:Enabled", "true");
        b.UseSetting("Dev:MockTopUp", "true");
        b.UseSetting("Identity:Pbkdf2Iterations", "1000");
        b.UseSetting("Admin:Pbkdf2Iterations", "1000");
        b.UseSetting("Admin:SeedDemoUsers", "true");
        b.ConfigureServices(s => { s.RemoveAll<IOtpSender>(); s.AddSingleton<IOtpSender>(Otp); });
    }

    public async Task<int> Sql(string sql, params (string, object?)[] p)
    {
        await using var cmd = Db.CreateCommand(sql);
        foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<T?> Scalar<T>(string sql, params (string, object?)[] p)
    {
        await using var cmd = Db.CreateCommand(sql);
        foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        var o = await cmd.ExecuteScalarAsync();
        return o is null or DBNull ? default : (T)o;
    }

    public Task<Player> NewPlayer(string country = "VN", string birthDate = "1990-01-01", string tz = "Asia/Ho_Chi_Minh", string locale = "vi") =>
        Player.RegisterAsync(this, country, birthDate, tz, locale);
}

public readonly record struct Resp(HttpStatusCode Status, JsonElement Body)
{
    public string? Code => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
    public JsonElement this[string name] => Body.GetProperty(name);
    public bool Ok => (int)Status is >= 200 and < 300;
}

public sealed class Player
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ApiFixture _fx;
    public HttpClient Http { get; }
    public Guid Id { get; private set; }
    public string Email { get; }
    public const string Password = "correct horse";

    private Player(ApiFixture fx) { _fx = fx; Http = fx.CreateClient(); Email = $"p{Guid.NewGuid():N}@test.local"; }

    public static async Task<Player> RegisterAsync(ApiFixture fx, string country, string birth, string tz, string locale)
    {
        var p = new Player(fx);
        var r = await p.Anonymous(HttpMethod.Post, "/v1/accounts", new { email = p.Email, password = Password, birthDate = birth, country, locale, timezone = tz });
        if (!r.Ok) throw new InvalidOperationException($"Register failed: {r.Status} {r.Body}");
        p.Id = r["account"].GetProperty("id").GetGuid();
        p.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", r["accessToken"].GetString());
        return p;
    }

    public async Task<Resp> Anonymous(HttpMethod m, string url, object? body = null, string? idem = null)
    {
        using var req = new HttpRequestMessage(m, url);
        if (body is not null) req.Content = JsonContent.Create(body, options: Json);
        if (idem is { Length: > 0 }) req.Headers.Add("Idempotency-Key", idem);
        using var res = await Http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        return new Resp(res.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    /// <summary>POST luôn gửi Idempotency-Key mới trừ khi truyền chuỗi rỗng.</summary>
    public Task<Resp> Post(string url, object? body = null, string? idem = null) => Anonymous(HttpMethod.Post, url, body ?? new { }, idem ?? Guid.NewGuid().ToString("N"));
    public Task<Resp> Put(string url, object body) => Anonymous(HttpMethod.Put, url, body);
    public Task<Resp> Get(string url) => Anonymous(HttpMethod.Get, url);

    /// <summary>Client không đăng nhập.</summary>
    public sealed class Anon(ApiFixture fx)
    {
        private readonly HttpClient _http = fx.CreateClient();
        public async Task<Resp> Post(string url, object body)
        {
            using var res = await _http.PostAsJsonAsync(url, body, Json);
            var text = await res.Content.ReadAsStringAsync();
            return new Resp(res.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
        }
        public async Task<Resp> Get(string url)
        {
            using var res = await _http.GetAsync(url);
            var text = await res.Content.ReadAsStringAsync();
            return new Resp(res.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
        }
    }

    public async Task<Resp> Login(string password = Password) => await Anonymous(HttpMethod.Post, "/v1/auth/login", new { email = Email, password });

    // ---- Ví ----
    public async Task<(long Gem, long Coin)> Balances() { var r = await Get("/v1/wallet"); return (r["gem"].GetInt64(), r["coin"].GetInt64()); }
    public async Task TopUp(long gem) { var r = await Post("/v1/dev/topup", new { gem }); Assert.True(r.Ok, r.Body.ToString()); }
    /// <summary>Có Coin dồi dào bằng cách nạp Gem rồi đổi (không có đường nào khác trên web ở R1).</summary>
    public async Task GetCoin(long coin)
    {
        var gem = (coin + 8) / 9; await TopUp(gem);
        var r = await Post("/v1/wallet/convert", new { direction = "GEM_TO_COIN", amount = gem }); Assert.True(r.Ok, r.Body.ToString());
    }
    public async Task VerifyPhone()
    {
        var phone = "+849" + Random.Shared.NextInt64(10_000_000, 99_999_999);
        var s = await Post("/v1/me/phone/otp", new { phone }); Assert.Equal(HttpStatusCode.Accepted, s.Status);
        var v = await Post("/v1/me/phone/verify", new { code = _fx.Otp.CodeFor(phone) }); Assert.True(v.Ok, v.Body.ToString());
    }

    // ---- Pack ----
    public async Task<Guid[]> Buy(int quantity, string currency = "GEM")
    {
        var r = await Post("/v1/packs/awakening-standard/purchase", new { currency, quantity });
        Assert.True(r.Ok, r.Body.ToString());
        return r["packInstanceIds"].EnumerateArray().Select(x => x.GetGuid()).ToArray();
    }
    public Task<Resp> Open(Guid packInstanceId) => Post($"/v1/pack-instances/{packInstanceId}/open");
    public async Task<List<JsonElement>> BuyAndOpen(int packs)
    {
        await TopUp(100L * packs);
        var cards = new List<JsonElement>();
        foreach (var id in await Buy(packs)) { var o = await Open(id); Assert.True(o.Ok, o.Body.ToString()); cards.AddRange(o["cards"].EnumerateArray()); }
        return cards;
    }
    public async Task<List<JsonElement>> Collection() => (await Get("/v1/collection"))["items"].EnumerateArray().ToList();
    public async Task<Guid> OpenWelcomePackAsync() { var l = await Get("/v1/me/packs"); var id = l.Body.EnumerateArray().First(x => x.GetProperty("kind").GetString() == "welcome").GetProperty("id").GetGuid(); Assert.True((await Open(id)).Ok); return id; }
}

/// <summary>Client quản trị đã đăng nhập (tài khoản mẫu theo vai trò do AdminBootstrap tạo khi Admin:SeedDemoUsers).</summary>
public sealed class AdminClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public HttpClient Http { get; }
    public string Email { get; private set; } = "";
    private AdminClient(ApiFixture fx) { Http = fx.CreateClient(); }
    public const string DemoPassword = "admin-demo-pass";

    public static async Task<AdminClient> LoginAsync(ApiFixture fx, string role, string? password = null)
    {
        var c = new AdminClient(fx) { Email = $"{role}@anima.local" };
        var r = await c.Anonymous(HttpMethod.Post, "/admin/v1/auth/login", new { email = c.Email, password = password ?? DemoPassword });
        if (!r.Ok) throw new InvalidOperationException($"Admin login failed for {role}: {r.Status} {r.Body}");
        c.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", r["accessToken"].GetString());
        return c;
    }

    public async Task<Resp> Anonymous(HttpMethod m, string url, object? body = null)
    {
        using var req = new HttpRequestMessage(m, url);
        if (body is not null) req.Content = JsonContent.Create(body, options: Json);
        using var res = await Http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        return new Resp(res.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }
    public Task<Resp> Get(string url) => Anonymous(HttpMethod.Get, url);
    public Task<Resp> Post(string url, object? body = null) => Anonymous(HttpMethod.Post, url, body ?? new { });
    public Task<Resp> Put(string url, object body) => Anonymous(HttpMethod.Put, url, body);
    public Task<Resp> Delete(string url) => Anonymous(HttpMethod.Delete, url);

    /// <summary>Đăng nhập bằng email/mật khẩu bất kỳ (thử khóa đăng nhập, tài khoản admin mới tạo).</summary>
    public sealed class Raw(ApiFixture fx)
    {
        private readonly AdminClient _c = new(fx);
        public Task<Resp> Attempt(string email, string password) => _c.Anonymous(HttpMethod.Post, "/admin/v1/auth/login", new { email, password });
        public async Task<AdminClient> LoginAsync(string email, string password)
        {
            var r = await Attempt(email, password);
            Assert.True(r.Ok, r.Body.ToString());
            var c = new AdminClient(fx) { Email = email };
            c.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", r["accessToken"].GetString());
            return c;
        }
    }

    public async Task<List<JsonElement>> Audit(string? action = null, int limit = 200)
        => (await Get($"/admin/v1/audit?limit={limit}" + (action is null ? "" : $"&action={action}"))).Body.EnumerateArray().ToList();
}
