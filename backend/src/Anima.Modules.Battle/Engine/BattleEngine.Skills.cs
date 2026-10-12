using Anima.Contracts;

namespace Anima.Battle.Engine;

public enum HitKind { Attack, Skill, Reflect, Spread, Burn, SelfDamage, Ultimate }

/// <summary>Kỹ năng, trạng thái, Emotion Meter, Resonance, Ultimate, Fusion. Số liệu và quy ước: docs/SKILLS_ANIMA.md.</summary>
public static partial class BattleEngine
{
    public const int SkillScale = 10, UltimateCost = 50, MeterMax = 100, SurgeTurns = 2, SurgePct = 20, OverchargeCap = 5, BaseSpd = 100;
    private const double SpdDodgeFactor = 0.25, DefReduction = 0.10;

    // ---------- Tra cứu ----------
    private static IEnumerable<(int Slot, FieldAnima F)> Alive(BattlePlayer p) =>
        p.Field.Select((f, i) => (i, f)).Where(x => x.f is not null).Select(x => (x.i, x.f!));

    private static bool HasSkill(FieldAnima f, string id) => f.SkillId == id;
    private static bool Has(FieldAnima f, string kind) => f.Statuses.Any(s => s.Kind == kind);
    private static int CountElement(BattlePlayer p, string element) => p.Field.Count(f => f is not null && f.Card.Element == element);
    private static bool AnySkill(BattlePlayer p, string id) => p.Field.Any(f => f is not null && f.SkillId == id);

    private static bool IsDebuffKind(string kind, double value) =>
        kind is "fear" or "stun" or "root" or "nododge" or "burn" || (kind.EndsWith("_pct", StringComparison.Ordinal) && value < 0);

    /// <summary>Tương khắc lên kỹ năng: +20% nếu người dùng khắc mục tiêu, −20% nếu bị khắc (docs/SKILLS_ANIMA.md §2).</summary>
    public static double ElementMod(string source, string target)
    {
        if (Counters(source, target) || (source == "Luminara" && target == Elements.Nihilum)) return 0.2;
        if (Counters(target, source)) return -0.2;
        return 0;
    }

    private static int Round10(double v) => (int)(Math.Round(v / 10.0, MidpointRounding.AwayFromZero) * 10);

    // ---------- Tính lại chỉ số ----------
    /// <summary>Tính lại mọi chỉ số suy ra (thưởng %, né, HP tối đa) từ trạng thái, passive, Resonance và Surge. Gọi sau mỗi thay đổi.</summary>
    public static void Recalc(BattleState s)
    {
        foreach (var p in s.Players)
        {
            var terra = CountElement(p, "Terrakin") >= 2 ? Alive(p).Where(x => x.F.Card.Element == "Terrakin").ToList() : [];
            var terraDef = terra.Count > 0 ? (int?)(terra.Sum(x => x.F.Def) / terra.Count) : null;
            foreach (var (_, f) in Alive(p))
            {
                if (f.BaseMaxHp == 0) f.BaseMaxHp = f.MaxHp;
                var el = f.Card.Element;
                double atk = 0, def = 0, spd = 0, dodge = 0, hp = 0;
                foreach (var st in f.Statuses)
                    switch (st.Kind)
                    {
                        case "atk_pct": atk += st.Value; break;
                        case "def_pct": def += st.Value; break;
                        case "spd_pct": spd += st.Value; break;
                        case "dodge_pct": dodge += st.Value; break;
                        case "hp_pct": hp += st.Value; break;
                        case "aura_def": def += 5 * st.Stacks; break;
                        case "overcharge": atk += 10 * st.Stacks; break;
                    }
                var ratio = f.MaxHp == 0 ? 1.0 : (double)f.Hp / f.MaxHp;
                if (HasSkill(f, "pyraxis.rage_bound")) atk += Math.Min(50, Math.Floor((1 - ratio) * 10 + 1e-9) * 5);
                if (HasSkill(f, "aqualis.deep_calm")) def += 10;
                if (HasSkill(f, "terrakin.unyielding") && ratio < 0.3) def += 50;
                if (HasSkill(f, "umbryx.veil_of_sorrow") && ratio < 0.5) { spd += 30; dodge += 20; }
                if (HasSkill(f, "ventara.free_spirit")) dodge += 25;
                if (HasSkill(f, "nihilum.despair") && ratio < 0.2) atk += 100;
                if (CountElement(p, el) >= 2)
                    switch (el) { case "Pyraxis": atk += 15; break; case "Voltaris": atk += 25; break; case "Umbryx": atk += 10; break; case "Terrakin": hp += 20; break; }
                if (p.SurgeLeft > 0) { atk += SurgePct; def += SurgePct; }

                f.AtkPct = (int)Math.Round(atk); f.DefPct = (int)Math.Round(def); f.SpdPct = (int)Math.Round(spd);
                f.DefBase = el == "Terrakin" ? terraDef : null;
                var effSpd = BaseSpd * (100 + f.SpdPct) / 100.0;
                dodge += (effSpd - BaseSpd) * SpdDodgeFactor;
                f.DodgePct = Has(f, "nododge") || Has(f, "root") ? 0 : Math.Clamp((int)Math.Round(dodge), 0, 75);

                var newMax = (int)(f.BaseMaxHp * (100 + hp) / 100.0);
                if (newMax > f.MaxHp) f.Hp += newMax - f.MaxHp;            // thưởng HP tối đa cũng cộng HP hiện tại
                f.MaxHp = newMax;
                if (f.Hp > f.MaxHp) f.Hp = f.MaxHp;
            }
        }
    }

