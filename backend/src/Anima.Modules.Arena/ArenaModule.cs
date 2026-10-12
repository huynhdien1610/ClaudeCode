using System.Security.Claims;
using System.Text.Json;
using Anima.Battle;
using Anima.Battle.Engine;
using Anima.Catalog;
using Anima.Contracts;
using Anima.Deck;
using Anima.Identity.Contracts;
using Anima.SharedKernel;

namespace Anima.Arena;

public sealed record QueueRequest(Guid? DeckId);
public sealed record ArenaActionRequest(string? Type, int? Hand, int? Slot, int? Target, string? Id = null);

/// <summary>Luật thuần của Đấu trường xếp hạng (không đụng DB) để dùng ở service và test.</summary>
public static class ArenaRules
{
    public const int TurnMs = 20_000, BankMs = 30_000, MulliganMs = 20_000, WinAp = 10, PairDailyLimit = 3;
    public const double BaseWindow = 100, WindowPerSecond = 25;

    /// <summary>Khoảng chênh điểm chấp nhận được; rộng dần theo thời gian người chờ lâu nhất đã chờ.</summary>
    public static double Window(double waitedSeconds) => BaseWindow + WindowPerSecond * Math.Max(0, waitedSeconds);

    /// <summary>BR-BTL-08: hạn của lượt = đầu lượt + 20 giây + quỹ dự phòng còn lại.</summary>
    public static DateTimeOffset Deadline(DateTimeOffset turnStart, int bankMs) => turnStart.AddMilliseconds(TurnMs + bankMs);

    /// <summary>Quỹ dự phòng còn lại sau một lượt: phần vượt quá 20 giây trừ vào quỹ.</summary>
    public static int BankAfterTurn(int bankMs, TimeSpan used) => Math.Max(0, bankMs - (int)Math.Max(0, used.TotalMilliseconds - TurnMs));

    public static string Label(Guid account) => "Keeper-" + Convert.ToHexString(account.ToByteArray())[..6];
}

public sealed class ArenaModule : IModule
{
    public string Name => "arena";
    public System.Reflection.Assembly MigrationAssembly => typeof(ArenaModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c) => s.AddScoped<ArenaService>();

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/arena").RequireAuthorization();
        g.MapGet("", async (ClaimsPrincipal u, ArenaService s, CancellationToken ct) => Results.Ok(await s.OverviewAsync(u.AccountId(), ct)));
        g.MapPost("/queue", async (ClaimsPrincipal u, QueueRequest r, ArenaService s, CancellationToken ct) => Results.Ok(await s.JoinAsync(u.AccountId(), r.DeckId, ct)));
        g.MapDelete("/queue", async (ClaimsPrincipal u, ArenaService s, CancellationToken ct) => { await s.LeaveAsync(u.AccountId(), ct); return Results.NoContent(); });
        g.MapGet("/leaderboard", async (ArenaService s, CancellationToken ct) => Results.Ok(await s.LeaderboardAsync(ct)));
        g.MapGet("/matches", async (ClaimsPrincipal u, ArenaService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(u.AccountId(), ct)));
        g.MapGet("/matches/{id:guid}", async (ClaimsPrincipal u, Guid id, ArenaService s, CancellationToken ct) => Results.Ok(await s.GetAsync(u.AccountId(), id, ct)));
        g.MapPost("/matches/{id:guid}/actions", async (ClaimsPrincipal u, Guid id, ArenaActionRequest a, ArenaService s, CancellationToken ct) => Results.Ok(await s.ActAsync(u.AccountId(), id, a, ct)));
        g.MapGet("/matches/{id:guid}/replay", async (ClaimsPrincipal u, Guid id, ArenaService s, CancellationToken ct) => Results.Ok(await s.ReplayAsync(u.AccountId(), id, ct)));
    }
}

