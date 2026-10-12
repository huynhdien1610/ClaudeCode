using Anima.Battle.Engine;
using Anima.Catalog;

namespace Anima.Battle;

/// <summary>Góc nhìn của một người chơi lên trận: giấu tay và bộ bài đối thủ (dùng chung cho trận luyện tập và PvP).</summary>
public static class BattleView
{
    public static object? Anima(FieldAnima? f)
    {
        if (f is null) return null;
        var def = SkillCatalog.Find(f.SkillId);
        var ult = SkillCatalog.UltimateOf(f.Card.Element);
        return new
        {
            name = f.Card.Name,
            element = f.Card.Element,
            defId = f.Card.DefId,
            atk = f.EffAtk,
            def = f.EffDef,
            hp = f.Hp,
            maxHp = f.MaxHp,
            canAttack = !f.Summoned && !f.Attacked && !f.Statuses.Any(x => x.Kind == "stun"),
            summoned = f.Summoned,
            skill = def is null ? null : new { id = def.Id, name = def.Name, kind = def.Kind.ToString().ToLowerInvariant(), target = def.Target.ToString().ToLowerInvariant(), cooldown = f.Cd.GetValueOrDefault(def.Id), cooldownMax = def.Cooldown },
            ultimate = new { name = ult.Name, target = ult.Target.ToString().ToLowerInvariant() },
            feared = f.Statuses.Any(x => x.Kind == "fear"),
            dodge = f.DodgePct,
            echo = f.IsEcho ? (int?)f.EchoTurns : null,
            borrowed = f.BorrowedSkill is null ? null : SkillCatalog.Find(f.BorrowedSkill)?.Name,
            statuses = f.Statuses.Select(x => new { kind = x.Kind, turns = x.Turns >= 90 ? (int?)null : x.Turns, value = x.Value, stacks = x.Stacks }),
        };
    }

    /// <summary>
    /// Trạng thái trận nhìn từ phía <paramref name="viewer"/>: tay và bộ bài của đối thủ bị giấu, "you" luôn là người đang xem.
    /// <paramref name="extra"/> là các trường riêng của chế độ (đồng hồ lượt, tên đối thủ...) gộp vào cùng cấp.
    /// </summary>
    public static IDictionary<string, object?> Build(Guid id, string status, int viewer, BattleState s, int actionCount, IReadOnlyDictionary<string, object?>? extra = null)
    {
        var me = s.Players[viewer]; var opp = s.Players[1 - viewer];
        var fusions = SkillCatalog.All.Where(d => d.Kind == SkillKind.Fusion).Select(d =>
        {
            var (a, b) = SkillCatalog.FusionElements[d.Id];
            var onField = me.Field.Any(f => f?.Card.Element == a) && me.Field.Any(f => f?.Card.Element == b);
            if (d.Id == "hopes_last_stand") onField &= me.Field.Where(f => f is not null).Sum(f => f!.Hp) * 100 < me.Field.Where(f => f is not null).Sum(f => f!.MaxHp) * 30;
            return new { id = d.Id, name = d.Name, elements = new[] { a, b }, target = d.Target.ToString().ToLowerInvariant(), used = me.TeamUsed.Contains("fusion:" + d.Id), available = onField && !me.TeamUsed.Contains("fusion:" + d.Id) && !me.FusionThisTurn };
        });
        var view = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["status"] = status,
            ["phase"] = s.Phase.ToString().ToLowerInvariant(),
            ["yourTurn"] = s.Phase == BattlePhase.Main && s.Active == viewer,
            ["turn"] = me.OwnTurns,
            ["firstPlayer"] = s.First == viewer ? "you" : "opponent",
            ["meter"] = s.Meter,
            ["surge"] = new { you = me.SurgeLeft, opponent = opp.SurgeLeft },
            ["you"] = new { memories = me.Memories.GroupBy(x => x.DefId).Select(g => new { defId = g.Key, name = g.First().Name, element = g.First().Element, skill = SkillCatalog.Find(g.First().Skill)?.Name, qty = g.Count() }), memoryReady = s.MemoryAllowed && !me.MemoryThisTurn && me.MemoryCd == 0, memoryCd = me.MemoryCd, keeper = me.Keeper, energy = me.Energy, deck = me.Deck.Count, mulliganAvailable = s.Phase == BattlePhase.Mulligan && !me.Decided && !me.MulliganUsed, decided = me.Decided, hand = me.Hand.Select((c, i) => new { index = i, c.DefId, c.Name, c.Element, c.Type, c.Rarity, c.Cost, c.Atk, c.Def, c.Hp }), field = me.Field.Select(Anima), graveyard = me.Graveyard.Count, fusions },
            ["opponent"] = new { keeper = opp.Keeper, hand = opp.Hand.Count, deck = opp.Deck.Count, field = opp.Field.Select(Anima), graveyard = opp.Graveyard.Count },
            ["result"] = s.Result is null ? null : new { won = s.Result.Winner == viewer, reason = s.Result.Reason },
            ["events"] = s.Events.TakeLast(60).Select(e => new { e.Seq, e.Kind, side = e.Player == viewer ? "you" : "opponent", data = e.Data }),
            ["actionCount"] = actionCount,
        };
        if (extra is not null) foreach (var (k, v) in extra) view[k] = v;
        return view;
    }
}

/// <summary>Chốt chỉ số thẻ lúc vào trận (thẻ phát hành là bất biến, BR-CARD-06).</summary>
public static class BattleCards
{
    public static BattleCard From(CardDefinition d) => new(d.Id, d.Name, d.Element, d.CardType, d.Rarity, d.Cost, d.Atk ?? 0, d.Def ?? 0, d.Hp ?? 0, d.Skill);
}
