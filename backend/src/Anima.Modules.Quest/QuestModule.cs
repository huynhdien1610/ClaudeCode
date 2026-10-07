using System.Security.Claims;
using Anima.Contracts;
using Anima.Gacha;
using Anima.Identity.Contracts;
using Anima.SharedKernel;
using Anima.Wallet;

namespace Anima.Quest;

public sealed record QuestTaskDef(string Code, string Kind, int Target);
public sealed record QuestDayDef(int Day, QuestTaskDef[] Tasks, string RewardType, long RewardCoin = 0);

/// <summary>
/// Nhiệm vụ Tân thủ 7 ngày (BR-NEW-03). Nội dung từng ngày là đề xuất [BA] chờ PO duyệt: BRD chỉ quy định "mỗi ngày 1 nhóm nhiệm vụ",
/// nên bản này chọn các việc làm được mà không cần tiêu tiền. Đếm theo tổng số lần từ lúc tạo tài khoản (làm bù được).
/// Tổng 6 pack có thể mở (1 chào mừng + 5 pack cơ bản của ngày 1 → 5) nên ngày 6 → 7 vẫn đạt được mà không phải mua gì.
/// </summary>
public static class NewbieQuests
{
    public const int Days = 7;
    public const string OpenPack = "OPEN_PACK", VerifyPhone = "VERIFY_PHONE";
    public const long Day67Coin = 200;   // Q-53

    public static readonly QuestDayDef[] All =
    [
        new(1, [new("open_welcome_pack", OpenPack, 1)], "BASIC_PACK"),
        new(2, [new("verify_phone", VerifyPhone, 1)], "BASIC_PACK"),
        new(3, [new("open_2_packs", OpenPack, 2)], "BASIC_PACK"),
        new(4, [new("open_3_packs", OpenPack, 3)], "BASIC_PACK"),
        new(5, [new("open_4_packs", OpenPack, 4)], "BASIC_PACK"),
        new(6, [new("open_5_packs", OpenPack, 5)], "COIN", Day67Coin),
        new(7, [new("open_6_packs", OpenPack, 6)], "COIN", Day67Coin),
    ];

    /// <summary>Ngày Tân thủ hiện tại (1 = ngày tạo tài khoản, theo múi giờ của tài khoản). &gt; 7 nghĩa là đã hết hạn.</summary>
    public static int CurrentDay(DateTimeOffset createdAt, DateTimeOffset now, string timezone)
    {
        if (!TimeZones.TryResolve(timezone, out var tz)) tz = TimeZoneInfo.Utc;
        var a = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(createdAt, tz).DateTime);
        var b = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);
        return Math.Max(1, b.DayNumber - a.DayNumber + 1);
    }
}

public sealed record QuestTaskView(string Code, string Kind, int Target, int Progress, bool Done);
public sealed record QuestDayView(int Day, bool Available, bool Complete, bool Claimed, string RewardType, long RewardCoin, IReadOnlyList<QuestTaskView> Tasks);
public sealed record QuestsView(int CurrentDay, bool Expired, DateTimeOffset EndsAt, IReadOnlyList<QuestDayView> Days);
public sealed record ClaimResult(int Day, string RewardType, long RewardCoin, Guid? PackInstanceId, Balances Balances);

public sealed class QuestModule : IModule
{
    public string Name => "quest";
    public System.Reflection.Assembly MigrationAssembly => typeof(QuestModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<QuestService>();
        s.AddScoped<IDomainEventHandler<PackOpened>, QuestCounters>();
        s.AddScoped<IDomainEventHandler<PhoneVerified>, QuestCounters>();
        s.AddScoped<QuestCounters>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/me/quests").RequireAuthorization();
        g.MapGet("", async (ClaimsPrincipal u, QuestService s, CancellationToken ct) => Results.Ok(await s.GetAsync(u.AccountId(), ct)));
        g.MapPost("/{day:int}/claim", async (ClaimsPrincipal u, int day, QuestService s, CancellationToken ct) => Results.Ok(await s.ClaimAsync(u.AccountId(), day, ct)));
    }
}

internal sealed class QuestCounters(IUnitOfWork uow) : IDomainEventHandler<PackOpened>, IDomainEventHandler<PhoneVerified>
{
    public Task HandleAsync(PackOpened e, CancellationToken ct) => Bump(e.AccountId, NewbieQuests.OpenPack, ct);
    public Task HandleAsync(PhoneVerified e, CancellationToken ct) => Bump(e.AccountId, NewbieQuests.VerifyPhone, ct);
    private Task Bump(Guid acc, string kind, CancellationToken ct) =>
        uow.ExecAsync("INSERT INTO quest.progress(account_id,kind,count) VALUES(@a,@k,1) ON CONFLICT (account_id,kind) DO UPDATE SET count = quest.progress.count + 1", ct, ("a", acc), ("k", kind));
}

