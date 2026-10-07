using System.Security.Claims;
using Anima.Contracts;
using Anima.Identity.Contracts;
using Anima.SharedKernel;

namespace Anima.Fairness;

public sealed record SeedPublicInfo(string ServerSeedHash, string ClientSeed, int NextNonce);
public sealed record RevealedSeed(string ServerSeedHash, string ServerSeed, string ClientSeed, int NoncesUsed, DateTimeOffset RevealedAt);
public sealed record RotateResult(RevealedSeed Previous, SeedPublicInfo Current);

/// <summary>Phiên quay: đã khóa seed active và dùng <see cref="Nonce"/>; nonce của seed đã tăng 1 (SAD 8.1).</summary>
public sealed class RollSession
{
    private readonly string _serverSeed;
    public RollSession(Guid seedId, string serverSeed, string hash, string clientSeed, int nonce)
    { SeedId = seedId; _serverSeed = serverSeed; SeedHash = hash; ClientSeed = clientSeed; Nonce = nonce; }
    public Guid SeedId { get; }
    public string SeedHash { get; }
    public string ClientSeed { get; }
    public int Nonce { get; }
    public int Roll(int slot) => FairnessMath.Roll(_serverSeed, ClientSeed, Nonce, slot);
    public int RollCard(int slot) => FairnessMath.Roll(_serverSeed, ClientSeed, Nonce, slot, "c");
    /// <summary>Lần chọn lại khi bản cuối vừa hết do đồng thời (SAD 8.2 bước 5).</summary>
    public int RollCard(int slot, int retry) => FairnessMath.Roll(_serverSeed, ClientSeed, Nonce, slot, retry == 0 ? "c" : $"c{retry}");
}

public interface IFairnessApi
{
    Task EnsureSeedAsync(Guid accountId, CancellationToken ct);
    /// <summary>Khóa seed active, lấy nonce hiện tại và tăng 1. Phải gọi trong transaction của lần mở/lật.</summary>
    Task<RollSession> BeginRollAsync(Guid accountId, CancellationToken ct);
}

public sealed class FairnessModule : IModule
{
    public string Name => "fairness";
    public System.Reflection.Assembly MigrationAssembly => typeof(FairnessModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<FairnessService>();
        s.AddScoped<IFairnessApi>(sp => sp.GetRequiredService<FairnessService>());
        s.AddScoped<IDomainEventHandler<AccountRegistered>, SeedOnRegistration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/fairness");
        g.MapGet("", async (ClaimsPrincipal u, FairnessService s, CancellationToken ct) => Results.Ok(await s.GetPublicAsync(u.AccountId(), ct))).RequireAuthorization();
        g.MapPut("/client-seed", async (ClaimsPrincipal u, ClientSeedRequest r, FairnessService s, CancellationToken ct) => Results.Ok(await s.SetClientSeedAsync(u.AccountId(), r.ClientSeed, ct))).RequireAuthorization();
        g.MapPost("/rotate", async (ClaimsPrincipal u, RotateRequest? r, FairnessService s, CancellationToken ct) => Results.Ok(await s.RotateAsync(u.AccountId(), r?.ClientSeed, ct))).RequireAuthorization();
        g.MapGet("/history", async (ClaimsPrincipal u, FairnessService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(u.AccountId(), ct))).RequireAuthorization();
        // SC-PF-04: seed đang dùng không bao giờ được trả về.
        g.MapGet("/server-seed", (ClaimsPrincipal u) => Results.Json(new { code = ErrorCodes.SeedNotRevealed, message = "The active server seed is revealed only after you rotate it" }, statusCode: 403)).RequireAuthorization();
        // Công cụ kiểm chứng công khai: chỉ là hàm thuần.
        g.MapPost("/verify", (VerifyRequest r) =>
        {
            if (string.IsNullOrEmpty(r.ServerSeed) || string.IsNullOrEmpty(r.ClientSeed) || r.Nonce < 1 || r.Slots is < 1 or > 20)
                throw DomainException.Validation("serverSeed, clientSeed, nonce >= 1 and 1 <= slots <= 20 are required");
            return Results.Ok(new
            {
                serverSeedHash = FairnessMath.Sha256Hex(r.ServerSeed),
                rolls = Enumerable.Range(0, r.Slots).Select(i => FairnessMath.Roll(r.ServerSeed, r.ClientSeed, r.Nonce, i)).ToArray(),
                cardRolls = Enumerable.Range(0, r.Slots).Select(i => FairnessMath.Roll(r.ServerSeed, r.ClientSeed, r.Nonce, i, "c")).ToArray(),
            });
        });
    }
}

public sealed record ClientSeedRequest(string? ClientSeed);
public sealed record RotateRequest(string? ClientSeed);
public sealed record VerifyRequest(string? ServerSeed, string? ClientSeed, int Nonce, int Slots);

internal sealed class SeedOnRegistration(IFairnessApi api) : IDomainEventHandler<AccountRegistered>
{
    public Task HandleAsync(AccountRegistered e, CancellationToken ct) => api.EnsureSeedAsync(e.AccountId, ct);
}

public sealed class FairnessService(IUnitOfWork uow, IFieldCipher cipher, ISecureRandom rng, IClock clock) : IFairnessApi
{
    private string NewServerSeed() => Convert.ToHexStringLower(rng.Bytes(32));
    private string NewClientSeed() => Convert.ToHexStringLower(rng.Bytes(8));

