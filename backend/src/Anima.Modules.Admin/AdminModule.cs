using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Anima.Catalog;
using Anima.Contracts;
using Anima.Economy;
using Anima.Identity.Contracts;
using Anima.SharedKernel;
using Anima.Wallet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Anima.Admin;

public static class Roles
{
    public const string Cs = "cs_agent", Content = "content_manager", Economy = "economy_manager", Fraud = "fraud_analyst", Finance = "finance_viewer", Super = "super_admin";
    public static readonly string[] All = [Cs, Content, Economy, Fraud, Finance, Super];
}

public sealed class AdminOptions
{
    public string JwtKey { get; set; } = "anima-dev-admin-signing-key-change-me-0123456789abcdef";
    public string JwtIssuer { get; set; } = "anima";
    public string JwtAudience { get; set; } = "anima-admin";
    public int TokenHours { get; set; } = 8;
    public int Pbkdf2Iterations { get; set; } = 600_000;
    /// <summary>Tạo 6 tài khoản mẫu (mỗi vai trò một) để thử phân quyền. Chỉ bật ở Development/Testing.</summary>
    public bool SeedDemoUsers { get; set; }
    public const string DemoPassword = "admin-demo-pass";
    /// <summary>Super Admin đầu tiên cho môi trường mới (chỉ tạo khi chưa có Super Admin nào).</summary>
    public string? BootstrapEmail { get; set; }
    public string? BootstrapPassword { get; set; }
}

public sealed record Actor(Guid Id, string Email, string Role);
public sealed record AdminUserDto(Guid Id, string Email, string Role, bool Active, DateTimeOffset CreatedAt);
public sealed record AdminAuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AdminUserDto Admin);
public sealed record AuditEntry(long Id, DateTimeOffset At, Guid? ActorId, string ActorEmail, string ActorRole, string Action, string? TargetType, string? TargetId, JsonElement? Before, JsonElement? After, string? Reason, string Outcome);
public sealed record CompensationDto(Guid Id, Guid AccountId, long Coin, string Ticket, string? Reason, string Status, string CreatedBy, DateTimeOffset CreatedAt, string? DecidedBy, DateTimeOffset? DecidedAt);

public sealed record LoginBody(string? Email, string? Password);
public sealed record CreateAdminBody(string? Email, string? Password, string? Role);
public sealed record UpdateAdminBody(string? Role, bool? Active);
public sealed record StatusBody(string? Action, string? Reason);
public sealed record GemBody(long Gem);
public sealed record OddsBody(List<RarityOdds>? Entries, DateTimeOffset? EffectiveFrom);
public sealed record ProposeBody(string? Key, long Value, DateTimeOffset? EffectiveFrom, string? Note);
public sealed record CompensationBody(Guid AccountId, long Coin, string? Ticket, string? Reason);

public static class ActorExtensions
{
    public static Actor Actor(this ClaimsPrincipal p) =>
        new(Guid.Parse(p.FindFirstValue("sub") ?? throw new DomainException(ErrorCodes.Unauthorized, "Missing admin", 401)), p.FindFirstValue("email") ?? "", p.FindFirstValue("role") ?? "");
}

