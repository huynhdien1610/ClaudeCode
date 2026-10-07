using System.Security.Claims;
using Anima.Contracts;
using Anima.Economy;
using Anima.Identity.Contracts;
using Anima.SharedKernel;

namespace Anima.Wallet;

public sealed record LedgerEntryDto(long Id, string Currency, long Amount, long BalanceAfter, string Reason, string? RefType, string? RefId, DateTimeOffset CreatedAt);
public sealed record Balances(long Gem, long Coin);
public sealed record ConvertRequest(string? Direction, long Amount);
public sealed record ConvertResult(string Direction, long Spent, long Received, Balances Balances);

public interface IWalletApi
{
    /// <summary>Cộng tiền. Trùng <paramref name="idempotencyKey"/> thì trả lại bút toán đã ghi, không cộng lần hai.</summary>
    Task<LedgerEntryDto> CreditAsync(Guid accountId, string currency, long amount, string reason, string? refType, string? refId, string? idempotencyKey, CancellationToken ct);
    /// <summary>Trừ tiền; ném INSUFFICIENT_BALANCE nếu không đủ. Phải gọi trong transaction của nghiệp vụ gọi nó.</summary>
    Task<LedgerEntryDto> DebitAsync(Guid accountId, string currency, long amount, string reason, string? refType, string? refId, string? idempotencyKey, CancellationToken ct);
    Task<Balances> GetBalancesAsync(Guid accountId, CancellationToken ct);
    /// <summary>
    /// BR-WAL-04: thu hồi Gem khi hoàn tiền. Khác <see cref="DebitAsync"/>, số dư Gem được phép xuống âm (Coin thì không bao giờ).
    /// Trả về bút toán, trong đó <c>BalanceAfter</c> là số dư Gem sau khi thu hồi.
    /// </summary>
    Task<LedgerEntryDto> ClawbackGemAsync(Guid accountId, long amount, string reason, string? refType, string? refId, string? idempotencyKey, CancellationToken ct);
}

public sealed class WalletModule : IModule
{
    public string Name => "wallet";
    public System.Reflection.Assembly MigrationAssembly => typeof(WalletModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<WalletService>();
        s.AddScoped<IWalletApi>(sp => sp.GetRequiredService<WalletService>());
        s.AddScoped<IStatsContributor, WalletStats>();
        s.AddScoped<IDomainEventHandler<AccountRegistered>, FirstLoginReward>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/wallet").RequireAuthorization();
        g.MapGet("", async (ClaimsPrincipal u, WalletService s, CancellationToken ct) => Results.Ok(await s.GetBalancesAsync(u.AccountId(), ct)));
        g.MapGet("/ledger", async (ClaimsPrincipal u, long? before, int? limit, WalletService s, CancellationToken ct) =>
            Results.Ok(await s.LedgerAsync(u.AccountId(), before, Math.Clamp(limit ?? 50, 1, 200), ct)));
        g.MapPost("/convert", async (HttpRequest req, ClaimsPrincipal u, ConvertRequest r, WalletService s, IIdempotency idem, CancellationToken ct) =>
        {
            var key = req.RequireIdempotencyKey(); var acc = u.AccountId();
            return Results.Ok(await idem.RunAsync(acc, key, "wallet.convert", Idempotency.Hash(r.Direction, r.Amount), () => s.ConvertAsync(acc, r.Direction, r.Amount, key, ct), ct));
        });
    }
}

internal sealed class FirstLoginReward(IWalletApi wallet, IEconomyApi economy) : IDomainEventHandler<AccountRegistered>
{
    public async Task HandleAsync(AccountRegistered e, CancellationToken ct) =>
        await wallet.CreditAsync(e.AccountId, Currencies.Coin, await economy.GetAsync(EconomyKeys.FirstLoginCoin, ct), "FIRST_LOGIN", "account", e.AccountId.ToString(), $"first-login:{e.AccountId}", ct);
}

public sealed class WalletService(IUnitOfWork uow, IClock clock, IEconomyApi economy, IIdentityApi identity) : IWalletApi
{
    private static LedgerEntryDto Map(Npgsql.NpgsqlDataReader r) => new(r.GetInt64(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3), r.GetString(4),
        r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetFieldValue<DateTimeOffset>(7));
    private const string Cols = "id, currency, amount, balance_after, reason, ref_type, ref_id, created_at";