    private static string ValidClientSeed(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 64 || s.Contains(':')) throw DomainException.Validation("clientSeed must be 1-64 characters and must not contain ':'");
        return s;
    }

    private Task InsertAsync(Guid accountId, string clientSeed, CancellationToken ct)
    {
        var seed = NewServerSeed();
        return uow.ExecAsync("INSERT INTO fairness.seed(id,account_id,server_seed_enc,server_seed_hash,client_seed,status,created_at) VALUES(@id,@a,@e,@h,@c,'active',@now) ON CONFLICT (account_id) WHERE status='active' DO NOTHING", ct,
            ("id", Guid.NewGuid()), ("a", accountId), ("e", cipher.Encrypt(seed)), ("h", FairnessMath.Sha256Hex(seed)), ("c", clientSeed), ("now", clock.UtcNow));
    }

    public Task EnsureSeedAsync(Guid accountId, CancellationToken ct) => uow.RunAsync(async () =>
    {
        if (await uow.ScalarAsync<int?>("SELECT 1 FROM fairness.seed WHERE account_id=@a AND status='active'", ct, ("a", accountId)) is null)
            await InsertAsync(accountId, NewClientSeed(), ct);
    }, ct);

    private sealed record ActiveSeed(Guid Id, byte[] Enc, string Hash, string Client, int Nonce);

    /// <summary>
    /// Khóa dòng seed active của tài khoản (FOR UPDATE). Nếu đổi seed chen vào giữa, dòng cũ vừa chuyển sang 'revealed' nên truy vấn trả 0 dòng:
    /// lúc đó chọn lại (câu lệnh mới thấy seed mới đã commit) thay vì tạo seed thứ hai.
    /// </summary>
    private async Task<ActiveSeed> LockActiveAsync(Guid accountId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var rows = await uow.QueryAsync("SELECT id, server_seed_enc, server_seed_hash, client_seed, next_nonce FROM fairness.seed WHERE account_id=@a AND status='active' FOR UPDATE",
                r => new ActiveSeed(r.GetGuid(0), r.GetFieldValue<byte[]>(1), r.GetString(2), r.GetString(3), r.GetInt32(4)), ct, ("a", accountId));
            if (rows.Count > 0) return rows[0];
            await InsertAsync(accountId, NewClientSeed(), ct);       // chưa có seed (hoặc vừa bị đổi): ON CONFLICT DO NOTHING
        }
        throw new InvalidOperationException("Could not lock the active fairness seed");
    }

    public async Task<RollSession> BeginRollAsync(Guid accountId, CancellationToken ct)
    {
        if (!uow.InTransaction) throw new InvalidOperationException("BeginRollAsync must run inside a transaction");
        // FOR UPDATE: đổi seed và mở pack đồng thời xếp hàng, mỗi lần quay dùng trọn một seed (SC-PF-05).
        var s = await LockActiveAsync(accountId, ct);
        await uow.ExecAsync("UPDATE fairness.seed SET next_nonce=next_nonce+1 WHERE id=@id", ct, ("id", s.Id));
        return new RollSession(s.Id, cipher.Decrypt(s.Enc), s.Hash, s.Client, s.Nonce);
    }

    public async Task<SeedPublicInfo> GetPublicAsync(Guid accountId, CancellationToken ct)
    {
        await EnsureSeedAsync(accountId, ct);
        var rows = await uow.QueryAsync("SELECT server_seed_hash, client_seed, next_nonce FROM fairness.seed WHERE account_id=@a AND status='active'",
            r => new SeedPublicInfo(r.GetString(0), r.GetString(1), r.GetInt32(2)), ct, ("a", accountId));
        return rows[0];
    }

    public Task<SeedPublicInfo> SetClientSeedAsync(Guid accountId, string? clientSeed, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var cs = ValidClientSeed(clientSeed);
        var cur = await LockActiveAsync(accountId, ct);
        await uow.ExecAsync("UPDATE fairness.seed SET client_seed=@c WHERE id=@id", ct, ("c", cs), ("id", cur.Id));
        return await GetPublicAsync(accountId, ct);
    }, ct);

    /// <summary>BR-PF-04: công bố server seed cũ, tạo seed mới, nonce về 1.</summary>
    public Task<RotateResult> RotateAsync(Guid accountId, string? newClientSeed, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var cs = newClientSeed is null ? NewClientSeed() : ValidClientSeed(newClientSeed);
        var old = await LockActiveAsync(accountId, ct);
        await uow.ExecAsync("UPDATE fairness.seed SET status='revealed', revealed_at=@now WHERE id=@id", ct, ("now", clock.UtcNow), ("id", old.Id));
        await InsertAsync(accountId, cs, ct);
        return new RotateResult(new RevealedSeed(old.Hash, cipher.Decrypt(old.Enc), old.Client, old.Nonce - 1, clock.UtcNow), await GetPublicAsync(accountId, ct));
    }, ct);

    public async Task<IReadOnlyList<RevealedSeed>> HistoryAsync(Guid accountId, CancellationToken ct) =>
        await uow.QueryAsync("SELECT server_seed_enc, server_seed_hash, client_seed, next_nonce, revealed_at FROM fairness.seed WHERE account_id=@a AND status='revealed' ORDER BY revealed_at DESC",
            r => new RevealedSeed(r.GetString(1), cipher.Decrypt(r.GetFieldValue<byte[]>(0)), r.GetString(2), r.GetInt32(3) - 1, r.GetFieldValue<DateTimeOffset>(4)), ct, ("a", accountId));
}