    // ---------- Trạng thái ----------
    private static bool Immune(FieldAnima f, string kind, double value)
    {
        if (!IsDebuffKind(kind, value)) return false;
        if (HasSkill(f, "nihilum.emptiness")) return true;
        return HasSkill(f, "aqualis.deep_calm") && kind == "fear";
    }

    private static bool AddStatus(BattleState s, int side, FieldAnima f, string kind, int turns, double value = 0, int maxStacks = 1)
    {
        if (Immune(f, kind, value)) { Emit(s, "immune", side, ("card", f.Card.Name), ("status", kind)); return false; }
        var existing = f.Statuses.FirstOrDefault(x => x.Kind == kind && Math.Sign(x.Value) == Math.Sign(value));
        if (existing is null) f.Statuses.Add(new Status { Kind = kind, Turns = turns, Value = value });
        else if (kind is "burn" or "overcharge") { existing.Stacks = Math.Min(maxStacks, existing.Stacks + 1); existing.Turns = Math.Max(existing.Turns, turns); existing.Value = Math.Max(existing.Value, value); }
        else { existing.Turns = Math.Max(existing.Turns, turns); if (Math.Abs(value) > Math.Abs(existing.Value)) existing.Value = value; }
        Emit(s, "status", side, ("card", f.Card.Name), ("status", kind), ("turns", turns));
        return true;
    }

    /// <summary>Gây debuff lên địch: độ lớn (phần trăm) và xác suất chịu tương khắc ±20%.</summary>
    private static bool Debuff(BattleState s, int side, FieldAnima caster, FieldAnima target, string kind, int turns, double pct = 0, bool scaled = true)
    {
        var v = pct == 0 ? 0 : pct * (scaled ? 1 + ElementMod(caster.Card.Element, target.Card.Element) : 1);
        return AddStatus(s, side, target, kind, turns, v);
    }

    private static void ClearDebuffs(FieldAnima f) => f.Statuses.RemoveAll(x => IsDebuffKind(x.Kind, x.Value));

    private static bool Roll(BattleState s, double percent) => percent > 0 && s.Rng.Next(10000) < percent * 100;

    // ---------- Hồi máu ----------
    private static int Heal(BattleState s, int side, FieldAnima f, double pctOfMax)
    {
        if (HasSkill(f, "nihilum.emptiness")) return 0;
        var amount = (int)Math.Round(f.MaxHp * pctOfMax / 100.0);
        var before = f.Hp; f.Hp = Math.Min(f.MaxHp, f.Hp + amount);
        if (f.Hp > before) Emit(s, "heal", side, ("card", f.Card.Name), ("amount", f.Hp - before));
        return f.Hp - before;
    }

    // ---------- Emotion Meter ----------
    private static void AddMeter(BattleState s, int amount)
    {
        s.Meter = Math.Min(MeterMax, s.Meter + amount);
        if (s.Meter < MeterMax) return;
        s.Meter = 0;
        foreach (var p in s.Players) p.SurgeLeft = SurgeTurns;
        Emit(s, "surge", s.Active);
        Recalc(s);
    }