    public Task<LedgerEntryDto> CreditAsync(Guid accountId, string currency, long amount, string reason, string? refType, string? refId, string? idem, CancellationToken ct) =>
        ApplyAsync(accountId, currency, Positive(amount), reason, refType, refId, idem, ct);

    public Task<LedgerEntryDto> DebitAsync(Guid accountId, string currency, long amount, string reason, string? refType, string? refId, string? idem, CancellationToken ct) =>
        ApplyAsync(accountId, currency, -Positive(amount), reason, refType, refId, idem, ct);

    public Task<LedgerEntryDto> ClawbackGemAsync(Guid accountId, long amount, string reason, string? refType, string? refId, string? idem, CancellationToken ct) =>
        ApplyAsync(accountId, Currencies.Gem, -Positive(amount), reason, refType, refId, idem, ct, allowNegative: true);

    private static long Positive(long a) => a > 0 ? a : throw new DomainException(ErrorCodes.InvalidAmount, "Amount must be positive");

    private Task<LedgerEntryDto> ApplyAsync(Guid acc, string currency, long delta, string reason, string? refType, string? refId, string? idem, CancellationToken ct, bool allowNegative = false)
    {
        if (!Currencies.IsValid(currency)) throw DomainException.Validation("Unknown currency");
        return uow.RunAsync(async () =>
        {
            if (idem is not null)
            {
                var ex = await uow.QueryAsync($"SELECT {Cols} FROM wallet.ledger_entry WHERE account_id=@a AND idempotency_key=@k", Map, ct, ("a", acc), ("k", idem));
                if (ex.Count > 0) return ex[0];
            }
            await uow.ExecAsync("INSERT INTO wallet.balance(account_id,currency) VALUES(@a,@c) ON CONFLICT DO NOTHING", ct, ("a", acc), ("c", currency));
            var cur = await uow.ScalarAsync<long>("SELECT amount FROM wallet.balance WHERE account_id=@a AND currency=@c FOR UPDATE", ct, ("a", acc), ("c", currency));
            var next = cur + delta;
            if (next < 0 && delta < 0 && !(allowNegative && currency == Currencies.Gem)) throw new DomainException(ErrorCodes.InsufficientBalance, $"Insufficient {currency}", 402, new { currency, required = -delta, available = cur });
            await uow.ExecAsync("UPDATE wallet.balance SET amount=@n, version=version+1 WHERE account_id=@a AND currency=@c", ct, ("n", next), ("a", acc), ("c", currency));
            var rows = await uow.QueryAsync($@"INSERT INTO wallet.ledger_entry(account_id,currency,amount,balance_after,reason,ref_type,ref_id,idempotency_key,created_at)
                VALUES(@a,@c,@d,@n,@r,@rt,@ri,@k,@now) RETURNING {Cols}", Map, ct,
                ("a", acc), ("c", currency), ("d", delta), ("n", next), ("r", reason), ("rt", refType), ("ri", refId), ("k", idem), ("now", clock.UtcNow));
            return rows[0];
        }, ct);
    }

    public async Task<Balances> GetBalancesAsync(Guid accountId, CancellationToken ct)
    {
        var rows = await uow.QueryAsync("SELECT currency, amount FROM wallet.balance WHERE account_id=@a", r => (r.GetString(0), r.GetInt64(1)), ct, ("a", accountId));
        return new Balances(rows.FirstOrDefault(x => x.Item1 == Currencies.Gem).Item2, rows.FirstOrDefault(x => x.Item1 == Currencies.Coin).Item2);
    }

    public async Task<IReadOnlyList<LedgerEntryDto>> LedgerAsync(Guid acc, long? before, int limit, CancellationToken ct) =>
        await uow.QueryAsync($"SELECT {Cols} FROM wallet.ledger_entry WHERE account_id=@a AND (@b::bigint IS NULL OR id < @b) ORDER BY id DESC LIMIT @l", Map, ct, ("a", acc), ("b", before), ("l", limit));

