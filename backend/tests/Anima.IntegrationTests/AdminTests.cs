using System.Net;
using System.Text.Json;

namespace Anima.IntegrationTests;

public sealed class AdminTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private const string Cs = "cs_agent", Content = "content_manager", Economy = "economy_manager", Fraud = "fraud_analyst", Finance = "finance_viewer", Super = "super_admin";

    private static object Odds(int common, int uncommon, int rare, int epic, int legendary, int secret) => new
    {
        entries = new[] { new { rarity = "common", ppm = common }, new { rarity = "uncommon", ppm = uncommon }, new { rarity = "rare", ppm = rare }, new { rarity = "epic", ppm = epic }, new { rarity = "legendary", ppm = legendary }, new { rarity = "secret", ppm = secret } },
    };
    // Tổng đúng 1,000,000 ppm: Legendary 3% thay vì 4%.
    private static object ValidOdds() => Odds(450_000, 250_000, 180_000, 70_000, 30_000, 20_000);

    // ---------- Đăng nhập và cô lập token ----------
    [Fact(DisplayName = "Đăng nhập quản trị: đúng mật khẩu nhận token và vai trò; sai mật khẩu bị từ chối")]
    public async Task AdminLogin()
    {
        var a = await AdminClient.LoginAsync(fx, Super);
        var me = await a.Get("/admin/v1/me");
        Assert.Equal(Super, me["role"].GetString());
        var anon = await AdminClient.LoginAsync(fx, Super);
        var bad = await anon.Anonymous(HttpMethod.Post, "/admin/v1/auth/login", new { email = $"{Super}@anima.local", password = "wrong" });
        Assert.Equal("INVALID_CREDENTIALS", bad.Code);
    }

    [Fact(DisplayName = "Cô lập: token người chơi không dùng được ở /admin và token admin không dùng được ở API người chơi")]
    public async Task TokensAreIsolated()
    {
        var player = await fx.NewPlayer(); var admin = await AdminClient.LoginAsync(fx, Super);
        using var asPlayer = fx.CreateClient(); asPlayer.DefaultRequestHeaders.Authorization = player.Http.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await asPlayer.GetAsync("/admin/v1/me")).StatusCode);
        using var adminOnPlayerApi = fx.CreateClient(); adminOnPlayerApi.DefaultRequestHeaders.Authorization = admin.Http.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await adminOnPlayerApi.GetAsync("/v1/wallet")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.CreateClient().GetAsync("/admin/v1/me")).StatusCode);
    }

    [Fact(DisplayName = "Quản lý admin: Super tạo admin mới; mật khẩu ngắn bị từ chối; không tự đổi vai trò; khóa admin thì token mất hiệu lực ngay; sai 5 lần thì bị khóa 15 phút")]
    public async Task AdminManagement()
    {
        var sa = await AdminClient.LoginAsync(fx, Super);
        var email = $"new{Guid.NewGuid():N}@anima.local";
        Assert.Equal("VALIDATION_FAILED", (await sa.Post("/admin/v1/admins", new { email, password = "short", role = Cs })).Code);
        var created = await sa.Post("/admin/v1/admins", new { email, password = "a-long-enough-password", role = Cs });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("EMAIL_ALREADY_USED", (await sa.Post("/admin/v1/admins", new { email, password = "a-long-enough-password", role = Cs })).Code);

        var myId = (await sa.Get("/admin/v1/me"))["id"].GetGuid();
        Assert.Equal("SELF_ROLE_CHANGE_FORBIDDEN", (await sa.Put($"/admin/v1/admins/{myId}", new { role = Economy })).Code);

        var newAdmin = await new AdminClient.Raw(fx).LoginAsync(email, "a-long-enough-password");
        Assert.True((await newAdmin.Get("/admin/v1/me")).Ok);
        Assert.True((await sa.Put($"/admin/v1/admins/{created["id"].GetGuid()}", new { active = false })).Ok);
        Assert.Equal(HttpStatusCode.Unauthorized, (await newAdmin.Get("/admin/v1/me")).Status);       // token cũ vô hiệu ngay

        var victim = $"lock{Guid.NewGuid():N}@anima.local";
        await sa.Post("/admin/v1/admins", new { email = victim, password = "a-long-enough-password", role = Cs });
        var raw = new AdminClient.Raw(fx);
        for (var i = 0; i < 5; i++) Assert.Equal("INVALID_CREDENTIALS", (await raw.Attempt(victim, "wrong password")).Code);
        var locked = await raw.Attempt(victim, "a-long-enough-password");
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.Status);
        Assert.Equal("LOGIN_LOCKED", locked.Code);
    }

    // ---------- Người chơi: che dữ liệu, audit, khóa/ban ----------
    private async Task<(Player P, string Phone)> VerifiedPlayerWith(string phone)
    {
        var p = await fx.NewPlayer();
        await p.Post("/v1/me/phone/otp", new { phone });
        Assert.True((await p.Post("/v1/me/phone/verify", new { code = fx.Otp.CodeFor(phone) })).Ok);
        return (p, phone);
    }

    [Fact(DisplayName = "SC-ADM-07: CS Agent chỉ thấy email và SĐT đã che (090****123)")]
    public async Task CsSeesMaskedPii()
    {
        var (p, _) = await VerifiedPlayerWith("+84901234123");
        var cs = await AdminClient.LoginAsync(fx, Cs);
        var r = await cs.Get($"/admin/v1/accounts/{p.Id}");
        Assert.Equal("090****123", r["account"].GetProperty("phoneMasked").GetString());
        var masked = r["account"].GetProperty("emailMasked").GetString()!;
        Assert.Matches(@"^p\*\*\*@test\.local$", masked);
        Assert.DoesNotContain(p.Email, r.Body.ToString(), StringComparison.OrdinalIgnoreCase);
        // Tìm theo email chính xác và theo SĐT.
        Assert.Equal(p.Id, (await cs.Get($"/admin/v1/accounts?q={Uri.EscapeDataString(p.Email)}")).Body[0].GetProperty("id").GetGuid());
        Assert.Equal(p.Id, (await cs.Get($"/admin/v1/accounts?q={Uri.EscapeDataString("+84901234123")}")).Body[0].GetProperty("id").GetGuid());
        Assert.Equal("FORBIDDEN", (await cs.Post($"/admin/v1/accounts/{p.Id}/reveal-pii")).Code);
    }

    [Fact(DisplayName = "SC-ADM-06: Fraud Analyst xem SĐT đầy đủ và việc xem được ghi vào audit log")]
    public async Task RevealPiiIsAudited()
    {
        var (p, phone) = await VerifiedPlayerWith("+84902222333");
        var fraud = await AdminClient.LoginAsync(fx, Fraud);
        var r = await fraud.Post($"/admin/v1/accounts/{p.Id}/reveal-pii");
        Assert.Equal(phone, r["phone"].GetString());
        Assert.Equal(p.Email, r["email"].GetString());
        var entry = (await fraud.Audit("PII_VIEW")).First(e => e.GetProperty("targetId").GetString() == p.Id.ToString());
        Assert.Equal(fraud.Email, entry.GetProperty("actorEmail").GetString());
        Assert.Equal(Fraud, entry.GetProperty("actorRole").GetString());
    }

    [Fact(DisplayName = "SC-ADM-04: ban ghi audit đủ người thực hiện, đối tượng, trạng thái trước/sau, lý do; token người chơi mất hiệu lực ngay")]
    public async Task BanIsAuditedAndImmediate()
    {
        var (p, _) = await VerifiedPlayerWith("+84903333444");
        var fraud = await AdminClient.LoginAsync(fx, Fraud);
        Assert.Equal("REASON_REQUIRED", (await fraud.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "ban" })).Code);
        var r = await fraud.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "ban", reason = "bot farm ads" });
        Assert.Equal("Verified", r["before"].GetString()); Assert.Equal("Banned", r["after"].GetString());

        var e = (await fraud.Audit("BAN")).First(x => x.GetProperty("targetId").GetString() == p.Id.ToString());
        Assert.Equal(fraud.Email, e.GetProperty("actorEmail").GetString());
        Assert.Equal("Verified", e.GetProperty("before").GetProperty("status").GetString());
        Assert.Equal("Banned", e.GetProperty("after").GetProperty("status").GetString());
        Assert.Equal("bot farm ads", e.GetProperty("reason").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await p.Get("/v1/wallet")).Status);          // token đang dùng bị từ chối ngay
        Assert.Equal("INVALID_CREDENTIALS", (await p.Login()).Code);                            // không đăng nhập lại được
    }

    [Fact(DisplayName = "Khóa tài khoản (Restricted): không mua pack được; gỡ khóa thì mua lại được; chỉ Super Admin gỡ ban")]
    public async Task RestrictAndUnban()
    {
        var p = await fx.NewPlayer(); await p.TopUp(200);
        var fraud = await AdminClient.LoginAsync(fx, Fraud); var sa = await AdminClient.LoginAsync(fx, Super);
        Assert.True((await fraud.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "restrict", reason = "chargeback pattern" })).Ok);
        Assert.Equal("ACCOUNT_RESTRICTED", (await p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 })).Code);
        Assert.Equal("INVALID_STATE", (await fraud.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "restrict", reason = "again" })).Code);
        Assert.True((await fraud.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "unrestrict" })).Ok);
        Assert.True((await p.Post("/v1/packs/awakening-standard/purchase", new { currency = "GEM", quantity = 1 })).Ok);

        await fraud.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "ban", reason = "fraud" });
        Assert.Equal("FORBIDDEN", (await fraud.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "unban" })).Code);   // Fraud không gỡ ban được
        var unban = await sa.Post($"/admin/v1/accounts/{p.Id}/status", new { action = "unban" });
        Assert.Equal("Banned", unban["before"].GetString()); Assert.Equal("Unverified", unban["after"].GetString());
        Assert.True((await p.Login()).Ok);
    }

    [Fact(DisplayName = "SC-ADM-10: admin không cộng được Gem; yêu cầu bị ghi audit DENIED")]
    public async Task GemGrantForbidden()
    {
        var p = await fx.NewPlayer(); var sa = await AdminClient.LoginAsync(fx, Super);
        var r = await sa.Post($"/admin/v1/accounts/{p.Id}/grant-gem", new { gem = 1000 });
        Assert.Equal(HttpStatusCode.Forbidden, r.Status); Assert.Equal("GEM_GRANT_FORBIDDEN", r.Code);
        Assert.Equal(0, (await p.Balances()).Gem);
        Assert.Contains((await sa.Audit("GEM_GRANT")), e => e.GetProperty("targetId").GetString() == p.Id.ToString() && e.GetProperty("outcome").GetString() == "DENIED");
    }

    // ---------- Audit ----------
    [Fact(DisplayName = "SC-ADM-05: không ai sửa hay xóa được audit log (API trả AUDIT_IMMUTABLE, database chặn)")]
    public async Task AuditIsImmutable()
    {
        var sa = await AdminClient.LoginAsync(fx, Super);
        await sa.Post($"/admin/v1/accounts/{Guid.NewGuid()}/grant-gem", new { gem = 1 });
        var id = (await sa.Audit()).First().GetProperty("id").GetInt64();
        var r = await sa.Delete($"/admin/v1/audit/{id}");
        Assert.Equal("AUDIT_IMMUTABLE", r.Code);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fx.Sql("DELETE FROM admin.audit_log WHERE id=@i", ("i", id)));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fx.Sql("UPDATE admin.audit_log SET reason='x' WHERE id=@i", ("i", id)));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fx.Sql("TRUNCATE admin.audit_log"));
        Assert.Contains(await sa.Audit("AUDIT_DELETE"), e => e.GetProperty("outcome").GetString() == "DENIED");
    }

    [Fact(DisplayName = "Xem audit log: chỉ Fraud Analyst và Super Admin; phân trang theo id")]
    public async Task AuditViewPermissionsAndPaging()
    {
        Assert.Equal("FORBIDDEN", (await (await AdminClient.LoginAsync(fx, Cs)).Get("/admin/v1/audit")).Code);
        Assert.Equal("FORBIDDEN", (await (await AdminClient.LoginAsync(fx, Finance)).Get("/admin/v1/audit")).Code);
        var fraud = await AdminClient.LoginAsync(fx, Fraud);
        var page1 = (await fraud.Get("/admin/v1/audit?limit=2")).Body.EnumerateArray().ToList();
        Assert.True(page1.Count <= 2);
        if (page1.Count == 2)
        {
            var older = (await fraud.Get($"/admin/v1/audit?limit=50&before={page1[1].GetProperty("id").GetInt64()}")).Body.EnumerateArray();
            Assert.All(older, e => Assert.True(e.GetProperty("id").GetInt64() < page1[1].GetProperty("id").GetInt64()));
        }
    }

    // ---------- Tỷ lệ rơi: maker-checker ----------
    [Theory(DisplayName = "SC-ADM-02: tổng tỷ lệ phải đúng 100.00%")]
    [InlineData(999_900, false)]
    [InlineData(1_000_000, true)]
    [InlineData(1_000_100, false)]
    public async Task OddsSumMustBeExact(int total, bool ok)
    {
        var eco = await AdminClient.LoginAsync(fx, Economy);
        var r = await eco.Post("/admin/v1/packs/awakening-standard/odds", Odds(total - 550_000, 250_000, 180_000, 70_000, 30_000, 20_000));
        if (ok) Assert.Equal(HttpStatusCode.Created, r.Status); else Assert.Equal("DROP_RATE_SUM_INVALID", r.Code);
    }

    [Fact(DisplayName = "Tỷ lệ phải đủ 6 rarity, mỗi rarity một lần")]
    public async Task OddsMustListEveryRarityOnce()
    {
        var eco = await AdminClient.LoginAsync(fx, Economy);
        Assert.Equal("VALIDATION_FAILED", (await eco.Post("/admin/v1/packs/awakening-standard/odds", new { entries = new[] { new { rarity = "common", ppm = 1_000_000 } } })).Code);
        Assert.Equal("VALIDATION_FAILED", (await eco.Post("/admin/v1/packs/awakening-standard/odds", new { entries = new[] { new { rarity = "common", ppm = 500_000 }, new { rarity = "common", ppm = 500_000 }, new { rarity = "rare", ppm = 0 }, new { rarity = "epic", ppm = 0 }, new { rarity = "legendary", ppm = 0 }, new { rarity = "secret", ppm = 0 } } })).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await eco.Post("/admin/v1/packs/welcome/odds", ValidOdds())).Status);
    }

    [Fact(DisplayName = "SC-ADM-01/08: Economy Manager tạo bản nháp, không tự duyệt được; Super Admin duyệt thì có hiệu lực và audit ghi người tạo, người duyệt")]
    public async Task OddsMakerChecker()
    {
        var eco = await AdminClient.LoginAsync(fx, Economy); var sa = await AdminClient.LoginAsync(fx, Super);
        var publicBefore = (await new Player.Anon(fx).Get("/v1/packs/awakening-standard"))["odds"].GetProperty("version").GetInt32();
        var draft = await eco.Post("/admin/v1/packs/awakening-standard/odds", ValidOdds());
        var v = draft["version"].GetInt32();
        Assert.Equal("draft", draft["status"].GetString());
        Assert.Equal(publicBefore, (await new Player.Anon(fx).Get("/v1/packs/awakening-standard"))["odds"].GetProperty("version").GetInt32());   // bản nháp chưa ảnh hưởng người chơi

        var self = await eco.Post($"/admin/v1/packs/awakening-standard/odds/{v}/approve");
        Assert.Equal(HttpStatusCode.Forbidden, self.Status); Assert.Equal("SELF_APPROVAL_FORBIDDEN", self.Code);

        var ok = await sa.Post($"/admin/v1/packs/awakening-standard/odds/{v}/approve");
        Assert.Equal("approved", ok["status"].GetString());
        Assert.Equal(eco.Email, ok["createdBy"].GetString()); Assert.Equal(sa.Email, ok["approvedBy"].GetString());
        Assert.Equal(v, (await new Player.Anon(fx).Get("/v1/packs/awakening-standard"))["odds"].GetProperty("version").GetInt32());   // người chơi thấy tỷ lệ mới
        var entry = (await sa.Audit("ODDS_APPROVE")).First();
        Assert.Equal(eco.Email, entry.GetProperty("after").GetProperty("createdBy").GetString());
        Assert.Equal(sa.Email, entry.GetProperty("after").GetProperty("approvedBy").GetString());

        var list = await sa.Get("/admin/v1/packs/awakening-standard/odds");
        Assert.Single(list.Body.EnumerateArray(), x => x.GetProperty("inEffect").GetBoolean());
    }

    [Fact(DisplayName = "SC-ADM-09 / BR-ADM-03: sửa version đã duyệt hoặc đang hiệu lực bị từ chối VERSION_LOCKED; bản nháp sửa được")]
    public async Task ApprovedOddsAreLocked()
    {
        var eco = await AdminClient.LoginAsync(fx, Economy);
        var current = (await eco.Get("/admin/v1/packs/awakening-standard/odds")).Body.EnumerateArray().First(x => x.GetProperty("inEffect").GetBoolean());
        var locked = await eco.Put($"/admin/v1/packs/awakening-standard/odds/{current.GetProperty("version").GetInt32()}", ValidOdds());
        Assert.Equal(HttpStatusCode.Conflict, locked.Status); Assert.Equal("VERSION_LOCKED", locked.Code);

        var draft = await eco.Post("/admin/v1/packs/awakening-standard/odds", ValidOdds());
        var edited = await eco.Put($"/admin/v1/packs/awakening-standard/odds/{draft["version"].GetInt32()}", Odds(500_000, 250_000, 150_000, 60_000, 30_000, 10_000));
        Assert.True(edited.Ok, edited.Body.ToString());
        Assert.Equal(500_000, edited["entries"][0].GetProperty("ppm").GetInt32());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fx.Sql(@"UPDATE catalog.odds_version SET entries='[]'::jsonb WHERE id=@i", ("i", current.GetProperty("id").GetInt32())));
    }

    [Fact(DisplayName = "Pack đã mua giữ tỷ lệ của version lúc mua dù sau đó đổi tỷ lệ (BR-PACK-04)")]
    public async Task PurchasedPackKeepsOddsSnapshot()
    {
        var p = await fx.NewPlayer(); await p.TopUp(100);
        var pack = (await p.Buy(1))[0];
        var oddsId = await fx.Scalar<int>("SELECT odds_version_id FROM gacha.pack_instance WHERE id=@i", ("i", pack));
        var eco = await AdminClient.LoginAsync(fx, Economy); var sa = await AdminClient.LoginAsync(fx, Super);
        var v = (await eco.Post("/admin/v1/packs/awakening-standard/odds", Odds(900_000, 50_000, 30_000, 10_000, 9_000, 1_000)))["version"].GetInt32();
        await sa.Post($"/admin/v1/packs/awakening-standard/odds/{v}/approve");
        Assert.True((await p.Open(pack)).Ok);
        Assert.Equal(oddsId, await fx.Scalar<int>("SELECT odds_version_id FROM gacha.pack_instance WHERE id=@i", ("i", pack)));
        var newest = await fx.Scalar<int>("SELECT max(id) FROM catalog.odds_version WHERE pack_code='awakening-standard'");
        Assert.NotEqual(oddsId, newest);
    }

    [Fact(DisplayName = "BR-SUP-04: pack ngừng bán vì hết bản được mở bán lại khi version tỷ lệ mới được duyệt")]
    public async Task ApprovalReopensExhaustedPack()
    {
        await fx.Sql("UPDATE catalog.pack_definition SET on_sale=false, off_sale_reason='RARITY_EXHAUSTED:legendary' WHERE code='awakening-standard'");
        var eco = await AdminClient.LoginAsync(fx, Economy); var sa = await AdminClient.LoginAsync(fx, Super);
        var v = (await eco.Post("/admin/v1/packs/awakening-standard/odds", Odds(500_000, 300_000, 150_000, 50_000, 0, 0)))["version"].GetInt32();
        await sa.Post($"/admin/v1/packs/awakening-standard/odds/{v}/approve");
        Assert.True((await new Player.Anon(fx).Get("/v1/packs/awakening-standard"))["pack"].GetProperty("onSale").GetBoolean());
    }

    // ---------- Tham số kinh tế ----------
    [Fact(DisplayName = "BR-ECO-03 + BR-ADM-02: đề xuất đổi phí rèn → người khác duyệt → version mới có hiệu lực; không tự duyệt")]
    public async Task EconomyChangeFlow()
    {
        var eco = await AdminClient.LoginAsync(fx, Economy); var sa = await AdminClient.LoginAsync(fx, Super);
        Assert.Equal("VALIDATION_FAILED", (await eco.Post("/admin/v1/economy/changes", new { key = "no_such_key", value = 1 })).Code);
        Assert.Equal("VALIDATION_FAILED", (await eco.Post("/admin/v1/economy/changes", new { key = "forge_fee_coin", value = 0 })).Code);

        var c = await eco.Post("/admin/v1/economy/changes", new { key = "forge_fee_coin", value = 60, note = "tăng phí để giảm lạm phát" });
        var id = c["id"].GetGuid();
        Assert.Equal(50, (await new Player.Anon(fx).Get("/v1/economy"))["forge_fee_coin"].GetInt64());          // chưa duyệt thì chưa đổi
        Assert.Equal("SELF_APPROVAL_FORBIDDEN", (await eco.Post($"/admin/v1/economy/changes/{id}/approve")).Code);

        var done = await sa.Post($"/admin/v1/economy/changes/{id}/approve");
        Assert.Equal("approved", done["status"].GetString());
        Assert.Equal(60, (await new Player.Anon(fx).Get("/v1/economy"))["forge_fee_coin"].GetInt64());
        Assert.Equal("INVALID_STATE", (await sa.Post($"/admin/v1/economy/changes/{id}/approve")).Code);

        var history = (await sa.Get("/admin/v1/economy"))["parameters"].EnumerateArray().Where(x => x.GetProperty("key").GetString() == "forge_fee_coin").ToList();
        Assert.Equal([2, 1], history.Select(x => x.GetProperty("version").GetInt32()).ToArray());              // version cũ còn nguyên
        Assert.Equal(eco.Email, history[0].GetProperty("createdBy").GetString()); Assert.Equal(sa.Email, history[0].GetProperty("approvedBy").GetString());

        // Phí mới áp dụng cho lần rèn kế tiếp của người chơi.
        var p = await fx.NewPlayer(); var cards = await p.BuyAndOpen(1);
        Assert.Equal(60, (await p.Get("/v1/forge/info"))["feeCoin"].GetInt64());
        _ = cards;

        var rej = await eco.Post("/admin/v1/economy/changes", new { key = "forge_fee_gem", value = 9 });
        Assert.True((await sa.Post($"/admin/v1/economy/changes/{rej["id"].GetGuid()}/reject")).Ok);
        Assert.Equal(5, (await new Player.Anon(fx).Get("/v1/economy"))["forge_fee_gem"].GetInt64());
    }

    // ---------- Bồi thường ----------
    [Fact(DisplayName = "SC-ADM-11: bồi thường không có ticket bị từ chối TICKET_REQUIRED")]
    public async Task CompensationNeedsTicket()
    {
        var p = await fx.NewPlayer(); var cs = await AdminClient.LoginAsync(fx, Cs);
        Assert.Equal("TICKET_REQUIRED", (await cs.Post("/admin/v1/compensations", new { accountId = p.Id, coin = 500, ticket = "  " })).Code);
        Assert.Equal("INVALID_AMOUNT", (await cs.Post("/admin/v1/compensations", new { accountId = p.Id, coin = 0, ticket = "T-1" })).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await cs.Post("/admin/v1/compensations", new { accountId = Guid.NewGuid(), coin = 5, ticket = "T-2" })).Status);
    }

    [Fact(DisplayName = "SC-ADM-12: CS Agent tạo, Fraud Analyst duyệt → người chơi nhận Coin; bút toán tham chiếu ticket; audit đủ người tạo và người duyệt")]
    public async Task CompensationHappyPath()
    {
        var p = await fx.NewPlayer(); var cs = await AdminClient.LoginAsync(fx, Cs); var fraud = await AdminClient.LoginAsync(fx, Fraud); var sa = await AdminClient.LoginAsync(fx, Super);
        var c = await cs.Post("/admin/v1/compensations", new { accountId = p.Id, coin = 500, ticket = "T-90", reason = "lỗi mất thẻ khi mở pack" });
        var id = c["id"].GetGuid();
        Assert.Equal("pending", c["status"].GetString());
        Assert.Equal(100, (await p.Balances()).Coin);                                                           // chưa duyệt thì chưa cộng

        Assert.Equal("FORBIDDEN", (await cs.Post($"/admin/v1/compensations/{id}/approve")).Code);               // người tạo (CS) không duyệt được
        Assert.Equal("FORBIDDEN", (await sa.Post($"/admin/v1/compensations/{id}/approve")).Code);               // Super Admin không nằm trong quyền duyệt (BRD 12.2)
        var done = await fraud.Post($"/admin/v1/compensations/{id}/approve");
        Assert.Equal("approved", done["status"].GetString());
        Assert.Equal(600, (await p.Balances()).Coin);

        var ledger = (await p.Get("/v1/wallet/ledger")).Body.EnumerateArray().First(e => e.GetProperty("reason").GetString() == "COMPENSATION");
        Assert.Equal(500, ledger.GetProperty("amount").GetInt64()); Assert.Equal("T-90", ledger.GetProperty("refId").GetString());
        var audit = (await fraud.Audit("COMP_APPROVE")).First(e => e.GetProperty("targetId").GetString() == p.Id.ToString());
        Assert.Equal("T-90", audit.GetProperty("after").GetProperty("ticket").GetString());
        Assert.Equal(cs.Email, audit.GetProperty("after").GetProperty("createdBy").GetString());
        Assert.Equal(fraud.Email, audit.GetProperty("after").GetProperty("approvedBy").GetString());

        Assert.Equal("INVALID_STATE", (await fraud.Post($"/admin/v1/compensations/{id}/approve")).Code);        // duyệt hai lần không cộng hai lần
        Assert.Equal(600, (await p.Balances()).Coin);
        Assert.Equal(0L, await fx.Scalar<long>("SELECT count(*) FROM wallet.balance_mismatch"));
    }

    [Fact(DisplayName = "Từ chối bồi thường: không cộng Coin")]
    public async Task CompensationRejected()
    {
        var p = await fx.NewPlayer(); var cs = await AdminClient.LoginAsync(fx, Cs); var fraud = await AdminClient.LoginAsync(fx, Fraud);
        var id = (await cs.Post("/admin/v1/compensations", new { accountId = p.Id, coin = 300, ticket = "T-91" }))["id"].GetGuid();
        Assert.Equal("rejected", (await fraud.Post($"/admin/v1/compensations/{id}/reject"))["status"].GetString());
        Assert.Equal(100, (await p.Balances()).Coin);
    }

    // ---------- Thẻ ----------
    [Fact(DisplayName = "SC-ADM-13: không xóa thẻ đã có bản (CARD_HAS_INSTANCES); SC-ADM-14: ngừng phát hành thì không cấp mới nhưng thẻ hiện có không đổi")]
    public async Task CardsCannotBeDeletedOnlyDiscontinued()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(1);
        var content = await AdminClient.LoginAsync(fx, Content);
        var issued = (await content.Get("/admin/v1/cards")).Body.EnumerateArray().First(c => c.GetProperty("issued").GetInt32() > 0);
        var id = issued.GetProperty("id").GetInt32();
        var del = await content.Delete($"/admin/v1/cards/{id}");
        Assert.Equal(HttpStatusCode.Conflict, del.Status); Assert.Equal("CARD_HAS_INSTANCES", del.Code);
        Assert.Equal("CARD_PUBLISHED_IMMUTABLE", (await content.Delete("/admin/v1/cards/100")).Code);   // thẻ 100 chưa có bản nhưng đã phát hành cũng không xóa được

        // Ngừng phát hành 38/39 thẻ Common: mọi Common cấp mới chỉ còn là thẻ cuối cùng.
        var commons = (await content.Get("/admin/v1/cards")).Body.EnumerateArray().Where(c => c.GetProperty("rarity").GetString() == "common").Select(c => c.GetProperty("id").GetInt32()).ToList();
        var keep = commons[0];
        foreach (var c in commons.Skip(1)) Assert.Equal(HttpStatusCode.NoContent, (await content.Post($"/admin/v1/cards/{c}/discontinue")).Status);
        var before = await fx.Scalar<int>("SELECT issued FROM catalog.edition WHERE card_definition_id=@i", ("i", commons[1]));
        var cards = await p.BuyAndOpen(6);
        Assert.All(cards.Where(c => c.GetProperty("rarity").GetString() == "common"), c => Assert.Equal(keep, c.GetProperty("card").GetProperty("id").GetInt32()));
        Assert.Equal(before, await fx.Scalar<int>("SELECT issued FROM catalog.edition WHERE card_definition_id=@i", ("i", commons[1])));
        Assert.Equal(HttpStatusCode.NotFound, (await content.Post("/admin/v1/cards/9999/discontinue")).Status);
        Assert.Contains(await (await AdminClient.LoginAsync(fx, Super)).Audit("CARD_DISCONTINUE"), e => e.GetProperty("targetId").GetString() == commons[1].ToString());
    }

    // ---------- Dashboard ----------
    [Fact(DisplayName = "Dashboard: Economy, Finance, Super xem được số liệu gom từ các module; CS Agent bị chặn")]
    public async Task DashboardMetrics()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(1);
        var finance = await AdminClient.LoginAsync(fx, Finance);
        var m = (await finance.Get("/admin/v1/dashboard"))["metrics"];
        Assert.True(m.GetProperty("accounts_total").GetInt64() >= 1);
        Assert.True(m.GetProperty("packs_opened").GetInt64() >= 1);
        Assert.True(m.GetProperty("cards_issued").GetInt64() >= 5);
        Assert.True(m.GetProperty("gem_dev_topup").GetInt64() >= 100);
        Assert.True(m.GetProperty("ledger_entries").GetInt64() > 0);
        Assert.Equal("FORBIDDEN", (await (await AdminClient.LoginAsync(fx, Cs)).Get("/admin/v1/dashboard")).Code);
    }

    // ---------- Ma trận phân quyền ----------
    public static IEnumerable<object[]> ForbiddenMatrix() =>
    [
        [Cs, "POST", "/admin/v1/packs/awakening-standard/odds", "odds"],
        [Cs, "POST", "/admin/v1/accounts/00000000-0000-0000-0000-000000000001/status", "ban"],
        [Content, "POST", "/admin/v1/economy/changes", "econ"],
        [Content, "POST", "/admin/v1/packs/awakening-standard/odds", "odds"],
        [Economy, "POST", "/admin/v1/cards/1/discontinue", "none"],
        [Economy, "POST", "/admin/v1/accounts/00000000-0000-0000-0000-000000000001/reveal-pii", "none"],
        [Finance, "POST", "/admin/v1/accounts/00000000-0000-0000-0000-000000000001/status", "restrict"],
        [Finance, "GET", "/admin/v1/accounts", "none"],
        [Fraud, "POST", "/admin/v1/accounts/00000000-0000-0000-0000-000000000001/status", "unban"],
        [Fraud, "POST", "/admin/v1/economy/changes", "econ"],
        [Super, "POST", "/admin/v1/cards/1/discontinue", "none"],
        [Super, "POST", "/admin/v1/packs/awakening-standard/odds", "odds"],
        [Super, "POST", "/admin/v1/compensations", "comp"],
        [Cs, "GET", "/admin/v1/admins", "none"],
    ];

    [Theory(DisplayName = "SC-ADM-03: hành động ngoài quyền bị từ chối FORBIDDEN và bị ghi vào audit log")]
    [MemberData(nameof(ForbiddenMatrix))]
    public async Task ForbiddenActionsAreRejectedAndAudited(string role, string method, string url, string body)
    {
        var a = await AdminClient.LoginAsync(fx, role); var sa = await AdminClient.LoginAsync(fx, Super);
        var before = (await sa.Audit(limit: 200)).Count(e => e.GetProperty("outcome").GetString() == "DENIED" && e.GetProperty("actorRole").GetString() == role);
        object? payload = body switch
        {
            "odds" => ValidOdds(),
            "econ" => new { key = "forge_fee_coin", value = 70 },
            "comp" => new { accountId = Guid.NewGuid(), coin = 5, ticket = "T-3" },
            "ban" => new { action = "ban", reason = "x" },
            "restrict" => new { action = "restrict", reason = "x" },
            "unban" => new { action = "unban" },
            _ => new { },
        };
        var r = method == "GET" ? await a.Get(url) : await a.Post(url, payload);
        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.Equal("FORBIDDEN", r.Code);
        var after = (await sa.Audit(limit: 200)).Count(e => e.GetProperty("outcome").GetString() == "DENIED" && e.GetProperty("actorRole").GetString() == role);
        Assert.True(after > before, "hành động bị từ chối phải được ghi audit");
    }
}
