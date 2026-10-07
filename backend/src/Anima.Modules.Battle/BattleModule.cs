using System.Security.Claims;
using System.Text.Json;
using Anima.Battle.Engine;
using Anima.Catalog;
using Anima.Contracts;
using Anima.Deck;
using Anima.SharedKernel;

namespace Anima.Battle;

public sealed record PracticeRequest(Guid? DeckId);
public sealed record ActionRequest(string? Type, int? Hand, int? Slot, int? Target);

public sealed class BattleModule : IModule
{
    public string Name => "battle";
    public System.Reflection.Assembly MigrationAssembly => typeof(BattleModule).Assembly;

    public void ConfigureServices(IServiceCollection s, IConfiguration c) => s.AddScoped<BattleService>();

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/battles").RequireAuthorization();
        g.MapPost("/practice", async (ClaimsPrincipal u, PracticeRequest r, BattleService s, CancellationToken ct) => Results.Created("/v1/battles", await s.StartPracticeAsync(u.AccountId(), r.DeckId, ct)));
        g.MapGet("", async (ClaimsPrincipal u, BattleService s, CancellationToken ct) => Results.Ok(await s.ListAsync(u.AccountId(), ct)));
        g.MapGet("/{id:guid}", async (ClaimsPrincipal u, Guid id, BattleService s, CancellationToken ct) => Results.Ok(await s.GetAsync(u.AccountId(), id, ct)));
        g.MapPost("/{id:guid}/actions", async (ClaimsPrincipal u, Guid id, ActionRequest a, BattleService s, CancellationToken ct) => Results.Ok(await s.ActAsync(u.AccountId(), id, a, ct)));
        g.MapGet("/{id:guid}/replay", async (ClaimsPrincipal u, Guid id, BattleService s, CancellationToken ct) => Results.Ok(await s.ReplayAsync(u.AccountId(), id, ct)));
    }
}