public sealed class QuestService(IUnitOfWork uow, IClock clock, IIdentityApi identity, IGachaApi gacha, IWalletApi wallet)
{
    private async Task<Dictionary<string, int>> ProgressAsync(Guid acc, CancellationToken ct) =>
        (await uow.QueryAsync("SELECT kind,count FROM quest.progress WHERE account_id=@a", r => (K: r.GetString(0), N: r.GetInt32(1)), ct, ("a", acc))).ToDictionary(x => x.K, x => x.N);

    private static bool Complete(QuestDayDef d, IReadOnlyDictionary<string, int> p) => d.Tasks.All(t => p.GetValueOrDefault(t.Kind) >= t.Target);

    public async Task<QuestsView> GetAsync(Guid acc, CancellationToken ct)
    {
        var info = await identity.GetAsync(acc, ct);
        var now = clock.UtcNow;
        var cur = NewbieQuests.CurrentDay(info.CreatedAt, now, info.Timezone);
        var p = await ProgressAsync(acc, ct);
        var claimed = (await uow.QueryAsync("SELECT day FROM quest.claim WHERE account_id=@a", r => r.GetInt32(0), ct, ("a", acc))).ToHashSet();
        var days = NewbieQuests.All.Select(d => new QuestDayView(d.Day, d.Day <= cur && cur <= NewbieQuests.Days, Complete(d, p), claimed.Contains(d.Day), d.RewardType, d.RewardCoin,
            d.Tasks.Select(t => new QuestTaskView(t.Code, t.Kind, t.Target, Math.Min(p.GetValueOrDefault(t.Kind), t.Target), p.GetValueOrDefault(t.Kind) >= t.Target)).ToList())).ToList();
        return new QuestsView(Math.Min(cur, NewbieQuests.Days + 1), cur > NewbieQuests.Days, EndOfLastDay(info.CreatedAt, info.Timezone), days);
    }

    private static DateTimeOffset EndOfLastDay(DateTimeOffset createdAt, string timezone)
    {
        var (from, _) = TimeZones.LocalDayRange(createdAt, timezone);
        return TimeZones.LocalDayRange(from.AddDays(NewbieQuests.Days - 1).AddHours(12), timezone).To;
    }

    /// <summary>Nhận thưởng một ngày (BR-NEW-03): phải hoàn thành, không quá hạn (hết ngày 7), không phải ngày chưa tới, nhận đúng một lần.</summary>
    public Task<ClaimResult> ClaimAsync(Guid acc, int day, CancellationToken ct) => uow.RunAsync(async () =>
    {
        var def = NewbieQuests.All.FirstOrDefault(d => d.Day == day) ?? throw DomainException.Validation("day must be between 1 and 7");
        var info = await identity.GetAsync(acc, ct);
        var cur = NewbieQuests.CurrentDay(info.CreatedAt, clock.UtcNow, info.Timezone);
        if (cur > NewbieQuests.Days) throw new DomainException(ErrorCodes.QuestExpired, "The newbie quests have ended", 409);
        if (day > cur) throw new DomainException(ErrorCodes.QuestNotAvailable, "This day has not started yet", 409);
        // Khóa theo tài khoản để hai yêu cầu nhận cùng lúc không cùng cấp thưởng.
        await uow.ExecAsync("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", ct, ("k", "quest:" + acc));
        if (await uow.ScalarAsync<int?>("SELECT 1 FROM quest.claim WHERE account_id=@a AND day=@d", ct, ("a", acc), ("d", day)) is not null)
            throw DomainException.Conflict(ErrorCodes.QuestAlreadyClaimed, "This reward was already claimed");
        if (!Complete(def, await ProgressAsync(acc, ct))) throw new DomainException(ErrorCodes.QuestNotComplete, "Finish every task of this day first", 409);

        Guid? packId = null;
        if (def.RewardType == "BASIC_PACK") packId = await gacha.GrantBasicPackAsync(acc, ct);
        else await wallet.CreditAsync(acc, Currencies.Coin, def.RewardCoin, "NEWBIE_QUEST", "quest", $"day-{day}", $"quest:{acc}:{day}", ct);
        await uow.ExecAsync("INSERT INTO quest.claim(account_id,day,reward_type,reward_ref,reward_coin,claimed_at) VALUES(@a,@d,@t,@r,@c,@now)", ct,
            ("a", acc), ("d", day), ("t", def.RewardType), ("r", packId?.ToString()), ("c", def.RewardType == "COIN" ? def.RewardCoin : null), ("now", clock.UtcNow));
        return new ClaimResult(day, def.RewardType, def.RewardCoin, packId, await wallet.GetBalancesAsync(acc, ct));
    }, ct);
}
