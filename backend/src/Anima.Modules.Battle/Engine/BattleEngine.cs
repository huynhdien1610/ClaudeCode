using Anima.Contracts;

namespace Anima.Battle.Engine;

public sealed record BattleSetup(byte[] Seed, IReadOnlyList<BattleCard> Deck0, IReadOnlyList<BattleCard> Deck1);

/// <summary>
/// Luật trận đấu thuần logic (BR-BTL, BR-ELM). Không đụng DB/đồng hồ/ngẫu nhiên hệ thống: cùng <see cref="BattleSetup"/> và cùng chuỗi
/// <see cref="BattleAction"/> luôn cho cùng kết quả (BR-BTL-10). Chưa có: kỹ năng thẻ, Tiếng vọng, Ký ức phong ấn, luật sàn (xem README).
/// </summary>
public static class BattleEngine
{
    public const int KeeperHp = 8000, OpeningHand = 5, MaxEnergy = 6, SuddenDeathFromTurn = 10, SuddenDeathStep = 500, MinDamage = 100, TimeoutsToLose = 3;

    // ---- Hệ (BR-ELM) ----
    private static readonly string[] Cycle = Elements.Cycle;

    private static int CycleIndex(string e) => Array.IndexOf(Cycle, e);

    /// <summary>Hệ <paramref name="a"/> sinh ra hệ <paramref name="b"/>: a đứng ngay trước b trong vòng sinh (BR-ELM-01).</summary>
    public static bool Generates(string a, string b)
    {
        var i = CycleIndex(a); var j = CycleIndex(b);
        return i >= 0 && j >= 0 && (i + 1) % Cycle.Length == j;
    }

    /// <summary>Hệ <paramref name="a"/> khắc hệ <paramref name="b"/>: cách một bước trong vòng sinh (BR-ELM-02).</summary>
    public static bool Counters(string a, string b)
    {
        var i = CycleIndex(a); var j = CycleIndex(b);
        return i >= 0 && j >= 0 && (i + 2) % Cycle.Length == j;
    }

    /// <summary>Hệ số sát thương theo phần nghìn: 1250, 750, 1100, 1500 hoặc 1000 (BR-ELM-03/04).</summary>
    public static int Multiplier(string attacker, string defender)
    {
        if (attacker == Elements.Nihilum) return defender == Elements.Nihilum ? 1000 : 1100;
        if (defender == Elements.Nihilum) return attacker == "Luminara" ? 1500 : 1000;
        if (Counters(attacker, defender)) return 1250;
        if (Counters(defender, attacker)) return 750;
        return 1000;
    }

    /// <summary>BR-BTL-05: max(100, ATK − DEF) × hệ số, làm tròn đến hàng chục (từ 5 trở lên làm tròn lên).</summary>
    public static int Damage(string attackerElement, int atk, string defenderElement, int def)
    {
        var raw = Math.Max(MinDamage, atk - def);
        var scaled = (long)raw * Multiplier(attackerElement, defenderElement);        // đơn vị 1/1000, tránh sai số số thực
        return (int)((scaled + 5000) / 10000 * 10);
    }

    // ---- Khởi tạo ----
    public static BattleState Start(BattleSetup setup)
    {
        var rng = new DeterministicRng(setup.Seed);
        var s = new BattleState { Players = [new BattlePlayer { Deck = [.. setup.Deck0] }, new BattlePlayer { Deck = [.. setup.Deck1] }], Rng = rng };
        rng.Shuffle(s.Players[0].Deck); rng.Shuffle(s.Players[1].Deck);
        s.First = rng.Next(2);                                                       // BR-BTL-02: thứ tự đi trước ngẫu nhiên
        s.Active = s.First;
        foreach (var p in s.Players) for (var i = 0; i < OpeningHand && p.Deck.Count > 0; i++) Draw(p);
        Emit(s, "start", s.First, ("first", s.First));
        return s;
    }

    private static void Draw(BattlePlayer p) { p.Hand.Add(p.Deck[0]); p.Deck.RemoveAt(0); }

    private static void Emit(BattleState s, string kind, int player, params (string, object?)[] data) =>
        s.Events.Add(new BattleEvent(s.Events.Count, kind, player, data.ToDictionary(d => d.Item1, d => d.Item2)));

    private static void Finish(BattleState s, int winner, string reason)
    {
        if (s.Phase == BattlePhase.Finished) return;
        s.Phase = BattlePhase.Finished; s.Result = new BattleResult(winner, reason);
        Emit(s, "finish", winner, ("reason", reason));
    }