    // ---------- Sát thương ----------
    /// <summary>Sát thương kỹ năng: độ_lớn × SkillScale × (1 ± tương khắc), trừ DEF×10% (tối đa giảm nửa) trừ khi bỏ qua DEF.</summary>
    private static int SkillDamageAmount(FieldAnima caster, FieldAnima target, double points, bool ignoreDef = false, double mult = 1)
    {
        var raw = points * SkillScale * (1 + ElementMod(caster.Card.Element, target.Card.Element)) * mult;
        if (!ignoreDef) raw = Math.Max(raw * 0.5, raw - target.EffDef * DefReduction);
        return Math.Max(10, Round10(raw));
    }

    /// <summary>Mọi sát thương lên Anima đi qua đây: bất tử, né, khiên, giảm sát thương, chia sát thương, phản đòn, lan sét, Meter, hạ gục.</summary>
    private static int Hit(BattleState s, int srcSide, FieldAnima? src, int tSide, int tSlot, int amount, HitKind kind, bool canDodge = false)
    {
        var tp = s.Players[tSide]; var t = tp.Field[tSlot];
        if (t is null || amount <= 0) return 0;
        if (Has(t, "invuln")) { Emit(s, "blocked", tSide, ("card", t.Card.Name)); return 0; }
        if (canDodge && Roll(s, t.DodgePct))
        {
            Emit(s, "dodge", tSide, ("card", t.Card.Name));
            if (HasSkill(t, "ventara.free_spirit")) AddStatus(s, tSide, t, "atk_pct", 1, 10);
            Recalc(s);
            return 0;
        }
        var shield = t.Statuses.FirstOrDefault(x => x.Kind == "shield_hit");
        if (shield is not null) { amount /= 2; t.Statuses.Remove(shield); }
        if (t.Card.Element == "Aqualis" && CountElement(tp, "Aqualis") >= 2 && kind is not HitKind.Burn and not HitKind.SelfDamage) amount = amount * 90 / 100;
        amount = Math.Max(1, amount);

        var dealt = 0;
        if (t.Card.Element == "Umbryx" && CountElement(tp, "Umbryx") >= 2 && kind is not HitKind.Burn and not HitKind.SelfDamage)
        {
            // Shared Pain: chia đều cho mọi Umbryx trên sân; phần dư về mục tiêu.
            var group = Alive(tp).Where(x => x.F.Card.Element == "Umbryx").ToList();
            var share = amount / group.Count; var rest = amount - share * group.Count;
            foreach (var (slot, g) in group) dealt += ApplyDamage(s, tSide, slot, g, share + (g == t ? rest : 0));
        }
        else dealt = ApplyDamage(s, tSide, tSlot, t, amount);
        Emit(s, "hit", srcSide, ("target", t.Card.Name), ("damage", dealt), ("kind", kind.ToString()));

        if (src is not null && kind is HitKind.Attack or HitKind.Skill or HitKind.Ultimate && s.Phase != BattlePhase.Finished)
        {
            if (kind == HitKind.Attack) AddMeter(s, 3);
            if (tp.Field[tSlot] == t)
            {
                if (HasSkill(t, "pyraxis.fury_unleashed")) Hit(s, tSide, t, srcSide, FindSlot(s.Players[srcSide], src), Math.Max(1, dealt * 20 / 100), HitKind.Reflect);
                if (HasSkill(t, "terrakin.avalanche")) Hit(s, tSide, t, srcSide, FindSlot(s.Players[srcSide], src), (int)(150 * (t.EffDef > 1000 ? 1.5 : 1)), HitKind.Reflect);
            }
            if (HasSkill(src, "voltaris.chain_reaction") && s.Players[srcSide].Field[FindSlot(s.Players[srcSide], src)] == src)
            {
                var chance = CountElement(s.Players[srcSide], "Voltaris") >= 2 ? 50 : 25;
                var others = Alive(tp).Where(x => x.F != t).ToList();
                if (others.Count > 0 && Roll(s, chance))
                {
                    var (slot, _) = others[s.Rng.Next(others.Count)];
                    Emit(s, "chain_reaction", srcSide, ("card", src.Card.Name));
                    Hit(s, srcSide, src, tSide, slot, Math.Max(1, dealt / 2), HitKind.Spread);
                }
            }
        }
        return dealt;
    }

    private static int FindSlot(BattlePlayer p, FieldAnima f) => Array.IndexOf(p.Field, f);

