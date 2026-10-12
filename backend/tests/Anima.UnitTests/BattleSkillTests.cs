using Anima.Battle.Engine;
using Xunit;

namespace Anima.UnitTests;

/// <summary>Kỹ năng, Emotion Meter, Resonance, Ultimate, Fusion, tương khắc ±20% (docs/SKILLS_ANIMA.md).</summary>
public class BattleSkillTests
{
    private static BattleCard Card(string el, string? skill = null, int atk = 1000, int def = 500, int hp = 3000, int cost = 1, string name = "x") =>
        new(1, name, el, "anima", "common", cost, atk, def, hp, skill);

    private static BattleState Playing(byte seed = 1, List<BattleCard>? d0 = null, List<BattleCard>? d1 = null)
    {
        List<BattleCard> plain() => Enumerable.Range(0, 30).Select(i => Card(Anima.Contracts.Elements.Cycle[i % 7], name: $"c{i}")).ToList();
        var s = BattleEngine.Start(new BattleSetup([seed], d0 ?? plain(), d1 ?? plain()));
        BattleEngine.Apply(s, new BattleAction(0, "keep")); BattleEngine.Apply(s, new BattleAction(1, "keep"));
        return s;
    }

    private static FieldAnima Put(BattleState s, int side, int slot, BattleCard c, bool summoned = false)
    {
        var f = new FieldAnima { Card = c, Uid = s.NextUid++, Atk = c.Atk, Def = c.Def, Hp = c.Hp, MaxHp = c.Hp, BaseMaxHp = c.Hp, Summoned = summoned };
        s.Players[side].Field[slot] = f; BattleEngine.Recalc(s); return f;
    }

    private static int Me(BattleState s) => s.Active;
    private static int Foe(BattleState s) => 1 - s.Active;
    private static void Do(BattleState s, string type, int slot, int? target = null, string? id = null, int? player = null) =>
        BattleEngine.Apply(s, new BattleAction(player ?? s.Active, type, Slot: slot, Target: target, Id: id));
    private static string Fails(BattleState s, string type, int slot, int? target = null, string? id = null) =>
        Assert.Throws<BattleException>(() => Do(s, type, slot, target, id)).Code;
    private static void End(BattleState s) => BattleEngine.Apply(s, new BattleAction(s.Active, "end"));
    private static void Meter(BattleState s, int v) => s.Meter = v;

    // ---------- Tương khắc ±20% (bổ sung) ----------
    [Theory(DisplayName = "Tương khắc kỹ năng: người dùng khắc mục tiêu +20%, bị khắc −20%, Luminara khắc Nihilum +20%, còn lại 0")]
    [InlineData("Umbryx", "Aqualis", 0.2)]
    [InlineData("Aqualis", "Umbryx", -0.2)]
    [InlineData("Luminara", "Nihilum", 0.2)]
    [InlineData("Nihilum", "Luminara", 0.0)]
    [InlineData("Umbryx", "Terrakin", 0.0)]
    [InlineData("Pyraxis", "Pyraxis", 0.0)]
    [InlineData("Voltaris", "Umbryx", 0.2)]
    [InlineData("Umbryx", "Voltaris", -0.2)]
    public void ElementMod(string src, string tgt, double expected) => Assert.Equal(expected, BattleEngine.ElementMod(src, tgt), 6);