    // ---- Áp dụng hành động ----
    public static void Apply(BattleState s, BattleAction a)
    {
        if (s.Phase == BattlePhase.Finished) throw new BattleException("MATCH_FINISHED", "The match is over");
        if (a.Player is not (0 or 1)) throw new BattleException("INVALID_ACTION", "Unknown player");

        if (a.Type == "concede") { Finish(s, 1 - a.Player, "CONCEDE"); s.ActionCount++; return; }          // BR-BTL-07
        if (s.Phase == BattlePhase.Mulligan) { ApplyMulligan(s, a); s.ActionCount++; return; }

        if (a.Type is "mulligan" or "keep") throw new BattleException(a.Type == "mulligan" ? "MULLIGAN_USED" : "INVALID_ACTION", "The opening hand is already settled");
        if (a.Player != s.Active) throw new BattleException("NOT_YOUR_TURN", "It is not your turn");
        var me = s.Me;
        if (a.Type == "timeout")
        {
            me.ConsecutiveTimeouts++;
            Emit(s, "timeout", a.Player, ("count", me.ConsecutiveTimeouts));
            if (me.ConsecutiveTimeouts >= TimeoutsToLose) Finish(s, 1 - a.Player, "TIMEOUT");              // BR-BTL-08
            else EndTurn(s);
            s.ActionCount++; return;
        }
        me.ConsecutiveTimeouts = 0;
        switch (a.Type)
        {
            case "play": Play(s, a); break;
            case "attack": Attack(s, a); break;
            case "end": EndTurn(s); break;
            default: throw new BattleException("INVALID_ACTION", $"Unknown action '{a.Type}'");
        }
        s.ActionCount++;
    }

    private static void ApplyMulligan(BattleState s, BattleAction a)
    {
        var p = s.Players[a.Player];
        if (a.Type is not ("mulligan" or "keep")) throw new BattleException("INVALID_ACTION", "Settle your opening hand first");
        if (a.Type == "mulligan" && p.MulliganUsed) throw new BattleException("MULLIGAN_USED", "You already mulliganed");
        if (p.Decided) throw new BattleException("INVALID_ACTION", "You already settled your opening hand");
        if (a.Type == "mulligan")
        {
            // Trả cả tay, xào lại, bốc lại đúng ngần ấy lá (BR-BTL-02).
            var n = p.Hand.Count;
            p.Deck.AddRange(p.Hand); p.Hand.Clear();
            s.Rng.Shuffle(p.Deck);
            for (var i = 0; i < n; i++) Draw(p);
            p.MulliganUsed = true;
        }
        p.Decided = true;
        Emit(s, a.Type, a.Player);
        if (s.Players.All(x => x.Decided))
        {
            var second = s.Players[1 - s.First];
            if (second.Deck.Count > 0) Draw(second);                                                      // Q-55: người đi sau bốc thêm 1 lá
            s.Phase = BattlePhase.Main;
            StartTurn(s, s.First);
        }
    }

    // ---- Lượt ----
    private static void StartTurn(BattleState s, int player)
    {
        s.Active = player; s.TurnNumber++;
        var p = s.Players[player];
        p.OwnTurns++;
        p.Energy = Math.Min(p.OwnTurns, MaxEnergy);                                                       // BR-BTL-03: không cộng dồn
        p.ChainUsed = false;
        foreach (var f in p.Field) if (f is not null) { f.Summoned = false; f.Attacked = false; }
        Emit(s, "turn", player, ("n", p.OwnTurns), ("energy", p.Energy));

        if (p.Deck.Count == 0) { Finish(s, 1 - player, "DECK_OUT"); return; }                             // BR-BTL-07: phải bốc khi hết bài
        Draw(p);

        if (p.OwnTurns >= SuddenDeathFromTurn)                                                            // BR-BTL-09
        {
            var loss = SuddenDeathStep * (p.OwnTurns - SuddenDeathFromTurn + 1);
            p.Keeper -= loss;
            Emit(s, "sudden_death", player, ("loss", loss), ("keeper", p.Keeper));
            if (p.Keeper <= 0) Finish(s, 1 - player, "SUDDEN_DEATH");
        }
    }

    private static void EndTurn(BattleState s)
    {
        foreach (var f in s.Me.Field) if (f is not null) f.TempAtk = 0;
        Emit(s, "end", s.Active);
        StartTurn(s, 1 - s.Active);
    }