public sealed class BattleService(IUnitOfWork uow, IClock clock, ISecureRandom random, IDeckApi decks, ICatalogApi catalog)
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);
    private const int Human = 0, BotSide = 1;

    private sealed record MatchRow(Guid Id, Guid AccountId, string Status, byte[] Seed, List<BattleCard> Player, List<BattleCard> BotCards, List<BattleAction> Actions, int? Winner, string? Reason, DateTimeOffset CreatedAt);

    private const string Cols = "id,account_id,status,seed,player_cards::text,bot_cards::text,actions::text,winner,reason,created_at";
    private static MatchRow Map(Npgsql.NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetFieldValue<byte[]>(3),
        JsonSerializer.Deserialize<List<BattleCard>>(r.GetString(4), J)!, JsonSerializer.Deserialize<List<BattleCard>>(r.GetString(5), J)!,
        JsonSerializer.Deserialize<List<BattleAction>>(r.GetString(6), J)!, r.IsDBNull(7) ? null : r.GetInt32(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetFieldValue<DateTimeOffset>(9));

    private async Task<MatchRow> Load(Guid acc, Guid id, bool forUpdate, CancellationToken ct) =>
        (await uow.QueryAsync($"SELECT {Cols} FROM battle.match WHERE id=@i AND account_id=@a" + (forUpdate ? " FOR UPDATE" : ""), Map, ct, ("i", id), ("a", acc))).FirstOrDefault()
        ?? throw DomainException.NotFound("Match");

    private static BattleCard ToBattle(CardDefinition d) => new(d.Id, d.Name, d.Element, d.CardType, d.Rarity, d.Cost, d.Atk ?? 0, d.Def ?? 0, d.Hp ?? 0);

    /// <summary>Bộ bài của máy: 15 Anima khác nhau (Common → Rare) × 2 bản, chọn tất định từ seed.</summary>
    private async Task<List<BattleCard>> BotDeckAsync(byte[] seed, CancellationToken ct)
    {
        var pool = (await catalog.ListCardsAsync(ct)).Where(d => d.CardType == CardTypes.Anima && d.Rarity is "common" or "uncommon" or "rare" && d.Hp > 0).OrderBy(d => d.Id).ToList();
        if (pool.Count < 15) throw new InvalidOperationException("The catalog has too few Anima for the practice opponent");
        new DeterministicRng([.. seed, 0xB0]).Shuffle(pool);
        return pool.Take(15).SelectMany(d => new[] { ToBattle(d), ToBattle(d) }).ToList();
    }

    private static BattleState Rebuild(MatchRow m)
    {
        var s = BattleEngine.Start(new BattleSetup(m.Seed, m.Player, m.BotCards));
        foreach (var a in m.Actions) BattleEngine.Apply(s, a);
        return s;
    }

    /// <summary>Máy chơi cho tới khi tới lượt người chơi (hoặc hết trận); ghi từng hành động của máy vào nhật ký.</summary>
    private static void RunBot(BattleState s, List<BattleAction> log)
    {
        for (var guard = 0; guard < 500 && s.Phase != BattlePhase.Finished; guard++)
        {
            var botTurn = s.Phase == BattlePhase.Mulligan ? !s.Players[BotSide].Decided : s.Active == BotSide;
            if (!botTurn) return;
            var a = Bot.Next(s, BotSide);
            BattleEngine.Apply(s, a); log.Add(a);
        }
    }

    public Task<object> StartPracticeAsync(Guid acc, Guid? deckId, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        if (deckId is null) throw DomainException.Validation("deckId is required");
        var cards = await decks.RequireBattleReadyAsync(acc, deckId.Value, ct);                  // BR-DECK-07
        var defs = (await catalog.ListCardsAsync(ct)).ToDictionary(d => d.Id);
        var player = cards.Select(c => ToBattle(defs[c.CardDefinitionId])).ToList();
        var seed = random.Bytes(32);
        var bot = await BotDeckAsync(seed, ct);

        var m = new MatchRow(Guid.NewGuid(), acc, "InProgress", seed, player, bot, [], null, null, clock.UtcNow);
        var s = BattleEngine.Start(new BattleSetup(seed, player, bot));
        var log = new List<BattleAction>();
        RunBot(s, log);                                                                           // máy giữ tay đầu ngay
        try
        {
            await uow.ExecAsync(@"INSERT INTO battle.match(id,account_id,mode,deck_id,status,seed,player_cards,bot_cards,actions,created_at)
                VALUES(@i,@a,'practice',@d,'InProgress',@s,@p::jsonb,@b::jsonb,@l::jsonb,@now)", ct,
                ("i", m.Id), ("a", acc), ("d", deckId), ("s", seed), ("p", JsonSerializer.Serialize(player, J)), ("b", JsonSerializer.Serialize(bot, J)), ("l", JsonSerializer.Serialize(log, J)), ("now", m.CreatedAt));
        }
        catch (Npgsql.PostgresException e) when (e.SqlState == "23505") { throw DomainException.Conflict("MATCH_IN_PROGRESS", "Finish or concede your current practice match first"); }
        return View(m with { Actions = log }, s);
    }, ct);

    public async Task<object> GetAsync(Guid acc, Guid id, CancellationToken ct)
    {
        var m = await Load(acc, id, false, ct);
        return View(m, Rebuild(m));
    }

    public async Task<object> ListAsync(Guid acc, CancellationToken ct) =>
        await uow.QueryAsync("SELECT id,status,winner,reason,created_at,finished_at FROM battle.match WHERE account_id=@a ORDER BY created_at DESC LIMIT 20",
            r => (object)new { id = r.GetGuid(0), status = r.GetString(1), won = r.IsDBNull(2) ? (bool?)null : r.GetInt32(2) == Human, reason = r.IsDBNull(3) ? null : r.GetString(3), createdAt = r.GetFieldValue<DateTimeOffset>(4), finishedAt = r.IsDBNull(5) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(5) }, ct, ("a", acc));

    public Task<object> ActAsync(Guid acc, Guid id, ActionRequest req, CancellationToken ct) => uow.RunAsync<object>(async () =>
    {
        var m = await Load(acc, id, true, ct);                                                     // khóa dòng: hai yêu cầu đồng thời xếp hàng
        if (m.Status == "Finished") throw DomainException.Conflict(ErrorCodes.MatchFinished, "The match is over");
        var s = Rebuild(m);
        var log = new List<BattleAction>();
        try
        {
            var a = new BattleAction(Human, req.Type ?? "", req.Hand, req.Slot, req.Target);
            BattleEngine.Apply(s, a); log.Add(a);
            RunBot(s, log);
        }
        catch (BattleException e) { throw new DomainException(e.Code, e.Message, e.Code == "INVALID_ACTION" ? 400 : 409); }

        var all = m.Actions.Concat(log).ToList();
        await uow.ExecAsync("UPDATE battle.match SET actions=@l::jsonb, status=@st, winner=@w, reason=@r, finished_at=@f WHERE id=@i", ct,
            ("l", JsonSerializer.Serialize(all, J)), ("st", s.Phase == BattlePhase.Finished ? "Finished" : "InProgress"), ("w", s.Result?.Winner), ("r", s.Result?.Reason),
            ("f", s.Phase == BattlePhase.Finished ? clock.UtcNow : null), ("i", id));
        return View(m with { Status = s.Phase == BattlePhase.Finished ? "Finished" : "InProgress", Actions = all, Winner = s.Result?.Winner, Reason = s.Result?.Reason }, s);
    }, ct);

    /// <summary>Dữ liệu phát lại (BR-BTL-10): chỉ sau khi trận kết thúc (seed không lộ khi còn đang đánh). Kèm kết quả server tự phát lại để đối chiếu.</summary>
    public async Task<object> ReplayAsync(Guid acc, Guid id, CancellationToken ct)
    {
        var m = await Load(acc, id, false, ct);
        if (m.Status != "Finished") throw DomainException.Conflict(ErrorCodes.InvalidState, "The replay is available after the match ends");
        var s = Rebuild(m);
        return new
        {
            id = m.Id,
            seed = Convert.ToHexString(m.Seed).ToLowerInvariant(),
            playerDeck = m.Player,
            opponentDeck = m.BotCards,
            actions = m.Actions,
            result = new { won = m.Winner == Human, reason = m.Reason },
            replayed = new { won = s.Result?.Winner == Human, reason = s.Result?.Reason, keeper = s.Players.Select(p => p.Keeper) },
        };
    }

    // ---- Góc nhìn của người chơi: giấu tay và bộ bài đối thủ ----
    private static object Anima(FieldAnima? f) => f is null ? null! : new { name = f.Card.Name, element = f.Card.Element, defId = f.Card.DefId, atk = f.EffAtk, def = f.EffDef, hp = f.Hp, maxHp = f.MaxHp, canAttack = !f.Summoned && !f.Attacked, summoned = f.Summoned };

    private static object View(MatchRow m, BattleState s)
    {
        var me = s.Players[Human]; var opp = s.Players[BotSide];
        return new
        {
            id = m.Id,
            status = m.Status,
            phase = s.Phase.ToString().ToLowerInvariant(),
            yourTurn = s.Phase == BattlePhase.Main && s.Active == Human,
            turn = me.OwnTurns,
            firstPlayer = s.First == Human ? "you" : "opponent",
            you = new { keeper = me.Keeper, energy = me.Energy, deck = me.Deck.Count, mulliganAvailable = s.Phase == BattlePhase.Mulligan && !me.Decided && !me.MulliganUsed, decided = me.Decided, hand = me.Hand.Select((c, i) => new { index = i, c.DefId, c.Name, c.Element, c.Type, c.Rarity, c.Cost, c.Atk, c.Def, c.Hp }), field = me.Field.Select(Anima), graveyard = me.Graveyard.Count },
            opponent = new { keeper = opp.Keeper, hand = opp.Hand.Count, deck = opp.Deck.Count, field = opp.Field.Select(Anima), graveyard = opp.Graveyard.Count },
            result = s.Result is null ? null : new { won = s.Result.Winner == Human, reason = s.Result.Reason },
            events = s.Events.TakeLast(40).Select(e => new { e.Seq, e.Kind, side = e.Player == Human ? "you" : "opponent", data = e.Data }),
            actionCount = m.Actions.Count,
        };
    }
}