    /// <summary>Trừ HP và xử lý khi về 0: Unyielding, The Void Calls, rồi hạ gục.</summary>
    private static int ApplyDamage(BattleState s, int side, int slot, FieldAnima t, int amount)
    {
        var p = s.Players[side];
        var ratioBefore = t.MaxHp == 0 ? 1.0 : (double)t.Hp / t.MaxHp;
        var before = t.Hp; t.Hp -= amount;
        if (t.Hp > 0) { Recalc(s); return amount; }
        if (HasSkill(t, "terrakin.unyielding") && ratioBefore < 0.3 && t.Used.Add("unyielding")) { t.Hp = 1; Emit(s, "survive", side, ("card", t.Card.Name)); Recalc(s); return before - 1; }
        if (t.Card.Element == "Nihilum" && CountElement(p, "Nihilum") >= 2 && p.TeamUsed.Add("void_calls"))
        {
            t.Hp = Math.Max(1, t.MaxHp * 30 / 100); Emit(s, "revive", side, ("card", t.Card.Name)); Recalc(s); return before - t.Hp;
        }
        Kill(s, side, slot, t);
        return before;
    }

    private static void Kill(BattleState s, int side, int slot, FieldAnima t)
    {
        var p = s.Players[side];
        p.Graveyard.Add(t.Card); p.Field[slot] = null;
        Recalc(s);                                                                      // Resonance/HP tối đa phụ thuộc số Anima còn trên sân
        Emit(s, "defeated", side, ("card", t.Card.Name), ("slot", slot));
        AddMeter(s, 5);
        foreach (var (_, ally) in Alive(p).ToList())
        {
            if (HasSkill(ally, "luminara.hopes_echo") && ally.Used.Add("hopes_echo"))
                foreach (var (_, other) in Alive(p)) if (other != ally) Heal(s, side, other, 15);
            if (HasSkill(ally, "umbryx.lonely_howl") && ally.Used.Add("lonely_howl"))
                foreach (var (_, foe) in Alive(s.Players[1 - side]).ToList()) Debuff(s, 1 - side, ally, foe, "atk_pct", 2, -15);
        }
        Recalc(s);
    }

    // ---------- Mục tiêu ----------
    private static FieldAnima EnemyAt(BattleState s, int side, int slot)
    {
        var foes = s.Players[1 - side];
        if (slot < 0 || slot >= BattlePlayer.Slots || foes.Field[slot] is not { } f) throw new BattleException("TARGET_INVALID", "There is no enemy Anima in that slot");
        if (!Has(f, "taunt") && Alive(foes).Any(x => Has(x.F, "taunt"))) throw new BattleException("TARGET_TAUNTED", "You must target the taunting Anima");
        return f;
    }

    private static FieldAnima AllyAt(BattleState s, int side, int slot)
    {
        var mine = s.Players[side];
        if (slot < 0 || slot >= BattlePlayer.Slots || mine.Field[slot] is not { } f) throw new BattleException("TARGET_INVALID", "There is no allied Anima in that slot");
        return f;
    }

    // ---------- Hành động kỹ năng ----------
    private static FieldAnima RequireActor(BattleState s, BattleAction a)
    {
        var me = s.Me;
        if (a.Slot is not { } si || si < 0 || si >= BattlePlayer.Slots || me.Field[si] is not { } f) throw new BattleException("INVALID_ACTION", "Pick one of your Anima");
        if (f.Summoned || f.Attacked || Has(f, "stun")) throw new BattleException("ATTACK_NOT_ALLOWED", "This Anima cannot act right now");
        return f;
    }

    private static void UseSkill(BattleState s, BattleAction a)
    {
        var f = RequireActor(s, a);
        var def = SkillCatalog.Find(f.SkillId);
        if (def is null || def.Kind != SkillKind.Active) throw new BattleException("SKILL_NOT_AVAILABLE", "This Anima has no active skill");
        if (f.Cd.GetValueOrDefault(def.Id) > 0) throw new BattleException("SKILL_ON_COOLDOWN", "This skill is recharging");
        var target = a.Target ?? -1;
        // Kiểm mục tiêu trước khi tiêu hao gì.
        if (def.Target == SkillTarget.Enemy) EnemyAt(s, s.Active, target);
        if (def.Target == SkillTarget.Ally) AllyAt(s, s.Active, target);
        f.Cd[def.Id] = def.Cooldown; f.Attacked = true;
        Emit(s, "skill", s.Active, ("card", f.Card.Name), ("skill", def.Name), ("id", def.Id));
        RunActive(s, s.Active, f, def, target);
        if (s.Phase != BattlePhase.Finished) AddMeter(s, 5);
        Recalc(s);
    }