    /// <summary>BR-WAL-05/06. GEM_TO_COIN: Amount = số Gem. COIN_TO_GEM: Amount = số Coin; số lẻ không đủ 1 Gem không bị trừ.</summary>
    public Task<ConvertResult> ConvertAsync(Guid acc, string? direction, long amount, string idemKey, CancellationToken ct) => uow.RunAsync(async () =>
    {
        if (direction is not ("GEM_TO_COIN" or "COIN_TO_GEM")) throw DomainException.Validation("direction must be GEM_TO_COIN or COIN_TO_GEM");
        if (amount <= 0) throw new DomainException(ErrorCodes.InvalidAmount, "Amount must be positive");
        long spent, received;
        if (direction == "GEM_TO_COIN")
        {
            spent = amount; received = checked(amount * await economy.GetAsync(EconomyKeys.GemToCoinRate, ct));
            await DebitAsync(acc, Currencies.Gem, spent, "CONVERT_OUT", "convert", idemKey, idemKey + ":out", ct);
            await CreditAsync(acc, Currencies.Coin, received, "CONVERT_IN", "convert", idemKey, idemKey + ":in", ct);
        }
        else
        {
            var info = await identity.GetAsync(acc, ct);
            if (!info.PhoneVerified) throw new DomainException(ErrorCodes.PhoneVerificationRequired, "Verify your phone number to convert Coin to Gem", 403);
            var rate = await economy.GetAsync(EconomyKeys.CoinToGemRate, ct);
            received = amount / rate;
            if (received < 1) throw new DomainException(ErrorCodes.InvalidAmount, $"At least {rate} Coin are needed for 1 Gem");
            spent = received * rate;
            var cap = await economy.GetAsync(EconomyKeys.CoinToGemDailyCapGem, ct);
            var (from, to) = TimeZones.LocalDayRange(clock.UtcNow, info.Timezone);
            // Khóa dòng Gem để các yêu cầu đồng thời của cùng tài khoản xếp hàng trước khi kiểm hạn mức.
            await uow.ExecAsync("INSERT INTO wallet.balance(account_id,currency) VALUES(@a,'GEM') ON CONFLICT DO NOTHING", ct, ("a", acc));
            await uow.ScalarAsync<long>("SELECT amount FROM wallet.balance WHERE account_id=@a AND currency='GEM' FOR UPDATE", ct, ("a", acc));
            var today = await uow.ScalarAsync<long?>("SELECT COALESCE(SUM(amount),0)::bigint FROM wallet.ledger_entry WHERE account_id=@a AND currency='GEM' AND reason='CONVERT_IN' AND created_at >= @f AND created_at < @t", ct,
                ("a", acc), ("f", from), ("t", to)) ?? 0;
            if (today + received > cap) throw new DomainException(ErrorCodes.CoinToGemDailyLimit, $"Daily Coin to Gem limit is {cap} Gem", 429, new { cap, convertedToday = today });
            await DebitAsync(acc, Currencies.Coin, spent, "CONVERT_OUT", "convert", idemKey, idemKey + ":out", ct);
            await CreditAsync(acc, Currencies.Gem, received, "CONVERT_IN", "convert", idemKey, idemKey + ":in", ct);
        }
        return new ConvertResult(direction, spent, received, await GetBalancesAsync(acc, ct));
    }, ct);
}

internal sealed class WalletStats(IUnitOfWork uow) : IStatsContributor
{
    public async Task<IReadOnlyDictionary<string, long>> CollectAsync(CancellationToken ct)
    {
        var v = (await uow.QueryAsync(@"SELECT count(*),
            COALESCE(sum(amount) FILTER (WHERE currency='COIN' AND amount>0),0)::bigint, COALESCE(-sum(amount) FILTER (WHERE currency='COIN' AND amount<0),0)::bigint,
            COALESCE(sum(amount) FILTER (WHERE currency='GEM' AND amount>0),0)::bigint, COALESCE(-sum(amount) FILTER (WHERE currency='GEM' AND amount<0),0)::bigint,
            COALESCE(sum(amount) FILTER (WHERE reason='DEV_TOPUP'),0)::bigint FROM wallet.ledger_entry", r => new[] { r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5) }, ct))[0];
        return new Dictionary<string, long> { ["ledger_entries"] = v[0], ["coin_issued"] = v[1], ["coin_spent"] = v[2], ["gem_issued"] = v[3], ["gem_spent"] = v[4], ["gem_dev_topup"] = v[5] };
    }
}
