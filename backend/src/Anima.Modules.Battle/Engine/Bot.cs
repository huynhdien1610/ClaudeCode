namespace Anima.Battle.Engine;

/// <summary>
/// Đối thủ máy cho chế độ luyện tập: tham lam và hoàn toàn tất định (không dùng ngẫu nhiên), nên trận luyện tập vẫn phát lại được.
/// Thứ tự ưu tiên: ra Anima đắt nhất đủ Cộng hưởng → Fusion khi dùng được → Ultimate khi đủ Meter → Active đã sẵn sàng → đòn thường.
/// </summary>
public static class Bot
{
    public static BattleAction Next(BattleState s, int me)
    {
        if (s.Phase == BattlePhase.Mulligan) return new BattleAction(me, "keep");
        var p = s.Players[me]; var opp = s.Players[1 - me];

        // 1. Ra sân: Anima đắt nhất mà đủ Cộng hưởng, vào ô trống đầu tiên.
        var free = Array.FindIndex(p.Field, f => f is null);
        if (free >= 0)
        {
            var best = -1;
            for (var i = 0; i < p.Hand.Count; i++)
                if (p.Hand[i].Type == "anima" && p.Hand[i].Cost <= p.Energy && (best < 0 || p.Hand[i].Cost > p.Hand[best].Cost)) best = i;
            if (best >= 0) return new BattleAction(me, "play", Hand: best, Slot: free);
        }

        // 2. Fusion (không tốn hành động của Anima).
        if (!p.FusionThisTurn && !opp.FieldEmpty)
            foreach (var (id, (a, b)) in SkillCatalog.FusionElements)
            {
                if (p.TeamUsed.Contains("fusion:" + id) || id == "hopes_last_stand") continue;
                if (!p.Field.Any(f => f?.Card.Element == a) || !p.Field.Any(f => f?.Card.Element == b)) continue;
                return new BattleAction(me, "fusion", Id: id, Target: id == "voids_edge" ? EnemyTarget(opp) : null);
            }

        // 3. Mỗi Anima còn hành động: Ultimate → Active → đòn thường.
        for (var i = 0; i < BattlePlayer.Slots; i++)
        {
            if (p.Field[i] is not { Summoned: false, Attacked: false } a || a.Statuses.Any(x => x.Kind == "stun")) continue;
            if (s.Meter >= BattleEngine.UltimateCost && !a.Statuses.Any(x => x.Kind == "fear") && !opp.FieldEmpty)
            {
                var u = SkillCatalog.UltimateOf(a.Card.Element);
                var target = u.Target switch { SkillTarget.Enemy => EnemyTarget(opp), SkillTarget.Ally => Weakest(p), _ => 0 };
                if (u.Target != SkillTarget.Ally || (target >= 0 && p.Field[target]!.Hp * 100 < p.Field[target]!.MaxHp * 60))
                    return new BattleAction(me, "ultimate", Slot: i, Target: target);
            }
            if (SkillCatalog.Find(a.SkillId) is { Kind: SkillKind.Active } d && a.Cd.GetValueOrDefault(d.Id) == 0 && !opp.FieldEmpty)
            {
                if (d.Target == SkillTarget.Enemy) return new BattleAction(me, "skill", Slot: i, Target: EnemyTarget(opp));
                if (d.Target is SkillTarget.Self or SkillTarget.AllEnemies) return new BattleAction(me, "skill", Slot: i, Target: i);
                if (d.Id == "luminara.dawns_embrace" && Weakest(p) is var w and >= 0 && p.Field[w]!.Hp * 100 < p.Field[w]!.MaxHp * 70) return new BattleAction(me, "skill", Slot: i, Target: w);
            }
            if (opp.FieldEmpty) return new BattleAction(me, "attack", Slot: i, Target: -1);
            return new BattleAction(me, "attack", Slot: i, Target: AttackTarget(a, opp));
        }
        return new BattleAction(me, "end");
    }

    /// <summary>Có Taunt thì buộc đánh Anima đó; không thì Anima ít HP nhất.</summary>
    private static int EnemyTarget(BattlePlayer opp)
    {
        int best = -1;
        for (var t = 0; t < BattlePlayer.Slots; t++)
        {
            if (opp.Field[t] is not { } d) continue;
            if (d.Statuses.Any(x => x.Kind == "taunt")) return t;
            if (best < 0 || d.Hp < opp.Field[best]!.Hp) best = t;
        }
        return best;
    }

    private static int Weakest(BattlePlayer p)
    {
        int best = -1;
        for (var t = 0; t < BattlePlayer.Slots; t++)
            if (p.Field[t] is { } d && (best < 0 || d.Hp * p.Field[best]!.MaxHp < p.Field[best]!.Hp * d.MaxHp)) best = t;
        return best;
    }

    /// <summary>Đòn thường: ưu tiên mục tiêu hạ được, rồi mục tiêu chịu nhiều sát thương nhất; có Taunt thì buộc đánh Taunt.</summary>
    private static int AttackTarget(FieldAnima a, BattlePlayer opp)
    {
        int bestT = -1, bestScore = int.MinValue;
        for (var t = 0; t < BattlePlayer.Slots; t++)
        {
            if (opp.Field[t] is not { } d) continue;
            if (d.Statuses.Any(x => x.Kind == "taunt")) return t;
            var dmg = BattleEngine.Damage(a.Card.Element, a.EffAtk, d.Card.Element, d.EffDef);
            var score = (dmg >= d.Hp ? 100_000 : 0) + dmg;
            if (score > bestScore) { bestScore = score; bestT = t; }
        }
        return bestT;
    }
}