    [Theory(DisplayName = "Silent Tear (25 → 250): sát thương theo tương khắc ±20% rồi trừ DEF×10%")]
    [InlineData("Aqualis", 2750)]   // khắc: 300 − 50 = 250
    [InlineData("Terrakin", 2800)]  // trung tính: 250 − 50 = 200
    [InlineData("Voltaris", 2850)]  // bị khắc: 200 − 50 = 150
    public void SkillDamageUsesCounterModifier(string targetElement, int hpAfter)
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Umbryx", "umbryx.silent_tear")); var t = Put(s, Foe(s), 0, Card(targetElement, hp: 3000, def: 500));
        Do(s, "skill", 0, 0);
        Assert.Equal(hpAfter, t.Hp);
    }

    [Fact(DisplayName = "Tương khắc cũng chỉnh độ lớn debuff: Blinding Light −20% ATK thành −24% khi khắc, −16% khi bị khắc")]
    public void CounterScalesDebuffMagnitude()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara", "luminara.blinding_light")); var t = Put(s, Foe(s), 0, Card("Nihilum", atk: 1000));
        Do(s, "skill", 0, 0);                                                  // Luminara khắc Nihilum: ATK −24%
        Assert.Equal(760, t.EffAtk);
        var s2 = Playing(); Put(s2, Me(s2), 0, Card("Luminara", "luminara.blinding_light")); var t2 = Put(s2, Foe(s2), 0, Card("Pyraxis", atk: 1000));
        Do(s2, "skill", 0, 0);                                                 // Pyraxis khắc... Luminara khắc Pyraxis? (Luminara→Pyraxis +20%)
        Assert.Equal(760, t2.EffAtk);
        var s3 = Playing(); Put(s3, Me(s3), 0, Card("Luminara", "luminara.blinding_light")); var t3 = Put(s3, Foe(s3), 0, Card("Ventara", atk: 1000));
        Do(s3, "skill", 0, 0);                                                 // Ventara khắc Luminara: −16%
        Assert.Equal(840, t3.EffAtk);
    }

    [Fact(DisplayName = "Hồi máu và buff lên đồng đội không chịu tương khắc")]
    public void HealIgnoresCounter()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara", "luminara.dawns_embrace")); var ally = Put(s, Me(s), 1, Card("Ventara", hp: 1000)); ally.Hp = 500;
        Do(s, "skill", 0, 1);
        Assert.Equal(700, ally.Hp);            // 20% × 1000, không ±20%
    }

    // ---------- Quy tắc chung ----------
    [Fact(DisplayName = "Active: tốn hành động của Anima, có hồi chiêu, Anima vừa ra sân và bị stun không dùng được")]
    public void ActiveRules()
    {
        var s = Playing(); var u = Put(s, Me(s), 0, Card("Umbryx", "umbryx.silent_tear")); Put(s, Foe(s), 0, Card("Terrakin", hp: 9000));
        Do(s, "skill", 0, 0);
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, "skill", 0, 0));                    // đã hành động
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, "attack", 0, 0));
        End(s); End(s);                                                                  // CD 1: sẵn sàng ở lượt kế tiếp của mình
        Do(s, "skill", 0, 0);
        End(s); End(s);
        var fresh = Put(s, Me(s), 1, Card("Umbryx", "umbryx.silent_tear"), summoned: true);
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, "skill", 1, 0));
        fresh.Summoned = false; u.Statuses.Add(new Status { Kind = "stun", Turns = 1 });
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, "skill", 0, 0));
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, "attack", 0, 0));
    }

    [Fact(DisplayName = "Hồi chiêu chặn dùng lại cho tới đúng lượt: Blinding Light CD 3")]
    public void Cooldown()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara", "luminara.blinding_light")); Put(s, Foe(s), 0, Card("Terrakin", hp: 99999));
        var me = Me(s);
        Do(s, "skill", 0, 0); End(s); End(s);
        Assert.Equal("SKILL_ON_COOLDOWN", Fails(s, "skill", 0, 0)); End(s); End(s);
        Assert.Equal("SKILL_ON_COOLDOWN", Fails(s, "skill", 0, 0)); End(s); End(s);
        Do(s, "skill", 0, 0);                                                          // lượt thứ 3 sau lần dùng
        Assert.Equal(me, s.Active);
    }

    [Fact(DisplayName = "Kỹ năng không có hoặc thẻ không có kỹ năng Active: SKILL_NOT_AVAILABLE; mục tiêu sai: TARGET_INVALID")]
    public void SkillValidation()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Pyraxis", "pyraxis.rage_bound")); Put(s, Me(s), 1, Card("Umbryx", "umbryx.silent_tear"));
        Assert.Equal("SKILL_NOT_AVAILABLE", Fails(s, "skill", 0, 0));
        Assert.Equal("TARGET_INVALID", Fails(s, "skill", 1, 2));
        Assert.Equal("INVALID_ACTION", Fails(s, "skill", 9, 0));
    }

    // ---------- Emotion Meter ----------
    [Fact(DisplayName = "Emotion Meter: +2 người đánh, +3 người bị đánh, +5 khi Anima ngã, +5 khi dùng kỹ năng")]
    public void MeterGains()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Umbryx", atk: 1000)); Put(s, Foe(s), 0, Card("Terrakin", def: 0, hp: 4000));
        Do(s, "attack", 0, 0);
        Assert.Equal(5, s.Meter);                                                       // +2 +3
        Put(s, Me(s), 1, Card("Umbryx", "umbryx.silent_tear")); Do(s, "skill", 1, 0);
        Assert.Equal(10, s.Meter);                                                      // +5
        var s2 = Playing(); Put(s2, Me(s2), 0, Card("Umbryx", atk: 9000)); Put(s2, Foe(s2), 0, Card("Terrakin", def: 0, hp: 100));
        Do(s2, "attack", 0, 0);
        Assert.Equal(10, s2.Meter);                                                     // +2 +3 +5
    }

    [Fact(DisplayName = "Emotional Surge: Meter đạt 100 → mọi Anima +20% ATK/DEF trong 2 lượt mỗi bên rồi hết; Meter về 0")]
    public void Surge()
    {
        var s = Playing(); var a = Put(s, Me(s), 0, Card("Umbryx", atk: 1000, def: 500)); var b = Put(s, Foe(s), 0, Card("Terrakin", atk: 1000, def: 500, hp: 99999));
        Meter(s, 97); Do(s, "attack", 0, 0);
        Assert.Equal(0, s.Meter); Assert.Equal(1200, a.EffAtk); Assert.Equal(600, b.EffDef);
        var me = Me(s); End(s); End(s);
        Assert.Equal(me, s.Active); Assert.Equal(1200, a.EffAtk);                       // còn lượt thứ 2
        End(s); End(s);
        Assert.Equal(1000, a.EffAtk); Assert.Equal(500, b.EffDef);
    }

    // ---------- Ultimate ----------
    [Fact(DisplayName = "Ultimate cần Meter ≥ 50 và tiêu 50; bị Fear thì không dùng được; tính là hành động của Anima")]
    public void UltimateRules()
    {
        var s = Playing(); var u = Put(s, Me(s), 0, Card("Umbryx")); Put(s, Foe(s), 0, Card("Terrakin", hp: 99999));
        Assert.Equal("EMOTION_INSUFFICIENT", Fails(s, "ultimate", 0));
        Meter(s, 49); Assert.Equal("EMOTION_INSUFFICIENT", Fails(s, "ultimate", 0));
        Meter(s, 80); u.Statuses.Add(new Status { Kind = "fear", Turns = 2 });
        Assert.Equal("ULTIMATE_BLOCKED", Fails(s, "ultimate", 0));
        u.Statuses.Clear(); Do(s, "ultimate", 0);
        Assert.Equal(35, s.Meter);                                                      // 80 − 50 + 5
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, "attack", 0, 0));
    }

    [Fact(DisplayName = "Ultimate Luminara/Aqualis: hồi đầy HP + xóa debuff; hồi 50% cả đội")]
    public void HealingUltimates()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara")); var a = Put(s, Me(s), 1, Card("Terrakin", hp: 1000)); a.Hp = 100;
        a.Statuses.Add(new Status { Kind = "atk_pct", Turns = 2, Value = -20 }); Meter(s, 50);
        Do(s, "ultimate", 0, 1);
        Assert.Equal(1000, a.Hp); Assert.Empty(a.Statuses);
        var t = Playing(); Put(t, Me(t), 0, Card("Aqualis", hp: 1000)); var b = Put(t, Me(t), 1, Card("Terrakin", hp: 1000)); b.Hp = 100; b.Statuses.Add(new Status { Kind = "fear", Turns = 2 });
        Meter(t, 50); Do(t, "ultimate", 0);
        Assert.Equal(600, b.Hp); Assert.Empty(b.Statuses);
    }

    [Fact(DisplayName = "Ultimate sát thương: Umbryx 600 mọi địch + hồi 30%; Pyraxis 1000 và hồi 50% khi hạ; Nihilum 1200 và mất 30%")]
    public void DamageUltimates()
    {
        var s = Playing(); var u = Put(s, Me(s), 0, Card("Umbryx", hp: 1000)); u.Hp = 500;
        var e1 = Put(s, Foe(s), 0, Card("Terrakin", hp: 3000, def: 0)); var e2 = Put(s, Foe(s), 1, Card("Pyraxis", hp: 3000, def: 0));
        Meter(s, 50); Do(s, "ultimate", 0);
        Assert.Equal(2400, e1.Hp); Assert.Equal(2400, e2.Hp); Assert.Equal(800, u.Hp);

        var p = Playing(); var py = Put(p, Me(p), 0, Card("Pyraxis", hp: 2000)); py.Hp = 1000; Put(p, Foe(p), 0, Card("Terrakin", hp: 900, def: 0));
        Meter(p, 50); Do(p, "ultimate", 0, 0);
        Assert.Null(p.Players[Foe(p)].Field[0]); Assert.Equal(2000, py.Hp);

        var n = Playing(); var ni = Put(n, Me(n), 0, Card("Nihilum", hp: 1000)); var tgt = Put(n, Foe(n), 0, Card("Terrakin", hp: 5000, def: 0));
        Meter(n, 50); Do(n, "ultimate", 0, 0);
        Assert.Equal(3800, tgt.Hp); Assert.Equal(700, ni.Hp);
    }

    [Fact(DisplayName = "Ultimate Terrakin: bất tử 2 lượt, hồi 30% cả đội; Ventara: 5 đòn ngẫu nhiên; Voltaris: 800 + stun")]
    public void OtherUltimates()
    {
        var s = Playing(); var t = Put(s, Me(s), 0, Card("Terrakin", hp: 1000)); var ally = Put(s, Me(s), 1, Card("Umbryx", hp: 1000)); ally.Hp = 100;
        Put(s, Foe(s), 0, Card("Terrakin"));
        Meter(s, 50); Do(s, "ultimate", 0);
        Assert.Equal(400, ally.Hp); Assert.Contains(t.Statuses, x => x.Kind == "invuln");

        var v = Playing(); Put(v, Me(v), 0, Card("Ventara")); var e = Put(v, Foe(v), 0, Card("Terrakin", hp: 3000, def: 0));
        Meter(v, 50); Do(v, "ultimate", 0);
        Assert.Equal(3000 - 5 * 200, e.Hp);                                             // 5 đòn × 200 (trung tính)

        var w = Playing(); Put(w, Me(w), 0, Card("Voltaris")); var e1 = Put(w, Foe(w), 0, Card("Pyraxis", hp: 3000, def: 0)); var e2 = Put(w, Foe(w), 1, Card("Ventara", hp: 3000, def: 0));
        Meter(w, 50); Do(w, "ultimate", 0);
        Assert.Equal(2200, e1.Hp); Assert.Equal(2200, e2.Hp);
    }

    [Fact(DisplayName = "Echo of Loss: Aqualis bên địch giảm 50% sát thương Ultimate, rồi hồi chiêu 2")]
    public void EchoOfLoss()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Umbryx"));
        var target = Put(s, Foe(s), 0, Card("Terrakin", hp: 3000, def: 0)); var aq = Put(s, Foe(s), 1, Card("Aqualis", "aqualis.echo_of_loss", hp: 3000, def: 0));
        Meter(s, 50); Do(s, "ultimate", 0);
        Assert.Equal(2700, target.Hp); Assert.Equal(3000 - 360, aq.Hp);                  // 300 (một nửa của 600); Aqualis bị Umbryx khắc: 600×1,2×0,5
        Assert.Equal(2, aq.Cd["aqualis.echo_of_loss"]);
    }

    // ---------- Cấp thẻ: Luminara ----------
    [Fact(DisplayName = "Dawn's Embrace: 20% HP, hoặc 40% khi mục tiêu dưới 30% HP; Nihilum không hồi được")]
    public void DawnsEmbrace()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara", "luminara.dawns_embrace")); var a = Put(s, Me(s), 1, Card("Terrakin", hp: 1000)); a.Hp = 600;
        Do(s, "skill", 0, 1); Assert.Equal(800, a.Hp);
        var t = Playing(); Put(t, Me(t), 0, Card("Luminara", "luminara.dawns_embrace")); var b = Put(t, Me(t), 1, Card("Terrakin", hp: 1000)); b.Hp = 200;
        Do(t, "skill", 0, 1); Assert.Equal(600, b.Hp);
        var u = Playing(); Put(u, Me(u), 0, Card("Luminara", "luminara.dawns_embrace")); var n = Put(u, Me(u), 1, Card("Nihilum", "nihilum.emptiness", hp: 1000)); n.Hp = 200;
        Do(u, "skill", 0, 1); Assert.Equal(200, n.Hp);
    }

    [Fact(DisplayName = "Radiant Aura: +5% DEF mỗi lượt cho đồng đội, tối đa 25%; mất khi hết nguồn")]
    public void RadiantAura()
    {
        var s = Playing(); var l = Put(s, Me(s), 0, Card("Luminara", "luminara.radiant_aura")); var a = Put(s, Me(s), 1, Card("Terrakin", def: 1000));
        var me = Me(s);
        for (var i = 0; i < 7; i++) { End(s); End(s); }
        Assert.Equal(me, s.Active); Assert.Equal(1250, a.EffDef);
        s.Players[me].Field[0] = null; End(s); End(s);
        Assert.Equal(1000, a.EffDef);
    }

    [Fact(DisplayName = "Hope's Echo hồi 15% cho đồng đội khi một đồng đội ngã (1 lần); Lonely Howl giảm 15% ATK địch")]
    public void AllyFallTriggers()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara", "luminara.hopes_echo", hp: 1000)); var b = Put(s, Me(s), 1, Card("Voltaris", hp: 1000)); b.Hp = 500;
        var weak = Put(s, Me(s), 2, Card("Pyraxis", hp: 10));
        Put(s, Foe(s), 0, Card("Umbryx", atk: 9000)); End(s);
        Do(s, "attack", 0, 2);                                                           // địch hạ đồng đội yếu
        Assert.Null(s.Players[1 - s.Active].Field[2]); Assert.Equal(650, b.Hp);
        var t = Playing(); Put(t, Me(t), 0, Card("Umbryx", "umbryx.lonely_howl")); Put(t, Me(t), 1, Card("Terrakin", hp: 10));
        var foe = Put(t, Foe(t), 0, Card("Pyraxis", atk: 9000, def: 0, hp: 9999)); End(t);
        Do(t, "attack", 0, 1);
        Assert.Equal(7650, foe.EffAtk);                                                  // −15% (Umbryx không khắc/bị khắc Pyraxis)
    }

    // ---------- Umbryx ----------
    [Fact(DisplayName = "Silent Tear ×1,5 khi mục tiêu đang bị debuff")]
    public void SilentTearBonus()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Umbryx", "umbryx.silent_tear")); var t = Put(s, Foe(s), 0, Card("Terrakin", hp: 3000, def: 0));
        t.Statuses.Add(new Status { Kind = "atk_pct", Turns = 2, Value = -10 }); BattleEngine.Recalc(s);
        Do(s, "skill", 0, 0);
        Assert.Equal(3000 - 380, t.Hp);                                                 // 375 làm tròn hàng chục (từ 5 lên)
    }

    [Fact(DisplayName = "Nightmare Touch: Fear 2 lượt chặn Ultimate; Veil of Sorrow tăng SPD và né khi HP dưới 50%")]
    public void NightmareAndVeil()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Umbryx", "umbryx.nightmare_touch")); var t = Put(s, Foe(s), 0, Card("Terrakin", hp: 9000));
        Do(s, "skill", 0, 0); Assert.Contains(t.Statuses, x => x.Kind == "fear");
        var v = Playing(); var u = Put(v, Me(v), 0, Card("Umbryx", "umbryx.veil_of_sorrow", hp: 1000));
        Assert.Equal(0, u.DodgePct); u.Hp = 400; BattleEngine.Recalc(v);
        Assert.Equal(28, u.DodgePct);                                                    // 20 + (130−100)×0,25 = 27,5 → làm tròn 28
    }

    // ---------- Pyraxis ----------
    [Fact(DisplayName = "Ember Strike gây Burn 50/lượt (×1,2 khi khắc) trong 3 lượt; Burn tính ở đầu lượt của chủ và hết hạn")]
    public void EmberStrikeBurn()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Pyraxis", "pyraxis.ember_strike")); var t = Put(s, Foe(s), 0, Card("Terrakin", hp: 3000, def: 0));
        Do(s, "skill", 0, 0);
        Assert.Equal(3000 - 420, t.Hp);                                                  // 350 × 1,2 (Pyraxis khắc Terrakin)
        End(s); Assert.Equal(3000 - 420 - 60, t.Hp);                                     // Burn 50 × 1,2 ở đầu lượt địch
        End(s); End(s); Assert.Equal(3000 - 420 - 120, t.Hp);
        End(s); End(s); Assert.Equal(3000 - 420 - 180, t.Hp);
        End(s); End(s);
        Assert.Equal(3000 - 420 - 180, t.Hp); Assert.DoesNotContain(t.Statuses, x => x.Kind == "burn");
    }

    [Fact(DisplayName = "Twin Fury: Burn chồng được tới 3 lớp khi có 2+ Pyraxis, không thì 1 lớp; mỗi Pyraxis +15% ATK")]
    public void TwinFury()
    {
        var s = Playing(); var a = Put(s, Me(s), 0, Card("Pyraxis", "pyraxis.ember_strike", atk: 1000)); Put(s, Me(s), 1, Card("Pyraxis", atk: 1000));
        Assert.Equal(1150, a.EffAtk);
        var t = Put(s, Foe(s), 0, Card("Terrakin", hp: 99999));
        Do(s, "skill", 0, 0); End(s); End(s); End(s); End(s); Do(s, "skill", 0, 0);
        Assert.Equal(2, t.Statuses.Single(x => x.Kind == "burn").Stacks);
        var u = Playing(); Put(u, Me(u), 0, Card("Pyraxis", "pyraxis.ember_strike")); var t2 = Put(u, Foe(u), 0, Card("Terrakin", hp: 99999));
        Do(u, "skill", 0, 0); End(u); End(u); End(u); End(u); Do(u, "skill", 0, 0);
        Assert.Equal(1, t2.Statuses.Single(x => x.Kind == "burn").Stacks);
    }

    [Fact(DisplayName = "Rage Bound +5% ATK mỗi 10% HP mất (tối đa +50%); Fury Unleashed phản 20% sát thương")]
    public void RageAndFury()
    {
        var s = Playing(); var a = Put(s, Me(s), 0, Card("Pyraxis", "pyraxis.rage_bound", atk: 1000, hp: 1000));
        a.Hp = 700; BattleEngine.Recalc(s); Assert.Equal(1150, a.EffAtk);
        a.Hp = 100; BattleEngine.Recalc(s); Assert.Equal(1450, a.EffAtk);
        a.Hp = 1; BattleEngine.Recalc(s); Assert.Equal(1450, a.EffAtk);                    // 99,9% mất → 9 bậc × 5% (đủ 50% chỉ khi HP = 0)

        var t = Playing(); var holder = Put(t, Me(t), 0, Card("Pyraxis", "pyraxis.fury_unleashed", def: 0, hp: 5000)); var atk = Put(t, Foe(t), 0, Card("Terrakin", atk: 1000, hp: 5000, def: 0)); End(t);
        Do(t, "attack", 0, 0);
        Assert.Equal(5000 - 750, holder.Hp);                                             // Terrakin khắc... Pyraxis khắc Terrakin → bị khắc ×0,75
        Assert.Equal(5000 - 150, atk.Hp);                                                // 20% × 750
    }

    [Fact(DisplayName = "Flame Vortex: 250 lên mọi địch kèm Burn 30/lượt")]
    public void FlameVortex()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Pyraxis", "pyraxis.flame_vortex")); var a = Put(s, Foe(s), 0, Card("Aqualis", hp: 3000, def: 0)); var b = Put(s, Foe(s), 1, Card("Umbryx", hp: 3000, def: 0));
        Do(s, "skill", 0);
        Assert.Equal(2750, a.Hp); Assert.Equal(2750, b.Hp); Assert.All(new[] { a, b }, x => Assert.Contains(x.Statuses, st => st.Kind == "burn" && Math.Abs(st.Value - 30) < 0.01));
    }

    // ---------- Aqualis ----------
    [Fact(DisplayName = "Tidal Memory +5 Meter; Deep Calm miễn Fear và +10% DEF; Undertow: SPD −30% và không né được")]
    public void AqualisSkills()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Aqualis", "aqualis.tidal_memory")); Put(s, Foe(s), 0, Card("Terrakin", hp: 9000));
        Do(s, "skill", 0, 0); Assert.Equal(10, s.Meter);                                 // +5 skill +5 tidal_memory

        var d = Playing(); var calm = Put(d, Me(d), 0, Card("Aqualis", "aqualis.deep_calm", def: 1000));
        Assert.Equal(1100, calm.EffDef);
        Put(d, Foe(d), 0, Card("Umbryx", "umbryx.nightmare_touch")); End(d);
        Do(d, "skill", 0, 0);
        Assert.DoesNotContain(calm.Statuses, x => x.Kind == "fear");

        var u = Playing(); Put(u, Me(u), 0, Card("Aqualis", "aqualis.undertow")); var t = Put(u, Foe(u), 0, Card("Ventara", "ventara.free_spirit", hp: 9000));
        Assert.Equal(25, t.DodgePct); Do(u, "skill", 0, 0);
        Assert.Equal(0, t.DodgePct); Assert.True(t.SpdPct < 0);
    }

    // ---------- Terrakin ----------
    [Fact(DisplayName = "Stone Wall: DEF +50% và Taunt — địch phải đánh Anima đó (TARGET_TAUNTED)")]
    public void StoneWallTaunt()
    {
        var s = Playing(); var wall = Put(s, Me(s), 0, Card("Terrakin", "terrakin.stone_wall", def: 1000)); Put(s, Me(s), 1, Card("Umbryx"));
        Put(s, Foe(s), 0, Card("Pyraxis"));
        Do(s, "skill", 0); Assert.Equal(1500, wall.EffDef);
        End(s);
        Assert.Equal("TARGET_TAUNTED", Fails(s, "attack", 0, 1));
        Do(s, "attack", 0, 0);
    }

    [Fact(DisplayName = "Unyielding: dưới 30% HP DEF +50% và sống sót 1 lần với 1 HP")]
    public void Unyielding()
    {
        var s = Playing(); var t = Put(s, Me(s), 0, Card("Terrakin", "terrakin.unyielding", def: 1000, hp: 1000)); t.Hp = 200; BattleEngine.Recalc(s);
        Assert.Equal(1500, t.EffDef);
        Put(s, Foe(s), 0, Card("Umbryx", atk: 9000)); End(s);
        Do(s, "attack", 0, 0);
        Assert.Equal(1, t.Hp); Assert.NotNull(s.Players[1 - s.Active].Field[0]);
        End(s); End(s);
        Do(s, "attack", 0, 0);
        Assert.Null(s.Players[1 - s.Active].Field[0]);                                    // lần sau thì ngã
    }

    [Fact(DisplayName = "Avalanche phản 150 (×1,5 khi DEF > 1000); Earth's Grasp: root chặn né")]
    public void AvalancheAndGrasp()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Terrakin", "terrakin.avalanche", def: 1200, hp: 9000)); var atk = Put(s, Foe(s), 0, Card("Umbryx", atk: 2000, hp: 5000, def: 0)); End(s);
        Do(s, "attack", 0, 0);
        Assert.Equal(5000 - 225, atk.Hp);
        var g = Playing(); Put(g, Me(g), 0, Card("Terrakin", "terrakin.earths_grasp")); var t = Put(g, Foe(g), 0, Card("Ventara", "ventara.free_spirit"));
        Do(g, "skill", 0, 0); Assert.Equal(0, t.DodgePct);
    }

    // ---------- Ventara ----------
    [Fact(DisplayName = "Wind Step đổi chỗ với đồng đội, cả hai SPD +20%")]
    public void WindStep()
    {
        var s = Playing(); var a = Put(s, Me(s), 0, Card("Ventara", "ventara.wind_step", name: "A")); var b = Put(s, Me(s), 2, Card("Umbryx", name: "B"));
        Do(s, "skill", 0, 2);
        Assert.Same(b, s.Players[Me(s)].Field[0]); Assert.Same(a, s.Players[Me(s)].Field[2]); Assert.Equal(20, a.SpdPct); Assert.Equal(20, b.SpdPct);
    }

    [Fact(DisplayName = "Gust Barrier: đòn kế tiếp −50%, dodge +15%; Free Spirit: dodge +25% và né thành công được +10% ATK")]
    public void GustBarrierAndFreeSpirit()
    {
        var s = Playing(); var a = Put(s, Me(s), 0, Card("Ventara", "ventara.gust_barrier", hp: 5000, def: 0)); Put(s, Foe(s), 0, Card("Umbryx", atk: 1000)); BattleEngine.Recalc(s);
        Do(s, "skill", 0); Assert.Equal(15 + 0, a.DodgePct + 0 * 0);
        End(s);
        // Đòn kế tiếp bị giảm một nửa dù có né hay không: kiểm trên nhiều seed.
        var halved = 0; var dodged = 0;
        for (byte seed = 1; seed <= 60; seed++)
        {
            var t = Playing(seed); var h = Put(t, Me(t), 0, Card("Ventara", "ventara.gust_barrier", hp: 5000, def: 0)); Put(t, Foe(t), 0, Card("Umbryx", atk: 1000));
            Do(t, "skill", 0); End(t); Do(t, "attack", 0, 0);
            if (h.Hp == 5000) dodged++; else if (h.Hp == 5000 - 500 || h.Hp == 5000 - 375 || h.Hp == 5000 - 625) halved++;
        }
        Assert.True(dodged > 0 && halved > 0, $"dodged={dodged} halved={halved}");

        var f = Playing(); var fs = Put(f, Me(f), 0, Card("Ventara", "ventara.free_spirit")); Assert.Equal(25, fs.DodgePct);
    }

    [Fact(DisplayName = "Curiosity: 30% nhận buff ngẫu nhiên đầu lượt — xảy ra ở một số seed nhưng không phải tất cả")]
    public void Curiosity()
    {
        var got = 0;
        for (byte seed = 1; seed <= 60; seed++)
        {
            var s = Playing(seed); var me = Me(s); var f = Put(s, me, 0, Card("Ventara", "ventara.curiosity"));
            End(s); End(s);
            if (f.Statuses.Any(x => x.Kind is "atk_pct" or "def_pct" or "spd_pct")) got++;
        }
        Assert.InRange(got, 5, 40);
    }

    // ---------- Voltaris ----------
    [Fact(DisplayName = "Surge Bolt 400 và 30% stun; Static Field giảm SPD mọi địch")]
    public void VoltarisActives()
    {
        var stuns = 0;
        for (byte seed = 1; seed <= 80; seed++)
        {
            var s = Playing(seed); Put(s, Me(s), 0, Card("Voltaris", "voltaris.surge_bolt")); var t = Put(s, Foe(s), 0, Card("Pyraxis", hp: 9000, def: 0));
            Do(s, "skill", 0, 0);
            Assert.Equal(9000 - 400, t.Hp);
            if (t.Statuses.Any(x => x.Kind == "stun")) stuns++;
        }
        Assert.InRange(stuns, 8, 50);

        var g = Playing(); Put(g, Me(g), 0, Card("Voltaris", "voltaris.static_field")); var e = Put(g, Foe(g), 0, Card("Pyraxis")); var e2 = Put(g, Foe(g), 1, Card("Ventara"));
        Do(g, "skill", 0); Assert.Equal(-20, e.SpdPct); Assert.Equal(-20, e2.SpdPct);
    }

    [Fact(DisplayName = "Overcharge: +10% ATK mỗi lượt (tối đa 5 lớp); cuối lượt không hành động mất nửa số lớp; Voltage Storm +25% ATK")]
    public void Overcharge()
    {
        var s = Playing(); var a = Put(s, Me(s), 0, Card("Voltaris", "voltaris.overcharge", atk: 1000)); Put(s, Foe(s), 0, Card("Terrakin", hp: 99999, def: 5000));
        var me = Me(s);
        End(s); End(s);                                                                  // không hành động: lớp 1 sinh ra ở đầu lượt này rồi cuối lượt bị chia đôi
        Assert.Equal(me, s.Active);
        for (var i = 0; i < 3; i++) { Do(s, "attack", 0, 0); End(s); End(s); }
        Assert.Equal(1000 + 100 * a.Statuses.Single(x => x.Kind == "overcharge").Stacks, a.EffAtk);
        for (var i = 0; i < 4; i++) { Do(s, "attack", 0, 0); End(s); End(s); }
        Assert.Equal(1500, a.EffAtk);
        var v = Playing(); var x1 = Put(v, Me(v), 0, Card("Voltaris", atk: 1000)); Put(v, Me(v), 1, Card("Voltaris", atk: 1000));
        Assert.Equal(1250, x1.EffAtk);
    }

    [Fact(DisplayName = "Chain Reaction: đôi khi lan 50% sát thương sang địch khác, không lan khi chỉ có một địch")]
    public void ChainReaction()
    {
        var spread = 0;
        for (byte seed = 1; seed <= 80; seed++)
        {
            var s = Playing(seed); Put(s, Me(s), 0, Card("Voltaris", "voltaris.chain_reaction", atk: 2000)); var a = Put(s, Foe(s), 0, Card("Pyraxis", hp: 9000, def: 0)); var b = Put(s, Foe(s), 1, Card("Ventara", hp: 9000, def: 0));
            Do(s, "attack", 0, 0);
            if (b.Hp < 9000) { spread++; Assert.Equal(9000 - 1000, b.Hp + 0 * a.Hp); }
        }
        Assert.InRange(spread, 5, 45);
        var solo = Playing(3); Put(solo, Me(solo), 0, Card("Voltaris", "voltaris.chain_reaction", atk: 2000)); Put(solo, Foe(solo), 0, Card("Terrakin", hp: 9000, def: 0));
        Do(solo, "attack", 0, 0);
    }

    // ---------- Nihilum ----------
    [Fact(DisplayName = "Void Touch 500 và tự mất 10% HP; Oblivion xóa 1 buff + 300")]
    public void NihilumActives()
    {
        var s = Playing(); var n = Put(s, Me(s), 0, Card("Nihilum", "nihilum.void_touch", hp: 1000)); var t = Put(s, Foe(s), 0, Card("Terrakin", hp: 9000, def: 0));
        Do(s, "skill", 0, 0);
        Assert.Equal(900, n.Hp); Assert.Equal(9000 - 500, t.Hp);
        var o = Playing(); Put(o, Me(o), 0, Card("Nihilum", "nihilum.oblivion")); var e = Put(o, Foe(o), 0, Card("Terrakin", hp: 9000, def: 0));
        e.Statuses.Add(new Status { Kind = "def_pct", Turns = 2, Value = 30 });
        Do(o, "skill", 0, 0);
        Assert.DoesNotContain(e.Statuses, x => x.Kind == "def_pct"); Assert.Equal(9000 - 300, e.Hp);
    }

    [Fact(DisplayName = "Emptiness: miễn mọi debuff và không hồi máu; Despair: dưới 20% HP ATK +100% và mất 5% HP mỗi đầu lượt")]
    public void EmptinessAndDespair()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Umbryx", "umbryx.nightmare_touch")); var n = Put(s, Foe(s), 0, Card("Nihilum", "nihilum.emptiness", hp: 9000));
        Do(s, "skill", 0, 0); Assert.DoesNotContain(n.Statuses, x => x.Kind == "fear");
        var d = Playing(); var x = Put(d, Me(d), 0, Card("Nihilum", "nihilum.despair", atk: 1000, hp: 1000)); x.Hp = 150; BattleEngine.Recalc(d);
        Assert.Equal(2000, x.EffAtk);
        var me = Me(d); End(d); End(d); Assert.Equal(me, d.Active); Assert.Equal(100, x.Hp);
    }

    [Fact(DisplayName = "The Void Calls: với 2+ Nihilum, một Nihilum sắp ngã được hồi sinh 30% HP — chỉ 1 lần/trận")]
    public void VoidCalls()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Nihilum", hp: 1000)); var b = Put(s, Me(s), 1, Card("Nihilum", hp: 1000)); Put(s, Foe(s), 0, Card("Umbryx", atk: 9000)); End(s);
        Do(s, "attack", 0, 1);
        Assert.NotNull(s.Players[1 - s.Active].Field[1]); Assert.Equal(300, s.Players[1 - s.Active].Field[1]!.Hp);
        End(s); End(s);
        Do(s, "attack", 0, 1);
        Assert.Null(s.Players[1 - s.Active].Field[1]);
        Assert.NotNull(b);
    }

    // ---------- Resonance ----------
    [Fact(DisplayName = "Sacred Bond: 2 Luminara trên sân → HP tối đa +30%, DEF +20% trong 3 lượt, chỉ 1 lần/trận")]
    public void SacredBond()
    {
        var s = Playing(); var me = s.Me; me.Energy = 6; me.Hand.Clear();
        me.Hand.AddRange([Card("Luminara", hp: 1000, def: 1000), Card("Luminara", hp: 1000, def: 1000)]);
        BattleEngine.Apply(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 0));
        BattleEngine.Apply(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 1));
        var l = me.Field[0]!;
        Assert.Equal(1300, l.MaxHp); Assert.Equal(1300, l.Hp); Assert.Equal(1200, l.EffDef);
        End(s); End(s); End(s); End(s); End(s); End(s);                                   // qua 3 lượt của mình
        Assert.Equal(1000, l.MaxHp); Assert.Equal(1000, l.EffDef); Assert.Contains("sacred_bond", me.TeamUsed);
    }

    [Fact(DisplayName = "Resonance đa hệ: Shared Pain chia sát thương; Shared Memory hồi 5% và giảm 10%; Mountain's Bond chia DEF và +20% HP")]
    public void Resonances()
    {
        var s = Playing(); var u1 = Put(s, Me(s), 0, Card("Umbryx", hp: 1000, atk: 1000)); var u2 = Put(s, Me(s), 1, Card("Umbryx", hp: 1000)); Assert.Equal(1100, u1.EffAtk);
        Put(s, Foe(s), 0, Card("Pyraxis", atk: 1900, def: 0)); End(s);
        Do(s, "attack", 0, 0);
        Assert.Equal(u1.Hp, u2.Hp); Assert.True(u1.Hp < 1000);

        var q = Playing(); var a1 = Put(q, Me(q), 0, Card("Aqualis", hp: 1000)); a1.Hp = 500; Put(q, Me(q), 1, Card("Aqualis", hp: 1000)); var me = Me(q);
        End(q); End(q); Assert.Equal(me, q.Active); Assert.Equal(550, a1.Hp);

        var t = Playing(); var t1 = Put(t, Me(t), 0, Card("Terrakin", def: 1000, hp: 1000)); var t2 = Put(t, Me(t), 1, Card("Terrakin", def: 500, hp: 1000));
        Assert.Equal(750, t1.EffDef); Assert.Equal(750, t2.EffDef); Assert.Equal(1200, t1.MaxHp);
    }

    // ---------- Fusion ----------
    [Fact(DisplayName = "Fusion cần đủ hai hệ trên sân, mỗi Fusion 1 lần/trận, tối đa 1 lần/lượt, không tốn hành động của Anima")]
    public void FusionRules()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara", hp: 1000)); var p = Put(s, Me(s), 1, Card("Pyraxis", hp: 1000)); p.Hp = 100;
        Assert.Equal("FUSION_NOT_AVAILABLE", Fails(s, "fusion", 0, id: "silent_storm"));
        Put(s, Foe(s), 0, Card("Terrakin", hp: 99999));
        Do(s, "fusion", 0, id: "dawnbreak");
        Assert.Equal(400, p.Hp); Assert.Contains(p.Statuses, x => x.Kind == "atk_pct" && x.Value == 20);
        Assert.Equal("FUSION_NOT_AVAILABLE", Fails(s, "fusion", 0, id: "dawnbreak"));
        Do(s, "attack", 0, 0);                                                           // Anima vẫn còn hành động
        End(s); End(s);
        Assert.Equal("FUSION_NOT_AVAILABLE", Fails(s, "fusion", 0, id: "dawnbreak"));      // 1 lần/trận
        Assert.Equal("FUSION_NOT_AVAILABLE", Fails(s, "fusion", 0, id: "nope"));
    }

    [Fact(DisplayName = "Silent Storm, Eternal Tide, Void's Edge, Hope's Last Stand, Chaos Theory (Fusion)")]
    public void FusionEffects()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Umbryx")); Put(s, Me(s), 1, Card("Voltaris")); var e = Put(s, Foe(s), 0, Card("Terrakin", hp: 3000, def: 0));
        Do(s, "fusion", 0, id: "silent_storm");
        Assert.Equal(2600, e.Hp); Assert.Contains(e.Statuses, x => x.Kind == "stun");

        var t = Playing(); var a = Put(t, Me(t), 0, Card("Aqualis", hp: 1000)); Put(t, Me(t), 1, Card("Terrakin", hp: 1000)); a.Hp = 500;
        Do(t, "fusion", 0, id: "eternal_tide");
        Assert.Equal(700, a.Hp); Assert.Contains(a.Statuses, x => x.Kind == "invuln");

        var v = Playing(); Put(v, Me(v), 0, Card("Nihilum")); Put(v, Me(v), 1, Card("Umbryx")); var armored = Put(v, Foe(v), 0, Card("Terrakin", hp: 3000, def: 9000));
        Do(v, "fusion", 0, 0, "voids_edge");
        Assert.Equal(3000 - 600, armored.Hp);                                             // bỏ qua DEF, trung tính (Umbryx vs Terrakin)

        var h = Playing(); var l = Put(h, Me(h), 0, Card("Luminara", hp: 1000)); var n = Put(h, Me(h), 1, Card("Nihilum", hp: 1000)); l.Hp = 100; n.Hp = 100;
        Do(h, "fusion", 1, 0, "hopes_last_stand");                                        // slot không dùng — Target = đồng đội
        Assert.Equal(1000, l.Hp);

        var c = Playing(); Put(c, Me(c), 0, Card("Ventara")); Put(c, Me(c), 1, Card("Voltaris")); var f1 = Put(c, Foe(c), 0, Card("Terrakin", hp: 9000)); var f2 = Put(c, Foe(c), 1, Card("Terrakin", hp: 9000));
        Do(c, "fusion", 0, id: "chaos_theory_fusion");
        Assert.All(new[] { f1, f2 }, f => Assert.NotEmpty(f.Statuses));
    }

    [Fact(DisplayName = "Hope's Last Stand chỉ dùng được khi đội dưới 30% tổng HP")]
    public void HopesLastStandCondition()
    {
        var s = Playing(); Put(s, Me(s), 0, Card("Luminara", hp: 1000)); Put(s, Me(s), 1, Card("Nihilum", hp: 1000));
        Assert.Equal("FUSION_NOT_AVAILABLE", Fails(s, "fusion", 0, 0, "hopes_last_stand"));
    }

    // ---------- Tất định, bot, phát lại ----------
    private static List<BattleCard> SkillDeck(int offset)
    {
        var cards = new List<BattleCard>();
        for (var i = 0; i < 30; i++)
        {
            var el = Anima.Contracts.Elements.All[(i + offset) % 8];
            var skills = SkillCatalog.CardSkills(el);
            cards.Add(new BattleCard(100 + i, $"{el}{i}", el, "anima", "common", 1 + (i % 4), 800 + (i % 5) * 150, 300 + (i % 4) * 150, 1200 + (i % 6) * 250, skills[i / 8 % skills.Count]));
        }
        return cards;
    }

    [Fact(DisplayName = "Máy đấu máy với bộ bài có đủ kỹ năng: luôn kết thúc, dùng được kỹ năng/Ultimate/Fusion, phát lại cho kết quả y hệt")]
    public void BotMatchesWithSkillsAreReplayable()
    {
        var kinds = new HashSet<string>();
        for (byte seed = 1; seed <= 40; seed++)
        {
            var setup = new BattleSetup([seed], SkillDeck(0), SkillDeck(3));
            var s = BattleEngine.Start(setup); var log = new List<BattleAction>();
            for (var guard = 0; s.Phase != BattlePhase.Finished && guard < 6000; guard++)
            {
                var who = s.Phase == BattlePhase.Mulligan ? Array.FindIndex(s.Players, p => !p.Decided) : s.Active;
                var a = Bot.Next(s, who); BattleEngine.Apply(s, a); log.Add(a);
            }
            Assert.NotNull(s.Result);
            foreach (var e in s.Events) kinds.Add(e.Kind);
            var r = BattleEngine.Start(setup); foreach (var a in log) BattleEngine.Apply(r, a);
            Assert.Equal(s.Result, r.Result); Assert.Equal(s.Events.Count, r.Events.Count);
            Assert.Equal(s.Players[0].Keeper, r.Players[0].Keeper); Assert.Equal(s.Meter, r.Meter);
        }
        foreach (var k in new[] { "skill", "ultimate", "status", "fusion" }) Assert.True(kinds.Contains(k), $"thiếu sự kiện {k}; có: {string.Join(",", kinds)}");
    }

    [Fact(DisplayName = "Danh mục: 8 hệ × 4 kỹ năng cấp thẻ + 8 Ultimate + 6 Fusion, id không trùng, Fusion có đủ hai hệ")]
    public void CatalogShape()
    {
        foreach (var el in Anima.Contracts.Elements.All) Assert.Equal(4, SkillCatalog.CardSkills(el).Count);
        Assert.Equal(SkillCatalog.All.Count, SkillCatalog.All.Select(x => x.Id).Distinct().Count());
        Assert.Equal(6, SkillCatalog.All.Count(x => x.Kind == SkillKind.Fusion));
        Assert.Equal(8, SkillCatalog.All.Count(x => x.Kind == SkillKind.Ultimate));
        Assert.All(SkillCatalog.All.Where(x => x.Kind == SkillKind.Fusion), f => Assert.True(SkillCatalog.FusionElements.ContainsKey(f.Id)));
    }
}