    private static void RunActive(BattleState s, int side, FieldAnima f, SkillDef def, int target)
    {
        var me = s.Players[side]; var foes = s.Players[1 - side];
        FieldAnima? foe() => foes.Field[target];
        void Skill(FieldAnima t, int slot, double pts, double mult = 1, bool ignoreDef = false) => Hit(s, side, f, 1 - side, slot, SkillDamageAmount(f, t, pts, ignoreDef, mult), HitKind.Skill, canDodge: true);
        switch (def.Id)
        {
            case "luminara.dawns_embrace": { var t = AllyAt(s, side, target); Heal(s, side, t, t.Hp * 100 < t.MaxHp * 30 ? 40 : 20); break; }
            case "luminara.blinding_light": { var t = foe()!; Skill(t, target, 30); if (foes.Field[target] == t) Debuff(s, 1 - side, f, t, "atk_pct", 2, -20); break; }
            case "umbryx.silent_tear": { var t = foe()!; Skill(t, target, 25, t.Statuses.Any(x => IsDebuffKind(x.Kind, x.Value)) ? 1.5 : 1); break; }
            case "umbryx.nightmare_touch": { var t = foe()!; Skill(t, target, 20); if (foes.Field[target] == t) AddStatus(s, 1 - side, t, "fear", 2); break; }
            case "pyraxis.ember_strike": { var t = foe()!; Skill(t, target, 35); if (foes.Field[target] == t) Burn(s, side, f, t, 5); break; }
            case "pyraxis.flame_vortex":
                foreach (var (slot, t) in Alive(foes).ToList()) { Hit(s, side, f, 1 - side, slot, SkillDamageAmount(f, t, 25), HitKind.Skill); if (foes.Field[slot] == t) Burn(s, side, f, t, 3); }
                break;
            case "aqualis.tidal_memory": { var t = foe()!; Skill(t, target, 20); AddMeter(s, 5); break; }
            case "aqualis.undertow": { var t = foe()!; Debuff(s, 1 - side, f, t, "spd_pct", 2, -30); AddStatus(s, 1 - side, t, "nododge", 2); break; }
            case "terrakin.stone_wall": AddStatus(s, side, f, "def_pct", 2, 50); AddStatus(s, side, f, "taunt", 2); break;
            case "terrakin.earths_grasp": { var t = foe()!; AddStatus(s, 1 - side, t, "root", 2); break; }
            case "ventara.wind_step":
                {
                    var t = AllyAt(s, side, target); var from = FindSlot(me, f);
                    if (t != f) { me.Field[from] = t; me.Field[target] = f; }
                    AddStatus(s, side, f, "spd_pct", 2, 20); AddStatus(s, side, t, "spd_pct", 2, 20); break;
                }
            case "ventara.gust_barrier": AddStatus(s, side, f, "shield_hit", 99); AddStatus(s, side, f, "dodge_pct", 2, 15); break;
            case "voltaris.surge_bolt": { var t = foe()!; Skill(t, target, 40); if (foes.Field[target] == t && Roll(s, 30 * (1 + ElementMod(f.Card.Element, t.Card.Element)))) AddStatus(s, 1 - side, t, "stun", 1); break; }
            case "voltaris.static_field":
                foreach (var (slot, t) in Alive(foes).ToList())
                {
                    Debuff(s, 1 - side, f, t, "spd_pct", 2, -20);
                    if (Roll(s, 20 * (1 + ElementMod(f.Card.Element, t.Card.Element)))) AddStatus(s, 1 - side, t, "stun", 1);
                }
                break;
            case "nihilum.void_touch": { var t = foe()!; Skill(t, target, 50); SelfLoss(s, side, f, 10); break; }
            case "nihilum.oblivion":
                {
                    var t = foe()!;
                    var buff = t.Statuses.FirstOrDefault(x => !IsDebuffKind(x.Kind, x.Value));
                    if (buff is not null) { t.Statuses.Remove(buff); Emit(s, "dispel", 1 - side, ("card", t.Card.Name), ("status", buff.Kind)); }
                    Skill(t, target, 30); break;
                }
            default: throw new BattleException("SKILL_NOT_AVAILABLE", "Unknown skill");
        }
    }