public sealed class AdminModule : IModule
{
    public string Name => "admin";
    public System.Reflection.Assembly MigrationAssembly => typeof(AdminModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddOptions<AdminOptions>().Configure(o => c.GetSection("Admin").Bind(o));
        s.AddScoped<AuditLog>();
        s.AddScoped<AdminService>();
        s.AddScoped<IStartupTask, AdminBootstrap>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var anon = app.MapGroup("/admin/v1");
        anon.MapPost("/auth/login", async (LoginBody b, AdminService s, CancellationToken ct) => Results.Ok(await s.LoginAsync(b, ct)));

        var g = app.MapGroup("/admin/v1").RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Admin" });
        g.MapGet("/me", (ClaimsPrincipal u) => Results.Ok(u.Actor()));

        // ---- Quản lý tài khoản admin (chỉ Super Admin) ----
        g.MapGet("/admins", async (AdminService s, CancellationToken ct) => Results.Ok(await s.ListAdminsAsync(ct))).Guard("ADMIN_LIST", Roles.Super);
        g.MapPost("/admins", async (ClaimsPrincipal u, CreateAdminBody b, AdminService s, CancellationToken ct) => Results.Created("/admin/v1/admins", await s.CreateAdminAsync(u.Actor(), b, ct))).Guard("ADMIN_CREATE", Roles.Super);
        g.MapPut("/admins/{id:guid}", async (ClaimsPrincipal u, Guid id, UpdateAdminBody b, AdminService s, CancellationToken ct) => Results.Ok(await s.UpdateAdminAsync(u.Actor(), id, b, ct))).Guard("ADMIN_UPDATE", Roles.Super);

        // ---- Người chơi ----
        g.MapGet("/accounts", async (string? q, int? limit, AdminService s, CancellationToken ct) => Results.Ok(await s.SearchAccountsAsync(q, Math.Clamp(limit ?? 25, 1, 100), ct))).Guard("ACCOUNT_SEARCH", Roles.Cs, Roles.Fraud, Roles.Super);
        g.MapGet("/accounts/{id:guid}", async (Guid id, AdminService s, CancellationToken ct) => Results.Ok(await s.AccountDetailAsync(id, ct))).Guard("ACCOUNT_VIEW", Roles.Cs, Roles.Fraud, Roles.Super);
        g.MapPost("/accounts/{id:guid}/reveal-pii", async (ClaimsPrincipal u, Guid id, AdminService s, CancellationToken ct) => Results.Ok(await s.RevealPiiAsync(u.Actor(), id, ct))).Guard("PII_VIEW", Roles.Fraud, Roles.Super);
        g.MapPost("/accounts/{id:guid}/status", async (HttpContext http, Guid id, StatusBody b, AdminService s, CancellationToken ct) =>
        {
            // Gỡ ban chỉ Super Admin; khóa/ban cho Fraud Analyst và Super Admin (BRD 12.2).
            var action = (b.Action ?? "").ToLowerInvariant();
            await s.RequireAsync(http, "ACCOUNT_" + action.ToUpperInvariant(), action == "unban" ? [Roles.Super] : [Roles.Fraud, Roles.Super]);
            return Results.Ok(await s.ChangeAccountStatusAsync(http.User.Actor(), id, action, b.Reason, ct));
        });
        // BR-ADM-04: admin không bao giờ cộng Gem trực tiếp (SC-ADM-10).
        g.MapPost("/accounts/{id:guid}/grant-gem", async (HttpContext http, Guid id, GemBody b, AdminService s, CancellationToken ct) =>
        {
            await s.DenyAsync(http.User.Actor(), "GEM_GRANT", "account", id.ToString(), $"gem={b.Gem}", ct);
            throw new DomainException(ErrorCodes.GemGrantForbidden, "Admins cannot add Gem. Compensate with Coin through a ticket instead", 403);
        });

        // ---- Tỷ lệ rơi ----
        g.MapGet("/packs", async (AdminService s, CancellationToken ct) => Results.Ok(await s.Catalog.ListPacksAsync(ct))).Guard("PACK_LIST", Roles.Economy, Roles.Finance, Roles.Super);
        g.MapGet("/packs/{code}/odds", async (string code, AdminService s, CancellationToken ct) => Results.Ok(await s.Catalog.ListOddsAsync(code, ct))).Guard("ODDS_LIST", Roles.Economy, Roles.Finance, Roles.Super);
        g.MapPost("/packs/{code}/odds", async (ClaimsPrincipal u, string code, OddsBody b, AdminService s, CancellationToken ct) => Results.Created($"/admin/v1/packs/{code}/odds", await s.CreateOddsDraftAsync(u.Actor(), code, b, ct))).Guard("ODDS_DRAFT", Roles.Economy);
        g.MapPut("/packs/{code}/odds/{version:int}", async (ClaimsPrincipal u, string code, int version, OddsBody b, AdminService s, CancellationToken ct) => Results.Ok(await s.UpdateOddsDraftAsync(u.Actor(), code, version, b, ct))).Guard("ODDS_UPDATE", Roles.Economy);
        g.MapPost("/packs/{code}/odds/{version:int}/approve", async (ClaimsPrincipal u, string code, int version, AdminService s, CancellationToken ct) => Results.Ok(await s.ApproveOddsAsync(u.Actor(), code, version, ct))).Guard("ODDS_APPROVE", Roles.Economy, Roles.Super);

        // ---- Tham số kinh tế ----
        g.MapGet("/economy", async (AdminService s, CancellationToken ct) => Results.Ok(new { parameters = await s.Economy.HistoryAsync(ct), changes = await s.Economy.ChangesAsync(null, ct) })).Guard("ECON_VIEW", Roles.Economy, Roles.Finance, Roles.Super);
        g.MapPost("/economy/changes", async (ClaimsPrincipal u, ProposeBody b, AdminService s, CancellationToken ct) => Results.Created("/admin/v1/economy", await s.ProposeEconomyAsync(u.Actor(), b, ct))).Guard("ECON_PROPOSE", Roles.Economy);
        g.MapPost("/economy/changes/{id:guid}/approve", async (ClaimsPrincipal u, Guid id, AdminService s, CancellationToken ct) => Results.Ok(await s.DecideEconomyAsync(u.Actor(), id, true, ct))).Guard("ECON_APPROVE", Roles.Economy, Roles.Super);
        g.MapPost("/economy/changes/{id:guid}/reject", async (ClaimsPrincipal u, Guid id, AdminService s, CancellationToken ct) => Results.Ok(await s.DecideEconomyAsync(u.Actor(), id, false, ct))).Guard("ECON_REJECT", Roles.Economy, Roles.Super);

        // ---- Thẻ ----
        g.MapGet("/cards", async (AdminService s, CancellationToken ct) => Results.Ok(await s.Catalog.ListCardsAsync(ct))).Guard("CARD_LIST", Roles.Content, Roles.Economy, Roles.Super);
        g.MapPost("/cards/{id:int}/discontinue", async (ClaimsPrincipal u, int id, AdminService s, CancellationToken ct) => { await s.DiscontinueCardAsync(u.Actor(), id, ct); return Results.NoContent(); }).Guard("CARD_DISCONTINUE", Roles.Content);
        g.MapDelete("/cards/{id:int}", async (ClaimsPrincipal u, int id, AdminService s, CancellationToken ct) => { await s.Catalog.DeleteCardAsync(id, ct); return Results.NoContent(); }).Guard("CARD_DELETE", Roles.Content);

        // ---- Bồi thường Coin ----
        g.MapGet("/compensations", async (AdminService s, CancellationToken ct) => Results.Ok(await s.ListCompensationsAsync(ct))).Guard("COMP_LIST", Roles.Cs, Roles.Fraud);
        g.MapPost("/compensations", async (ClaimsPrincipal u, CompensationBody b, AdminService s, CancellationToken ct) => Results.Created("/admin/v1/compensations", await s.CreateCompensationAsync(u.Actor(), b, ct))).Guard("COMP_CREATE", Roles.Cs);
        g.MapPost("/compensations/{id:guid}/approve", async (ClaimsPrincipal u, Guid id, AdminService s, CancellationToken ct) => Results.Ok(await s.DecideCompensationAsync(u.Actor(), id, true, ct))).Guard("COMP_APPROVE", Roles.Fraud);
        g.MapPost("/compensations/{id:guid}/reject", async (ClaimsPrincipal u, Guid id, AdminService s, CancellationToken ct) => Results.Ok(await s.DecideCompensationAsync(u.Actor(), id, false, ct))).Guard("COMP_REJECT", Roles.Fraud);

        // ---- Dashboard và audit ----
        g.MapGet("/dashboard", async (AdminService s, CancellationToken ct) => Results.Ok(await s.DashboardAsync(ct))).Guard("DASHBOARD", Roles.Economy, Roles.Finance, Roles.Super);
        g.MapGet("/audit", async (long? before, int? limit, string? action, AdminService s, CancellationToken ct) => Results.Ok(await s.Audit.ListAsync(before, Math.Clamp(limit ?? 50, 1, 200), action, ct))).Guard("AUDIT_VIEW", Roles.Fraud, Roles.Super);
        // SC-ADM-05: không ai xóa hay sửa được audit log.
        g.MapDelete("/audit/{id:long}", async (HttpContext http, long id, AdminService s, CancellationToken ct) =>
        {
            await s.DenyAsync(http.User.Actor(), "AUDIT_DELETE", "audit", id.ToString(), null, ct);
            throw new DomainException(ErrorCodes.AuditImmutable, "The audit log cannot be modified or deleted", 403);
        });
    }
}