    // ---- Ra sân ----
    private static void Play(BattleState s, BattleAction a)
    {
        var me = s.Me;
        if (a.Hand is not { } hi || hi < 0 || hi >= me.Hand.Count || a.Slot is not { } slot || slot < 0 || slot >= BattlePlayer.Slots)
            throw new BattleException("INVALID_ACTION", "Pick a card in your hand and a slot");
        var card = me.Hand[hi];
        if (card.Type != CardTypes.Anima) throw new BattleException("CARD_NOT_PLAYABLE", "Only Anima can be played in this version");
        if (me.Field[slot] is not null) throw new BattleException("SLOT_OCCUPIED", "That slot is occupied");
        if (card.Cost > me.Energy) throw new BattleException("INSUFFICIENT_RESONANCE", "Not enough Resonance");

        me.Energy -= card.Cost; me.Hand.RemoveAt(hi);
        var f = new FieldAnima { Card = card, Atk = card.Atk, Def = card.Def, Hp = card.Hp, MaxHp = card.Hp, Summoned = true };
        // BR-ELM-05: khi ra sân mà phe mình có Anima thuộc hệ sinh ra nó → +200 ATK và +200 HP đến hết trận. Nihilum không nhận.
        var generated = card.Element != Elements.Nihilum && me.Field.Any(x => x is not null && Generates(x.Card.Element, card.Element));
        if (generated) { f.Atk += 200; f.Hp += 200; f.MaxHp += 200; }
        me.Field[slot] = f;
        Emit(s, "play", s.Active, ("card", card.Name), ("slot", slot), ("generated", generated));
    }

    // ---- Tấn công ----
    private static void Attack(BattleState s, BattleAction a)
    {
        var me = s.Me; var opp = s.Opp;
        if (a.Slot is not { } si || si < 0 || si >= BattlePlayer.Slots || me.Field[si] is not { } atk) throw new BattleException("INVALID_ACTION", "Pick one of your Anima");
        if (a.Target is not { } target || target < -1 || target >= BattlePlayer.Slots) throw new BattleException("INVALID_ACTION", "Pick a target");
        if (atk.Summoned || atk.Attacked) throw new BattleException("ATTACK_NOT_ALLOWED", atk.Summoned ? "This Anima entered this turn" : "This Anima already attacked this turn");   // BR-BTL-04

        TryChain(s);                                                                                       // BR-ELM-06: kiểm ở đầu giai đoạn tấn công
        atk.Attacked = true;
        if (target == -1)
        {
            if (!opp.FieldEmpty) { atk.Attacked = false; throw new BattleException("DIRECT_ATTACK_NOT_ALLOWED", "Defeat the opposing Anima first"); }
            opp.Keeper -= atk.EffAtk;
            Emit(s, "attack_keeper", s.Active, ("slot", si), ("damage", atk.EffAtk), ("keeper", opp.Keeper));
            if (opp.Keeper <= 0) Finish(s, s.Active, "KEEPER_DOWN");
            return;
        }
        if (opp.Field[target] is not { } def) { atk.Attacked = false; throw new BattleException("INVALID_ACTION", "There is no Anima in that slot"); }
        var dmg = Damage(atk.Card.Element, atk.EffAtk, def.Card.Element, def.EffDef);
        def.Hp -= dmg;
        Emit(s, "attack", s.Active, ("slot", si), ("target", target), ("damage", dmg), ("hp", Math.Max(0, def.Hp)));
        if (def.Hp <= 0) { opp.Graveyard.Add(def.Card); opp.Field[target] = null; Emit(s, "defeated", 1 - s.Active, ("card", def.Card.Name), ("slot", target)); }   // BR-BTL-06
    }

    /// <summary>BR-ELM-06: 3 Anima thuộc 3 hệ liên tiếp trong vòng sinh → cả 3 +300 ATK đến hết lượt; tối đa 1 lần mỗi lượt.</summary>
    private static void TryChain(BattleState s)
    {
        var me = s.Me;
        if (me.ChainUsed) return;
        var present = me.Field.Where(f => f is not null && CycleIndex(f.Card.Element) >= 0).Select(f => f!).ToList();
        for (var start = 0; start < Cycle.Length; start++)
        {
            var three = Enumerable.Range(0, 3).Select(k => Cycle[(start + k) % Cycle.Length]).ToList();
            var members = three.Select(e => present.FirstOrDefault(f => f.Card.Element == e)).ToList();
            if (members.Any(m => m is null)) continue;
            foreach (var m in members) m!.TempAtk += 300;
            me.ChainUsed = true;
            Emit(s, "chain", s.Active, ("elements", three));
            return;
        }
    }
}