    private static void Burn(BattleState s, int side, FieldAnima caster, FieldAnima target, double perTurn)
    {
        var cap = CountElement(s.Players[side], "Pyraxis") >= 2 ? 3 : 1;
        var v = perTurn * SkillScale * (1 + ElementMod(caster.Card.Element, target.Card.Element));
        if (Immune(target, "burn", 1)) return;
        var ex = target.Statuses.FirstOrDefault(x => x.Kind == "burn");
        if (ex is null) target.Statuses.Add(new Status { Kind = "burn", Turns = 3, Value = v });
        else { ex.Stacks = Math.Min(cap, ex.Stacks + 1); ex.Turns = 3; ex.Value = Math.Max(ex.Value, v); }
        Emit(s, "status", 1 - side, ("card", target.Card.Name), ("status", "burn"), ("turns", 3));
    }

    private static void SelfLoss(BattleState s, int side, FieldAnima f, double pctMax)
    {
        var slot = FindSlot(s.Players[side], f);
        if (slot < 0) return;
        Hit(s, side, null, side, slot, Math.Max(1, (int)(f.MaxHp * pctMax / 100)), HitKind.SelfDamage);
    }

    // ---------- Ultimate ----------
    private static void UseUltimate(BattleState s, BattleAction a)
    {
        var f = RequireActor(s, a);
        if (Has(f, "fear")) throw new BattleException("ULTIMATE_BLOCKED", "This Anima is feared and cannot use an Ultimate");
        var def = SkillCatalog.UltimateOf(f.Card.Element);
        if (s.Meter < UltimateCost) throw new BattleException("EMOTION_INSUFFICIENT", "The Emotion Meter is too low");
        var side = s.Active; var me = s.Me; var foes = s.Opp; var target = a.Target ?? -1;
        if (def.Target == SkillTarget.Enemy) EnemyAt(s, side, target);
        if (def.Target == SkillTarget.Ally) AllyAt(s, side, target);
        s.Meter -= UltimateCost; f.Attacked = true;
        Emit(s, "ultimate", side, ("card", f.Card.Name), ("skill", def.Name), ("id", def.Id));

        // Echo of Loss: một Aqualis bên địch (sẵn sàng) giảm 50% sát thương Ultimate này.
        var echo = Alive(foes).Select(x => x.F).FirstOrDefault(x => HasSkill(x, "aqualis.echo_of_loss") && x.Cd.GetValueOrDefault("aqualis.echo_of_loss") == 0 && !Has(x, "stun"));
        var mult = 1.0;
        if (echo is not null) { mult = 0.5; echo.Cd["aqualis.echo_of_loss"] = 2; Emit(s, "echo_of_loss", 1 - side, ("card", echo.Card.Name)); }
        void Ult(FieldAnima t, int slot, double pts) => Hit(s, side, f, 1 - side, slot, SkillDamageAmount(f, t, pts, mult: mult), HitKind.Ultimate);

        switch (def.Id)
        {
            case "luminara.ultimate": { var t = AllyAt(s, side, target); Heal(s, side, t, 100); ClearDebuffs(t); break; }
            case "umbryx.ultimate":
                foreach (var (slot, t) in Alive(foes).ToList()) Ult(t, slot, 60);
                Heal(s, side, f, 30); break;
            case "pyraxis.ultimate":
                {
                    var t = foes.Field[target]!; Ult(t, target, 100);
                    if (foes.Field[target] != t) Heal(s, side, f, 50);
                    break;
                }
            case "aqualis.ultimate": foreach (var (_, t) in Alive(me).ToList()) { Heal(s, side, t, 50); ClearDebuffs(t); } break;
            case "terrakin.ultimate": AddStatus(s, side, f, "invuln", 2); foreach (var (_, t) in Alive(me).ToList()) Heal(s, side, t, 30); break;
            case "ventara.ultimate":
                for (var i = 0; i < 5; i++)
                {
                    var live = Alive(foes).ToList(); if (live.Count == 0) break;
                    var (slot, t) = live[s.Rng.Next(live.Count)]; Ult(t, slot, 20);
                }
                break;
            case "voltaris.ultimate":
                foreach (var (slot, t) in Alive(foes).ToList())
                {
                    Ult(t, slot, 80);
                    if (foes.Field[slot] == t && Roll(s, 50 * (1 + ElementMod(f.Card.Element, t.Card.Element)))) AddStatus(s, 1 - side, t, "stun", 1);
                }
                break;
            case "nihilum.ultimate": { var t = foes.Field[target]!; Ult(t, target, 120); SelfLoss(s, side, f, 30); break; }
        }
        if (s.Phase != BattlePhase.Finished) AddMeter(s, 5);
        Recalc(s);
    }