public static class GuardExtensions
{
    /// <summary>
    /// Chỉ cho các vai trò liệt kê; vai trò khác nhận 403 FORBIDDEN và hành động bị ghi vào audit log (SC-ADM-03).
    /// Quyền kiểm tra ở backend, giao diện chỉ ẩn/hiện theo quyền.
    /// </summary>
    public static RouteHandlerBuilder Guard(this RouteHandlerBuilder b, string action, params string[] roles) =>
        b.AddEndpointFilter(async (ctx, next) =>
        {
            await ctx.HttpContext.RequestServices.GetRequiredService<AdminService>().RequireAsync(ctx.HttpContext, action, roles);
            return await next(ctx);
        });
}

public sealed class AuditLog(IUnitOfWork uow)
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    public Task WriteAsync(Actor a, string action, string? targetType, string? targetId, object? before, object? after, string? reason, CancellationToken ct, string outcome = "OK") =>
        uow.ExecAsync("INSERT INTO admin.audit_log(actor_id,actor_email,actor_role,action,target_type,target_id,before,after,reason,outcome) VALUES(@a,@e,@r,@act,@tt,@ti,@b::jsonb,@af::jsonb,@why,@o)", ct,
            ("a", a.Id), ("e", a.Email), ("r", a.Role), ("act", action), ("tt", targetType), ("ti", targetId),
            ("b", before is null ? null : JsonSerializer.Serialize(before, J)), ("af", after is null ? null : JsonSerializer.Serialize(after, J)), ("why", reason), ("o", outcome));

    public async Task<IReadOnlyList<AuditEntry>> ListAsync(long? before, int limit, string? action, CancellationToken ct) =>
        await uow.QueryAsync(@"SELECT id,at,actor_id,actor_email,actor_role,action,target_type,target_id,before::text,after::text,reason,outcome FROM admin.audit_log
            WHERE (@b::bigint IS NULL OR id < @b) AND (@a::text IS NULL OR action = @a) ORDER BY id DESC LIMIT @l",
            r => new AuditEntry(r.GetInt64(0), r.GetFieldValue<DateTimeOffset>(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : JsonDocument.Parse(r.GetString(8)).RootElement.Clone(), r.IsDBNull(9) ? null : JsonDocument.Parse(r.GetString(9)).RootElement.Clone(), r.IsDBNull(10) ? null : r.GetString(10), r.GetString(11)), ct, ("b", before), ("a", action), ("l", limit));
}

