namespace Anima.Battle.Engine;

/// <summary>
/// Đối thủ máy cho chế độ luyện tập: tham lam và hoàn toàn tất định (không dùng ngẫu nhiên), nên trận luyện tập vẫn phát lại được.
/// Ra Anima đắt nhất đủ Cộng hưởng, rồi mỗi Anima tấn công: ưu tiên hạ được mục tiêu gây sát thương lớn nhất, không thì đánh mục tiêu mất nhiều HP nhất.
/// </summary>
public static class Bot
{
    /// <summary>Danh sách hành động cho cả lượt hiện tại của máy (kết thúc bằng "end"), tính trên bản sao trạng thái hiện tại của máy.</summary>
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

        // 2. Tấn công bằng Anima còn đánh được.
        for (var i = 0; i < BattlePlayer.Slots; i++)
        {
            if (p.Field[i] is not { Summoned: false, Attacked: false } a) continue;
            if (opp.FieldEmpty) return new BattleAction(me, "attack", Slot: i, Target: -1);
            int bestT = -1, bestScore = int.MinValue;
            for (var t = 0; t < BattlePlayer.Slots; t++)
            {
                if (opp.Field[t] is not { } d) continue;
                var dmg = BattleEngine.Damage(a.Card.Element, a.EffAtk, d.Card.Element, d.EffDef);
                var score = (dmg >= d.Hp ? 100_000 : 0) + dmg;
                if (score > bestScore) { bestScore = score; bestT = t; }
            }
            return new BattleAction(me, "attack", Slot: i, Target: bestT);
        }
        return new BattleAction(me, "end");
    }
}