    // ---------- Fusion ----------
    private static void UseFusion(BattleState s, BattleAction a)
    {
        var def = SkillCatalog.Find(a.Id);
        if (def is null || def.Kind != SkillKind.Fusion) throw new BattleException("FUSION_NOT_AVAILABLE", "Unknown fusion");
        var side = s.Active; var me = s.Me; var foes = s.Opp; var target = a.Target ?? -1;
        var (e1, e2) = SkillCatalog.FusionElements[def.Id];
        if (CountElement(me, e1) < 1 || CountElement(me, e2) < 1) throw new BattleException("FUSION_NOT_AVAILABLE", "You need both elements on the field");
        if (me.TeamUsed.Contains("fusion:" + def.Id)) throw new BattleException("FUSION_NOT_AVAILABLE", "This fusion was already used");
        if (me.FusionThisTurn) throw new BattleException("FUSION_NOT_AVAILABLE", "Only one fusion per turn");
        if (def.Id == "hopes_last_stand")
        {
            var cur = Alive(me).Sum(x => x.F.Hp); var max = Alive(me).Sum(x => x.F.MaxHp);
            if (cur * 100 >= max * 30) throw new BattleException("FUSION_NOT_AVAILABLE", "The team must be below 30% HP");
            AllyAt(s, side, target);
        }
        if (def.Target == SkillTarget.Enemy) EnemyAt(s, side, target);
        me.TeamUsed.Add("fusion:" + def.Id); me.FusionThisTurn = true;
        Emit(s, "fusion", side, ("skill", def.Name), ("id", def.Id));
        var caster = Alive(me).First(x => x.F.Card.Element == e1).F;
        switch (def.Id)
        {
            case "dawnbreak": foreach (var (_, t) in Alive(me)) { Heal(s, side, t, 30); AddStatus(s, side, t, "atk_pct", 2, 20); } break;
            case "silent_storm":
                foreach (var (slot, t) in Alive(foes).ToList()) { Hit(s, side, caster, 1 - side, slot, SkillDamageAmount(caster, t, 40), HitKind.Ultimate); if (foes.Field[slot] == t) AddStatus(s, 1 - side, t, "stun", 1); }
                break;
            case "eternal_tide": foreach (var (_, t) in Alive(me)) { AddStatus(s, side, t, "invuln", 2); Heal(s, side, t, 20); } break;   // 2 = lượt hiện tại + lượt kế của địch
            case "chaos_theory_fusion":
                foreach (var (_, t) in Alive(foes).ToList())
                {
                    if (HasSkill(t, "aqualis.deep_calm")) { Emit(s, "immune", 1 - side, ("card", t.Card.Name), ("status", "chaos")); continue; }
                    switch (s.Rng.Next(5))
                    {
                        case 0: Burn(s, side, caster, t, 3); break;
                        case 1: AddStatus(s, 1 - side, t, "fear", 2); break;
                        case 2: AddStatus(s, 1 - side, t, "spd_pct", 2, -30); break;
                        case 3: AddStatus(s, 1 - side, t, "atk_pct", 2, -20); break;
                        default: AddStatus(s, 1 - side, t, "def_pct", 2, -20); break;
                    }
                }
                break;
            case "voids_edge": { var t = foes.Field[target]!; Hit(s, side, caster, 1 - side, target, SkillDamageAmount(caster, t, 60, ignoreDef: true), HitKind.Ultimate); break; }
            case "hopes_last_stand": { var t = AllyAt(s, side, target); Heal(s, side, t, 100); break; }
        }
        if (s.Phase != BattlePhase.Finished) AddMeter(s, 5);
        Recalc(s);
    }