public sealed class AdminService(IUnitOfWork uow, IClock clock, Microsoft.Extensions.Options.IOptions<AdminOptions> opt, IIdentityAdminApi identity, ICatalogAdminApi catalog,
    IEconomyAdminApi economy, IWalletApi wallet, IServiceProvider sp, AuditLog audit)
{
    private readonly AdminOptions _o = opt.Value;
    internal ICatalogAdminApi Catalog => catalog;
    internal IEconomyAdminApi Economy => economy;
    internal AuditLog Audit => audit;

    // ---------- Phân quyền ----------
    public async Task RequireAsync(HttpContext http, string action, params string[] roles)
    {
        var a = http.User.Actor();
        if (Array.IndexOf(roles, a.Role) >= 0) return;
        await audit.WriteAsync(a, action, null, http.Request.Path, null, null, "role not allowed", http.RequestAborted, "DENIED");   // ghi ngoài transaction để không bị rollback
        throw new DomainException(ErrorCodes.Forbidden, "Your role cannot perform this action", 403);
    }

    internal Task DenyAsync(Actor a, string action, string targetType, string targetId, string? note, CancellationToken ct) =>
        audit.WriteAsync(a, action, targetType, targetId, null, null, note, ct, "DENIED");

    // ---------- Đăng nhập admin ----------
    private static AdminUserDto Dto(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetFieldValue<DateTimeOffset>(4));
    private const string UserCols = "id,email,role,active,created_at";

    public async Task<AdminAuthResponse> LoginAsync(LoginBody b, CancellationToken ct)
    {
        var email = (b.Email ?? "").Trim().ToLowerInvariant();
        var rows = await uow.QueryAsync("SELECT id,email,role,active,created_at,password_hash,failed_logins,locked_until FROM admin.admin_user WHERE email_norm=@e",
            r => (User: Dto(r), Hash: r.GetString(5), Failed: r.GetInt32(6), Locked: r.IsDBNull(7) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(7)), ct, ("e", email));
        if (rows.Count == 0) { PasswordHasher.Verify(b.Password ?? "", DummyHash); throw new DomainException(ErrorCodes.InvalidCredentials, "Invalid email or password", 401); }
        var x = rows[0];
        if (x.Locked > clock.UtcNow) throw new DomainException(ErrorCodes.LoginLocked, "Too many failed attempts. Try again later", 429);
        if (!x.User.Active || !PasswordHasher.Verify(b.Password ?? "", x.Hash))
        {
            var f = x.Failed + 1;
            await uow.ExecAsync("UPDATE admin.admin_user SET failed_logins=@f, locked_until=@l WHERE id=@i", ct, ("f", f >= 5 ? 0 : f), ("l", f >= 5 ? clock.UtcNow.AddMinutes(15) : null), ("i", x.User.Id));
            throw new DomainException(ErrorCodes.InvalidCredentials, "Invalid email or password", 401);
        }
        await uow.ExecAsync("UPDATE admin.admin_user SET failed_logins=0, locked_until=NULL WHERE id=@i", ct, ("i", x.User.Id));
        var exp = clock.UtcNow.AddHours(_o.TokenHours);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _o.JwtIssuer,
            Audience = _o.JwtAudience,
            Expires = exp.UtcDateTime,
            IssuedAt = clock.UtcNow.UtcDateTime,
            Claims = new Dictionary<string, object> { ["sub"] = x.User.Id.ToString(), ["email"] = x.User.Email, ["role"] = x.User.Role },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.JwtKey)), SecurityAlgorithms.HmacSha256),
        });
        return new AdminAuthResponse(token, exp, x.User);
    }
    private static readonly string DummyHash = PasswordHasher.Hash("dummy", 1000);

    /// <summary>Token còn hạn nhưng tài khoản admin đã bị vô hiệu hóa thì bị từ chối ở mọi yêu cầu.</summary>
    public async Task<bool> IsActiveAsync(Guid id, CancellationToken ct) => await uow.ScalarAsync<bool?>("SELECT active FROM admin.admin_user WHERE id=@i", ct, ("i", id)) == true;

    // ---------- Quản lý admin ----------
    public async Task<IReadOnlyList<AdminUserDto>> ListAdminsAsync(CancellationToken ct) => await uow.QueryAsync($"SELECT {UserCols} FROM admin.admin_user ORDER BY created_at", Dto, ct);

    public Task<AdminUserDto> CreateAdminAsync(Actor by, CreateAdminBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var email = (b.Email ?? "").Trim();
        if (!email.Contains('@') || (b.Password ?? "").Length < 12) throw DomainException.Validation("A valid email and a password of at least 12 characters are required");
        if (Array.IndexOf(Roles.All, b.Role) < 0) throw DomainException.Validation("Unknown role");
        var rows = await uow.QueryAsync($"INSERT INTO admin.admin_user(id,email,email_norm,password_hash,role,created_by) VALUES(@i,@e,@n,@p,@r,@c) ON CONFLICT (email_norm) DO NOTHING RETURNING {UserCols}", Dto, ct,
            ("i", Guid.NewGuid()), ("e", email), ("n", email.ToLowerInvariant()), ("p", PasswordHasher.Hash(b.Password!, _o.Pbkdf2Iterations)), ("r", b.Role), ("c", by.Id));
        if (rows.Count == 0) throw DomainException.Conflict(ErrorCodes.EmailAlreadyUsed, "This admin email already exists");
        await audit.WriteAsync(by, "ADMIN_CREATE", "admin", rows[0].Id.ToString(), null, new { email, role = b.Role }, null, ct);
        return rows[0];
    }, ct);

    public Task<AdminUserDto> UpdateAdminAsync(Actor by, Guid id, UpdateAdminBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        // Super Admin không được tự đổi vai trò hay tự khóa mình (SoD, BRD 12.2).
        if (id == by.Id) throw new DomainException(ErrorCodes.SelfRoleChangeForbidden, "You cannot change your own role or status", 403);
        if (b.Role is not null && Array.IndexOf(Roles.All, b.Role) < 0) throw DomainException.Validation("Unknown role");
        var before = (await uow.QueryAsync($"SELECT {UserCols} FROM admin.admin_user WHERE id=@i FOR UPDATE", Dto, ct, ("i", id))).FirstOrDefault() ?? throw DomainException.NotFound("Admin");
        var after = (await uow.QueryAsync($"UPDATE admin.admin_user SET role=COALESCE(@r,role), active=COALESCE(@a,active) WHERE id=@i RETURNING {UserCols}", Dto, ct, ("r", b.Role), ("a", b.Active), ("i", id)))[0];
        await audit.WriteAsync(by, "ADMIN_UPDATE", "admin", id.ToString(), new { before.Role, before.Active }, new { after.Role, after.Active }, null, ct);
        return after;
    }, ct);

    // ---------- Người chơi ----------
    public Task<IReadOnlyList<AdminAccountRow>> SearchAccountsAsync(string? q, int limit, CancellationToken ct) => identity.SearchAsync(q, limit, ct);

    public async Task<object> AccountDetailAsync(Guid id, CancellationToken ct)
    {
        var a = await identity.GetAsync(id, ct) ?? throw DomainException.NotFound("Account");
        return new { account = a, balances = await wallet.GetBalancesAsync(id, ct) };
    }

    public Task<PiiView> RevealPiiAsync(Actor by, Guid id, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var pii = await identity.RevealPiiAsync(id, ct);
        await audit.WriteAsync(by, "PII_VIEW", "account", id.ToString(), null, null, "full PII viewed", ct);       // SC-ADM-06
        return pii;
    }, ct);

    public Task<object> ChangeAccountStatusAsync(Actor by, Guid id, string action, string? reason, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        if (action is not ("restrict" or "unrestrict" or "ban" or "unban")) throw DomainException.Validation("action must be restrict, unrestrict, ban or unban");
        // Khóa và ban bắt buộc có lý do (US-10.1).
        if (action is "restrict" or "ban" && string.IsNullOrWhiteSpace(reason)) throw new DomainException(ErrorCodes.ReasonRequired, "A reason is required", 400);
        var (before, after) = await identity.ChangeStatusAsync(id, action, ct);
        await audit.WriteAsync(by, action.ToUpperInvariant(), "account", id.ToString(), new { status = before }, new { status = after }, reason, ct);   // SC-ADM-04
        return new { id, before, after };
    }, ct);

    // ---------- Tỷ lệ rơi (maker-checker) ----------
    public Task<OddsVersionAdmin> CreateOddsDraftAsync(Actor by, string code, OddsBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var v = await catalog.CreateDraftAsync(code, b.Entries ?? [], b.EffectiveFrom, by.Email, ct);
        await audit.WriteAsync(by, "ODDS_DRAFT", "pack", code, null, new { v.Version, v.Entries, v.EffectiveFrom }, null, ct);
        return v;
    }, ct);

    public Task<OddsVersionAdmin> UpdateOddsDraftAsync(Actor by, string code, int version, OddsBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var v = await catalog.UpdateDraftAsync(code, version, b.Entries ?? [], ct);
        await audit.WriteAsync(by, "ODDS_UPDATE", "pack", code, null, new { v.Version, v.Entries }, null, ct);
        return v;
    }, ct);

    public Task<OddsVersionAdmin> ApproveOddsAsync(Actor by, string code, int version, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var v = await catalog.ApproveAsync(code, version, by.Email, ct);
        // SC-ADM-08: audit ghi rõ người tạo và người duyệt.
        await audit.WriteAsync(by, "ODDS_APPROVE", "pack", code, new { status = "draft" }, new { v.Version, v.Status, v.CreatedBy, v.ApprovedBy, v.EffectiveFrom }, null, ct);
        return v;
    }, ct);

    // ---------- Tham số kinh tế ----------
    public Task<EconomyChange> ProposeEconomyAsync(Actor by, ProposeBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var c = await economy.ProposeAsync(b.Key ?? "", b.Value, b.EffectiveFrom, by.Email, b.Note, ct);
        await audit.WriteAsync(by, "ECON_PROPOSE", "economy", c.Key, null, new { c.Value, c.EffectiveFrom }, b.Note, ct);
        return c;
    }, ct);

    public Task<EconomyChange> DecideEconomyAsync(Actor by, Guid id, bool approve, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var c = approve ? await economy.ApproveAsync(id, by.Email, ct) : await economy.RejectAsync(id, by.Email, ct);
        await audit.WriteAsync(by, approve ? "ECON_APPROVE" : "ECON_REJECT", "economy", c.Key, new { c.ProposedBy }, new { c.Value, c.Status, c.EffectiveFrom, decidedBy = by.Email }, null, ct);
        return c;
    }, ct);

    // ---------- Thẻ ----------
    public Task DiscontinueCardAsync(Actor by, int id, CancellationToken ct) => uow.RunAsync(async () =>
    {
        await catalog.DiscontinueCardAsync(id, ct);
        await audit.WriteAsync(by, "CARD_DISCONTINUE", "card", id.ToString(), new { discontinued = false }, new { discontinued = true }, null, ct);
    }, ct);

    // ---------- Bồi thường Coin (BR-ADM-04) ----------
    private const string CompCols = "id,account_id,coin,ticket,reason,status,created_by,created_at,decided_by,decided_at";
    private static CompensationDto MapComp(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetInt64(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5), r.GetString(6), r.GetFieldValue<DateTimeOffset>(7),
        r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9));

    public async Task<IReadOnlyList<CompensationDto>> ListCompensationsAsync(CancellationToken ct) =>
        await uow.QueryAsync($"SELECT {CompCols} FROM admin.compensation ORDER BY created_at DESC LIMIT 100", MapComp, ct);

    public Task<CompensationDto> CreateCompensationAsync(Actor by, CompensationBody b, CancellationToken ct) => uow.RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(b.Ticket)) throw new DomainException(ErrorCodes.TicketRequired, "A support ticket code is required", 400);   // SC-ADM-11
        if (b.Coin is < 1 or > 1_000_000) throw new DomainException(ErrorCodes.InvalidAmount, "Coin must be between 1 and 1,000,000", 400);
        if (await identity.GetAsync(b.AccountId, ct) is null) throw DomainException.NotFound("Account");
        var c = (await uow.QueryAsync($"INSERT INTO admin.compensation(id,account_id,coin,ticket,reason,status,created_by,created_at) VALUES(@i,@a,@c,@t,@r,'pending',@by,@now) RETURNING {CompCols}", MapComp, ct,
            ("i", Guid.NewGuid()), ("a", b.AccountId), ("c", b.Coin), ("t", b.Ticket.Trim()), ("r", b.Reason), ("by", by.Email), ("now", clock.UtcNow)))[0];
        await audit.WriteAsync(by, "COMP_CREATE", "account", b.AccountId.ToString(), null, new { c.Coin, c.Ticket }, b.Reason, ct);
        return c;
    }, ct);

    public Task<CompensationDto> DecideCompensationAsync(Actor by, Guid id, bool approve, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var c = (await uow.QueryAsync($"SELECT {CompCols} FROM admin.compensation WHERE id=@i FOR UPDATE", MapComp, ct, ("i", id))).FirstOrDefault() ?? throw DomainException.NotFound("Compensation");
        if (c.Status != "pending") throw new DomainException(ErrorCodes.InvalidState, $"The request is already {c.Status}", 409);
        if (string.Equals(c.CreatedBy, by.Email, StringComparison.OrdinalIgnoreCase)) throw new DomainException(ErrorCodes.SelfApprovalForbidden, "You cannot decide your own request", 403);
        long? ledger = null;
        if (approve)
        {
            // Bút toán tham chiếu ticket; idempotent theo mã yêu cầu.
            ledger = (await wallet.CreditAsync(c.AccountId, Currencies.Coin, c.Coin, "COMPENSATION", "ticket", c.Ticket, $"compensation:{c.Id}", ct)).Id;
        }
        var done = (await uow.QueryAsync($"UPDATE admin.compensation SET status=@s, decided_by=@d, decided_at=@now, ledger_entry_id=@l WHERE id=@i RETURNING {CompCols}", MapComp, ct,
            ("s", approve ? "approved" : "rejected"), ("d", by.Email), ("now", clock.UtcNow), ("l", ledger), ("i", id)))[0];
        await audit.WriteAsync(by, approve ? "COMP_APPROVE" : "COMP_REJECT", "account", c.AccountId.ToString(), null, new { c.Coin, c.Ticket, createdBy = c.CreatedBy, approvedBy = by.Email, ledgerEntryId = ledger }, null, ct);
        return done;
    }, ct);

    // ---------- Dashboard ----------
    public async Task<object> DashboardAsync(CancellationToken ct)
    {
        var all = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var c in sp.GetServices<IStatsContributor>()) foreach (var (k, v) in await c.CollectAsync(ct)) all[k] = v;
        return new { generatedAt = clock.UtcNow, metrics = all };
    }
}