public sealed class ArenaService(IUnitOfWork uow, IClock clock, ISecureRandom random, IDeckApi decks, ICatalogApi catalog, IIdentityApi identity)
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    private sealed record Row(Guid Id, Guid P0, Guid P1, string Status, byte[] Seed, List<BattleCard> Cards0, List<BattleCard> Cards1, List<BattleAction> Actions,
        int? Winner, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset TurnStartedAt, int[] Bank, double Rating0, double Rating1, double? Delta0, double? Delta1, int Ap0, int Ap1);

    private const string Cols = @"id,p0,p1,status,seed,cards0::text,cards1::text,actions::text,winner,reason,created_at,turn_started_at,bank0_ms,bank1_ms,rating0,rating1,delta0,delta1,ap0,ap1";
    private static Row Map(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetFieldValue<byte[]>(4),
        JsonSerializer.Deserialize<List<BattleCard>>(r.GetString(5), J)!, JsonSerializer.Deserialize<List<BattleCard>>(r.GetString(6), J)!,
        JsonSerializer.Deserialize<List<BattleAction>>(r.GetString(7), J)!, r.IsDBNull(8) ? null : r.GetInt32(8), r.IsDBNull(9) ? null : r.GetString(9),
        r.GetFieldValue<DateTimeOffset>(10), r.GetFieldValue<DateTimeOffset>(11), [r.GetInt32(12), r.GetInt32(13)], r.GetDouble(14), r.GetDouble(15),
        r.IsDBNull(16) ? null : r.GetDouble(16), r.IsDBNull(17) ? null : r.GetDouble(17), r.GetInt32(18), r.GetInt32(19));

    private static BattleState Rebuild(Row m, int upTo = int.MaxValue)
    {
        var s = BattleEngine.Start(new BattleSetup(m.Seed, m.Cards0, m.Cards1, null, MemoryAllowed: false));      // Ranked PvP cấm Ký ức phong ấn (BR-CARD-05)
        foreach (var a in m.Actions.Take(upTo)) BattleEngine.Apply(s, a);
        return s;
    }

    private async Task<Row> Load(Guid id, bool forUpdate, CancellationToken ct) =>
        (await uow.QueryAsync($"SELECT {Cols} FROM arena.match WHERE id=@i" + (forUpdate ? " FOR UPDATE" : ""), Map, ct, ("i", id))).FirstOrDefault() ?? throw DomainException.NotFound("Match");

    private static int Side(Row m, Guid acc) => m.P0 == acc ? 0 : m.P1 == acc ? 1 : throw DomainException.NotFound("Match");

    // ---------- Điểm kỹ năng và Arena Point ----------
    private async Task<(Glicko G, int Games, int Wins)> RatingAsync(Guid acc, CancellationToken ct)
    {
        var r = (await uow.QueryAsync("SELECT rating,rd,vol,games,wins FROM arena.rating WHERE account_id=@a", x => (G: new Glicko(x.GetDouble(0), x.GetDouble(1), x.GetDouble(2)), Games: x.GetInt32(3), Wins: x.GetInt32(4)), ct, ("a", acc))).ToList();
        return r.Count > 0 ? r[0] : (Glicko.Initial, 0, 0);
    }

    private Task SaveRatingAsync(Guid acc, Glicko g, bool won, CancellationToken ct) =>
        uow.ExecAsync(@"INSERT INTO arena.rating(account_id,rating,rd,vol,games,wins,updated_at) VALUES(@a,@r,@d,@v,1,@w,@now)
            ON CONFLICT (account_id) DO UPDATE SET rating=@r, rd=@d, vol=@v, games=arena.rating.games+1, wins=arena.rating.wins+@w, updated_at=@now", ct,
            ("a", acc), ("r", g.Rating), ("d", g.Rd), ("v", g.Vol), ("w", won ? 1 : 0), ("now", clock.UtcNow));

    private async Task<long> ApAsync(Guid acc, CancellationToken ct) => await uow.ScalarAsync<long?>("SELECT balance FROM arena.ap WHERE account_id=@a", ct, ("a", acc)) ?? 0;

    // ---------- Tổng quan và ghép trận ----------
    public Task<object> OverviewAsync(Guid acc, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        var active = await SettleActiveAsync(acc, ct);
        if (active is null && await InQueueAsync(acc, ct)) active = await TryMatchAsync(acc, ct);
        var (g, games, wins) = await RatingAsync(acc, ct);
        var q = (await uow.QueryAsync("SELECT joined_at FROM arena.queue WHERE account_id=@a", x => x.GetFieldValue<DateTimeOffset>(0), ct, ("a", acc))).Select(x => (DateTimeOffset?)x).FirstOrDefault();
        return new
        {
            rating = Math.Round(g.Rating),
            rd = Math.Round(g.Rd),
            games,
            wins,
            ap = await ApAsync(acc, ct),
            queue = q is null ? null : new { joinedAt = q, waitedSeconds = (int)(clock.UtcNow - q.Value).TotalSeconds },
            activeMatchId = active,
            rules = new { turnSeconds = ArenaRules.TurnMs / 1000, bankSeconds = ArenaRules.BankMs / 1000, winAp = ArenaRules.WinAp },
        };
    }, ct);

    private async Task<bool> InQueueAsync(Guid acc, CancellationToken ct) => await uow.ScalarAsync<int?>("SELECT 1 FROM arena.queue WHERE account_id=@a", ct, ("a", acc)) is not null;

    private async Task<Guid?> ActiveMatchAsync(Guid acc, CancellationToken ct) =>
        await uow.ScalarAsync<Guid?>("SELECT id FROM arena.match WHERE status='InProgress' AND (p0=@a OR p1=@a) LIMIT 1", ct, ("a", acc));

    /// <summary>Trận đang đánh của tài khoản, sau khi xử lý hết giờ; trận đã quá hạn kết thúc thì không còn tính là đang đánh.</summary>
    private async Task<Guid?> SettleActiveAsync(Guid acc, CancellationToken ct)
    {
        var id = await ActiveMatchAsync(acc, ct);
        if (id is null) return null;
        var m = await Load(id.Value, true, ct);
        await SettleAsync(m, ct);
        return await ActiveMatchAsync(acc, ct);
    }

    public Task<object> JoinAsync(Guid acc, Guid? deckId, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        if (deckId is null) throw DomainException.Validation("deckId is required");
        var info = await identity.GetAsync(acc, ct);
        if (info.Status is "Restricted" or "Banned" or "PendingDeletion" or "Deleted") throw new DomainException(ErrorCodes.AccountRestricted, "Your account cannot play ranked matches", 403);
        await decks.RequireBattleReadyAsync(acc, deckId.Value, ct);                                                   // BR-DECK-07
        await uow.ExecAsync("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", ct, ("k", "arena:mm"));
        if (await SettleActiveAsync(acc, ct) is not null) throw DomainException.Conflict("MATCH_IN_PROGRESS", "Finish your current match first");
        var (g, _, _) = await RatingAsync(acc, ct);
        await uow.ExecAsync(@"INSERT INTO arena.queue(account_id,deck_id,rating,joined_at) VALUES(@a,@d,@r,@now)
            ON CONFLICT (account_id) DO UPDATE SET deck_id=@d, rating=@r", ct, ("a", acc), ("d", deckId.Value), ("r", g.Rating), ("now", clock.UtcNow));
        var matched = await TryMatchAsync(acc, ct);
        return new { queued = matched is null, matchId = matched };
    }, ct);

    public Task LeaveAsync(Guid acc, CancellationToken ct) => uow.ExecAsync("DELETE FROM arena.queue WHERE account_id=@a", ct, ("a", acc));

    /// <summary>Ghép <paramref name="acc"/> với người chờ gần điểm nhất (BR-PVP-06). Trả về id trận nếu ghép được.</summary>
    private async Task<Guid?> TryMatchAsync(Guid acc, CancellationToken ct)
    {
        await uow.ExecAsync("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", ct, ("k", "arena:mm"));
        var me = (await uow.QueryAsync("SELECT deck_id,rating,joined_at FROM arena.queue WHERE account_id=@a", x => (Deck: x.GetGuid(0), Rating: x.GetDouble(1), At: x.GetFieldValue<DateTimeOffset>(2)), ct, ("a", acc))).ToList();
        if (me.Count == 0) return await ActiveMatchAsync(acc, ct);                                                   // đã được người khác ghép
        var now = clock.UtcNow; var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var cands = await uow.QueryAsync("SELECT account_id,deck_id,rating,joined_at FROM arena.queue WHERE account_id<>@a ORDER BY abs(rating-@r), joined_at LIMIT 10",
            x => (Acc: x.GetGuid(0), Deck: x.GetGuid(1), Rating: x.GetDouble(2), At: x.GetFieldValue<DateTimeOffset>(3)), ct, ("a", acc), ("r", me[0].Rating));
        foreach (var c in cands)
        {
            var waited = Math.Max((now - me[0].At).TotalSeconds, (now - c.At).TotalSeconds);
            if (Math.Abs(c.Rating - me[0].Rating) > ArenaRules.Window(waited)) continue;
            var pair = await uow.ScalarAsync<long?>(@"SELECT count(*) FROM arena.match WHERE ((p0=@a AND p1=@b) OR (p0=@b AND p1=@a)) AND created_at >= @since", ct, ("a", acc), ("b", c.Acc), ("since", dayStart)) ?? 0;
            if (pair >= ArenaRules.PairDailyLimit) continue;                                                          // BR-PVP-06: tối đa 3 trận mỗi cặp mỗi ngày
            if (await ActiveMatchAsync(c.Acc, ct) is not null) { await uow.ExecAsync("DELETE FROM arena.queue WHERE account_id=@a", ct, ("a", c.Acc)); continue; }
            List<DeckCard> d0, d1;
            try { d0 = (await decks.RequireBattleReadyAsync(acc, me[0].Deck, ct)).ToList(); } catch (DomainException) { await uow.ExecAsync("DELETE FROM arena.queue WHERE account_id=@a", ct, ("a", acc)); throw; }
            try { d1 = (await decks.RequireBattleReadyAsync(c.Acc, c.Deck, ct)).ToList(); } catch (DomainException) { await uow.ExecAsync("DELETE FROM arena.queue WHERE account_id=@a", ct, ("a", c.Acc)); continue; }
            return await CreateMatchAsync(acc, c.Acc, d0, d1, me[0].Rating, c.Rating, ct);
        }
        return null;
    }

    private async Task<Guid> CreateMatchAsync(Guid p0, Guid p1, List<DeckCard> d0, List<DeckCard> d1, double r0, double r1, CancellationToken ct)
    {
        var defs = (await catalog.ListCardsAsync(ct)).ToDictionary(d => d.Id);
        var c0 = d0.Select(c => BattleCards.From(defs[c.CardDefinitionId])).ToList(); var c1 = d1.Select(c => BattleCards.From(defs[c.CardDefinitionId])).ToList();
        var id = Guid.NewGuid(); var seed = random.Bytes(32); var now = clock.UtcNow;
        await uow.ExecAsync(@"INSERT INTO arena.match(id,p0,p1,mode,status,seed,cards0,cards1,created_at,turn_started_at,rating0,rating1)
            VALUES(@i,@p0,@p1,'ranked','InProgress',@s,@c0::jsonb,@c1::jsonb,@now,@now,@r0,@r1)", ct,
            ("i", id), ("p0", p0), ("p1", p1), ("s", seed), ("c0", JsonSerializer.Serialize(c0, J)), ("c1", JsonSerializer.Serialize(c1, J)), ("now", now), ("r0", r0), ("r1", r1));
        await uow.ExecAsync("DELETE FROM arena.queue WHERE account_id = ANY(@a)", ct, ("a", new[] { p0, p1 }));
        return id;
    }

    // ---------- Đồng hồ và kết thúc trận ----------
    /// <summary>
    /// Áp hết thời gian (BR-BTL-08) một cách lười: không có tiến trình nền, mỗi lần ai đó đọc hoặc đánh trận này thì các hành động "timeout" (và "keep" tay đầu)
    /// đáng lẽ đã xảy ra được sinh ra, đóng dấu theo hạn, rồi lưu vào nhật ký như hành động thường nên phát lại vẫn đúng.
    /// Hết 3 lượt liên tiếp thì thua; đó cũng là cách xử lý mất kết nối quá 60 giây (BR-BTL-07).
    /// </summary>
    private async Task<(BattleState S, Row M, List<BattleAction> Log)> SettleAsync(Row m, CancellationToken ct)
    {
        var s = Rebuild(m); var log = new List<BattleAction>(); var now = clock.UtcNow;
        var start = m.TurnStartedAt; var bank = (int[])m.Bank.Clone();
        for (var guard = 0; guard < 400 && s.Phase != BattlePhase.Finished; guard++)
        {
            if (s.Phase == BattlePhase.Mulligan)
            {
                var due = m.CreatedAt.AddMilliseconds(ArenaRules.MulliganMs);
                if (now <= due) break;
                for (var p = 0; p < 2; p++) if (!s.Players[p].Decided) { var a = new BattleAction(p, "keep"); BattleEngine.Apply(s, a); log.Add(a); }
                start = due; continue;
            }
            var side = s.Active; var deadline = ArenaRules.Deadline(start, bank[side]);
            if (now <= deadline) break;
            var t = new BattleAction(side, "timeout"); BattleEngine.Apply(s, t); log.Add(t);
            bank[side] = 0; start = deadline;
        }
        if (log.Count == 0) return (s, m, log);
        m = await SaveAsync(m, s, log, start, bank, ct);
        return (s, m, log);
    }

    private async Task<Row> SaveAsync(Row m, BattleState s, List<BattleAction> added, DateTimeOffset turnStart, int[] bank, CancellationToken ct)
    {
        var all = m.Actions.Concat(added).ToList(); var finished = s.Phase == BattlePhase.Finished;
        m = m with { Actions = all, TurnStartedAt = turnStart, Bank = bank, Status = finished ? "Finished" : "InProgress", Winner = s.Result?.Winner, Reason = s.Result?.Reason };
        await uow.ExecAsync("UPDATE arena.match SET actions=@l::jsonb, status=@st, winner=@w, reason=@r, finished_at=@f, turn_started_at=@t, bank0_ms=@b0, bank1_ms=@b1 WHERE id=@i", ct,
            ("l", JsonSerializer.Serialize(all, J)), ("st", m.Status), ("w", m.Winner), ("r", m.Reason), ("f", finished ? clock.UtcNow : null), ("t", turnStart), ("b0", bank[0]), ("b1", bank[1]), ("i", m.Id));
        return finished ? await FinishAsync(m, ct) : m;
    }

    /// <summary>Cập nhật điểm Glicko-2 của hai bên và cộng AP cho người thắng; gọi đúng một lần khi trận vừa kết thúc.</summary>
    private async Task<Row> FinishAsync(Row m, CancellationToken ct)
    {
        var (g0, _, _) = await RatingAsync(m.P0, ct); var (g1, _, _) = await RatingAsync(m.P1, ct);
        var w = m.Winner!.Value;
        var n0 = Glicko2.Update(g0, g1, w == 0 ? 1 : 0); var n1 = Glicko2.Update(g1, g0, w == 1 ? 1 : 0);
        await SaveRatingAsync(m.P0, n0, w == 0, ct); await SaveRatingAsync(m.P1, n1, w == 1, ct);
        var winner = w == 0 ? m.P0 : m.P1;
        await uow.ExecAsync("INSERT INTO arena.ap(account_id,balance) VALUES(@a,@v) ON CONFLICT (account_id) DO UPDATE SET balance = arena.ap.balance + @v", ct, ("a", winner), ("v", (long)ArenaRules.WinAp));
        var (d0, d1) = (n0.Rating - g0.Rating, n1.Rating - g1.Rating);
        var (ap0, ap1) = (w == 0 ? ArenaRules.WinAp : 0, w == 1 ? ArenaRules.WinAp : 0);
        await uow.ExecAsync("UPDATE arena.match SET delta0=@d0, delta1=@d1, ap0=@a0, ap1=@a1 WHERE id=@i", ct, ("d0", d0), ("d1", d1), ("a0", ap0), ("a1", ap1), ("i", m.Id));
        return m with { Delta0 = d0, Delta1 = d1, Ap0 = ap0, Ap1 = ap1 };
    }

    // ---------- Xem và đánh trận ----------
    private object View(Row m, BattleState s, int viewer)
    {
        var finished = m.Status == "Finished"; var mine = m.Bank[viewer];
        var side = s.Phase == BattlePhase.Main ? s.Active : -1;
        var deadline = finished ? (DateTimeOffset?)null : s.Phase == BattlePhase.Mulligan ? m.CreatedAt.AddMilliseconds(ArenaRules.MulliganMs) : ArenaRules.Deadline(m.TurnStartedAt, m.Bank[s.Active]);
        var opp = viewer == 0 ? m.P1 : m.P0;
        var delta = viewer == 0 ? m.Delta0 : m.Delta1; var before = viewer == 0 ? m.Rating0 : m.Rating1;
        return BattleView.Build(m.Id, m.Status, viewer, s, m.Actions.Count, new Dictionary<string, object?>
        {
            ["mode"] = "ranked",
            ["opponentName"] = ArenaRules.Label(opp),
            ["clock"] = new { turnSeconds = ArenaRules.TurnMs / 1000, deadline, serverTime = clock.UtcNow, bankMs = new { you = mine, opponent = m.Bank[1 - viewer] }, active = side < 0 ? null : side == viewer ? "you" : "opponent" },
            ["rating"] = finished ? new { before = Math.Round(before), delta = delta is null ? (double?)null : Math.Round(delta.Value, 1), ap = viewer == 0 ? m.Ap0 : m.Ap1 } : null,
        });
    }

    public Task<object> GetAsync(Guid acc, Guid id, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        var m = await Load(id, true, ct); var viewer = Side(m, acc);
        var (s, m2, _) = await SettleAsync(m, ct);
        return View(m2, s, viewer);
    }, ct);

    public Task<object> ActAsync(Guid acc, Guid id, ArenaActionRequest req, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        var m = await Load(id, true, ct); var viewer = Side(m, acc);
        var (s, m2, _) = await SettleAsync(m, ct);                                                                  // hết giờ được xử lý trước khi nhận hành động mới
        m = m2;
        if (m.Status == "Finished") throw DomainException.Conflict(ErrorCodes.MatchFinished, "The match is over");
        var now = clock.UtcNow;
        var before = (Phase: s.Phase, Turn: s.TurnNumber, Active: s.Active);
        var a = new BattleAction(viewer, req.Type ?? "", req.Hand, req.Slot, req.Target, req.Id);
        if (a.Type == "memory") throw new DomainException("NOT_ALLOWED_IN_RANKED", "Sealed Memories cannot be used in ranked matches", 409);
        try { BattleEngine.Apply(s, a); }
        catch (BattleException e) { throw new DomainException(e.Code, e.Message, e.Code is "INVALID_ACTION" or "TARGET_INVALID" ? 400 : 409); }

        var start = m.TurnStartedAt; var bank = (int[])m.Bank.Clone();
        if (before.Phase == BattlePhase.Mulligan && s.Phase == BattlePhase.Main) start = now;                          // cả hai đã chốt tay: lượt đầu bắt đầu
        else if (before.Phase == BattlePhase.Main && s.TurnNumber != before.Turn) { bank[before.Active] = ArenaRules.BankAfterTurn(bank[before.Active], now - start); start = now; }
        m = await SaveAsync(m, s, [a], start, bank, ct);
        return View(m, s, viewer);
    }, ct);

    public async Task<object> HistoryAsync(Guid acc, CancellationToken ct) =>
        await uow.QueryAsync(@"SELECT id,p0,p1,status,winner,reason,created_at,delta0,delta1,ap0,ap1 FROM arena.match WHERE p0=@a OR p1=@a ORDER BY created_at DESC LIMIT 20",
            r =>
            {
                var viewer = r.GetGuid(1) == acc ? 0 : 1;
                var won = r.IsDBNull(4) ? (bool?)null : r.GetInt32(4) == viewer;
                var d = r.IsDBNull(7 + viewer) ? (double?)null : Math.Round(r.GetDouble(7 + viewer), 1);
                return (object)new { id = r.GetGuid(0), opponentName = ArenaRules.Label(viewer == 0 ? r.GetGuid(2) : r.GetGuid(1)), status = r.GetString(3), won, reason = r.IsDBNull(5) ? null : r.GetString(5), createdAt = r.GetFieldValue<DateTimeOffset>(6), ratingDelta = d, ap = r.GetInt32(9 + viewer) };
            }, ct, ("a", acc));

    public async Task<object> LeaderboardAsync(CancellationToken ct) =>
        await uow.QueryAsync("SELECT account_id,rating,games,wins FROM arena.rating WHERE games > 0 ORDER BY rating DESC, games DESC LIMIT 20",
            r => (object)new { name = ArenaRules.Label(r.GetGuid(0)), rating = Math.Round(r.GetDouble(1)), games = r.GetInt32(2), wins = r.GetInt32(3) }, ct);

    /// <summary>Phát lại (BR-BTL-10): sau khi trận kết thúc, lộ seed và bộ bài hai bên; server tự phát lại để đối chiếu.</summary>
    public async Task<object> ReplayAsync(Guid acc, Guid id, CancellationToken ct)
    {
        var m = await Load(id, false, ct); var viewer = Side(m, acc);
        if (m.Status != "Finished") throw DomainException.Conflict(ErrorCodes.InvalidState, "The replay is available after the match ends");
        var s = Rebuild(m);
        return new
        {
            id = m.Id,
            seed = Convert.ToHexString(m.Seed).ToLowerInvariant(),
            yourSide = viewer,
            decks = new[] { m.Cards0, m.Cards1 },
            actions = m.Actions,
            result = new { winner = m.Winner, reason = m.Reason },
            replayed = new { winner = s.Result?.Winner, reason = s.Result?.Reason },
        };
    }
}