    // ---------- Resonance khi ra sân ----------
    private static void AfterPlay(BattleState s, int side)
    {
        var p = s.Players[side];
        if (CountElement(p, "Luminara") >= 2 && p.TeamUsed.Add("sacred_bond"))
            foreach (var (_, f) in Alive(p).Where(x => x.F.Card.Element == "Luminara").ToList())
            { AddStatus(s, side, f, "hp_pct", 3, 30); AddStatus(s, side, f, "def_pct", 3, 20); Emit(s, "resonance", side, ("skill", "Sacred Bond")); }
        Recalc(s);
    }

    // ---------- Đầu lượt / cuối lượt ----------
    private static void StartTurnEffects(BattleState s, int side)
    {
        var p = s.Players[side];
        p.FusionThisTurn = false;
        foreach (var (_, f) in Alive(p)) foreach (var k in f.Cd.Keys.ToList()) f.Cd[k] = Math.Max(0, f.Cd[k] - 1);

        // Radiant Aura: cộng dồn DEF cho đồng đội; hết nguồn thì mất.
        var aura = AnySkill(p, "luminara.radiant_aura");
        foreach (var (_, f) in Alive(p))
        {
            var st = f.Statuses.FirstOrDefault(x => x.Kind == "aura_def");
            if (aura) { if (st is null) f.Statuses.Add(new Status { Kind = "aura_def", Turns = 99, Stacks = 1 }); else st.Stacks = Math.Min(5, st.Stacks + 1); }
            else if (st is not null) f.Statuses.Remove(st);
            if (HasSkill(f, "voltaris.overcharge")) AddStatus(s, side, f, "overcharge", 99, 10, OverchargeCap);
        }
        Recalc(s);

        foreach (var (slot, f) in Alive(p).ToList())
        {
            if (p.Field[slot] != f) continue;
            var burn = f.Statuses.FirstOrDefault(x => x.Kind == "burn");
            if (burn is not null) { Emit(s, "burn", side, ("card", f.Card.Name)); Hit(s, 1 - side, null, side, slot, (int)(burn.Value * burn.Stacks), HitKind.Burn); }
            if (p.Field[slot] != f) continue;
            if (HasSkill(f, "nihilum.despair") && f.Hp * 100 < f.MaxHp * 20) SelfLoss(s, side, f, 5);
            if (p.Field[slot] != f) continue;
            if (f.Card.Element == "Aqualis" && CountElement(p, "Aqualis") >= 2) Heal(s, side, f, 5);
            if (HasSkill(f, "ventara.curiosity") && Roll(s, 30))
            {
                var kind = new[] { "atk_pct", "def_pct", "spd_pct" }[s.Rng.Next(3)];
                AddStatus(s, side, f, kind, 2, 15);
            }
        }
        // Chaos Theory (Resonance Ventara): một Ventara ngẫu nhiên dùng miễn phí 1 Active đã sẵn sàng.
        if (CountElement(p, "Ventara") >= 2)
        {
            var ready = Alive(p).Where(x => x.F.Card.Element == "Ventara" && SkillCatalog.Find(x.F.SkillId) is { Kind: SkillKind.Active } d && x.F.Cd.GetValueOrDefault(d.Id) == 0).ToList();
            if (ready.Count > 0)
            {
                var (_, f) = ready[s.Rng.Next(ready.Count)]; var d = SkillCatalog.Find(f.SkillId)!;
                var tgt = d.Id == "ventara.wind_step" ? Alive(p).Where(x => x.F != f).Select(x => x.Slot).DefaultIfEmpty(-1).First() : FindSlot(p, f);
                if (tgt >= 0) { Emit(s, "resonance", side, ("skill", "Chaos Theory")); RunActive(s, side, f, d, tgt); }
            }
        }
        Recalc(s);
    }

    private static void EndTurnEffects(BattleState s, int side)
    {
        var p = s.Players[side];
        foreach (var (_, f) in Alive(p))
        {
            if (HasSkill(f, "voltaris.overcharge") && !f.Attacked)
            {
                var st = f.Statuses.FirstOrDefault(x => x.Kind == "overcharge");
                if (st is not null) { st.Stacks = st.Stacks / 2; if (st.Stacks == 0) f.Statuses.Remove(st); }
            }
            foreach (var st in f.Statuses.ToList()) if (st.Turns < 90 && --st.Turns <= 0) f.Statuses.Remove(st);
            f.TempAtk = 0;
        }
        if (p.SurgeLeft > 0) p.SurgeLeft--;
        Recalc(s);
    }
}