internal sealed class AdminBootstrap(IUnitOfWork uow, Microsoft.Extensions.Options.IOptions<AdminOptions> opt, ILogger<AdminBootstrap> log) : IStartupTask
{
    public async Task RunAsync(CancellationToken ct)
    {
        var o = opt.Value;
        async Task Ensure(string email, string password, string role)
        {
            await uow.ExecAsync("INSERT INTO admin.admin_user(id,email,email_norm,password_hash,role) VALUES(@i,@e,@n,@p,@r) ON CONFLICT (email_norm) DO NOTHING", ct,
                ("i", Guid.NewGuid()), ("e", email), ("n", email.ToLowerInvariant()), ("p", PasswordHasher.Hash(password, o.Pbkdf2Iterations)), ("r", role));
        }
        if (o.SeedDemoUsers)
            foreach (var role in Roles.All) await Ensure($"{role}@anima.local", AdminOptions.DemoPassword, role);
        // Một Super Admin thứ hai cho thử quy trình duyệt chéo.
        if (o.SeedDemoUsers) await Ensure("super_admin2@anima.local", AdminOptions.DemoPassword, Roles.Super);
        if (!string.IsNullOrWhiteSpace(o.BootstrapEmail) && !string.IsNullOrWhiteSpace(o.BootstrapPassword)
            && await uow.ScalarAsync<int?>("SELECT 1 FROM admin.admin_user WHERE role='super_admin' LIMIT 1", ct) is null)
        {
            await Ensure(o.BootstrapEmail, o.BootstrapPassword, Roles.Super);
            LogBootstrap(log, o.BootstrapEmail);
        }
    }

    private static void LogBootstrap(ILogger l, string email) => l.LogWarning("Created the first Super Admin {Email}. Change this password now.", email);
}
