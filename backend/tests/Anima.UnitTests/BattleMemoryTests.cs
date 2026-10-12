using Anima.Battle.Engine;
using Xunit;

namespace Anima.UnitTests;

/// <summary>Ký ức phong ấn (BR-CARD-05) và Ultimate Secret Rare (BR-SKL-04).</summary>
public class BattleMemoryTests
{
    private static BattleCard Card(string el, string rarity = "common", string? skill = null, int hp = 3000) =>
        new(1, "x", el, "anima", rarity, 1, 1000, 500, hp, skill);

    private static BattleState Playing(bool memoryAllowed = true)
    {
        var deck = Enumerable.Range(0, 30).Select(i => Card(Anima.Contracts.Elements.Cycle[i % 7])).ToList();
        var s = BattleEngine.Start(new BattleSetup([1], deck, deck, null, memoryAllowed));
        s.EchoChancePct = 0;
        BattleEngine.Apply(s, new BattleAction(0, "keep")); BattleEngine.Apply(s, new BattleAction(1, "keep"));
        return s;
    }

    private static FieldAnima Put(BattleState s, int side, int slot, BattleCard c)
    {
        var f = new FieldAnima { Card = c, Uid = s.NextUid++, Atk = c.Atk, Def = c.Def, Hp = c.Hp, MaxHp = c.Hp, BaseMaxHp = c.Hp };
        s.Players[side].Field[slot] = f; BattleEngine.Recalc(s); return f;
    }

    private static void Use(BattleState s, int defId, int slot) => BattleEngine.Apply(s, new BattleAction(s.Active, "memory", Slot: slot, Id: defId.ToString()));
    private static string Fails(BattleState s, int defId, int slot) => Assert.Throws<BattleException>(() => Use(s, defId, slot)).Code;
    private static void End(BattleState s) => BattleEngine.Apply(s, new BattleAction(s.Active, "end"));
    private static readonly MemoryCard FreeSpirit = new(66, "Ventara", "ventara.free_spirit");      // Passive: né +25%

    [Fact(DisplayName = "Ký ức phong ấn: Anima cùng hệ mượn Passive gốc, hết hiệu lực ở đầu lượt kế tiếp của chủ; thẻ bị tiêu")]
    public void BorrowsPassiveForOneRound()
    {
        var s = Playing(); var me = s.Active; var t = Put(s, me, 0, Card("Ventara"));
        s.Players[me].Memories.Add(FreeSpirit);
        Assert.Equal(0, t.DodgePct);
        Use(s, 66, 0);
        Assert.Equal(25, t.DodgePct); Assert.Empty(s.Players[me].Memories);
        End(s); Assert.Equal(25, t.DodgePct);                                            // còn hiệu lực qua lượt của địch
        End(s);
        Assert.Equal(me, s.Active); Assert.Null(t.BorrowedSkill); Assert.Equal(0, t.DodgePct);
    }

    [Fact(DisplayName = "Ký ức phong ấn: cần Anima cùng hệ trên sân (Echo không tính) và phải có thẻ trong kho")]
    public void NeedsMatchingElementAndStock()
    {
        var s = Playing(); var me = s.Active; Put(s, me, 0, Card("Pyraxis"));
        Assert.Equal("MEMORY_NOT_AVAILABLE", Fails(s, 66, 0));
        s.Players[me].Memories.Add(FreeSpirit);
        Assert.Equal("NO_MATCHING_ELEMENT", Fails(s, 66, 0));                           // Pyraxis không phải Ventara
        Assert.Equal("NO_MATCHING_ELEMENT", Fails(s, 66, 2));                           // ô trống
        Put(s, me, 1, Card("Ventara"));
        var echo = new FieldAnima { Card = Card("Ventara"), Uid = s.NextUid++, Atk = 1, Def = 1, Hp = 100, MaxHp = 100, BaseMaxHp = 100, IsEcho = true, EchoTurns = 2 };
        s.Players[me].Field[2] = echo;
        Assert.Equal("NO_MATCHING_ELEMENT", Fails(s, 66, 2));
        Use(s, 66, 1);
    }

    [Fact(DisplayName = "Ký ức phong ấn: mỗi lượt một cái, hồi chiêu chung 3 lượt")]
    public void OnePerTurnAndSharedCooldown()
    {
        var s = Playing(); var me = s.Active; Put(s, me, 0, Card("Ventara"));
        s.Players[me].Memories.AddRange([FreeSpirit, new MemoryCard(67, "Ventara", "ventara.free_spirit"), new MemoryCard(68, "Ventara", "ventara.free_spirit")]);
        Use(s, 66, 0);
        Assert.Equal("ALREADY_USED_THIS_TURN", Fails(s, 67, 0));
        End(s); End(s);                                                                 // lượt 2 của ta: CD 3 → 2
        Assert.Equal("ON_COOLDOWN", Fails(s, 67, 0));
        End(s); End(s); Assert.Equal("ON_COOLDOWN", Fails(s, 67, 0));                   // CD 1
        End(s); End(s); Use(s, 67, 0);                                                  // CD về 0
    }

    [Fact(DisplayName = "Ký ức phong ấn: trận bị cấm (Ranked PvP) trả NOT_ALLOWED_IN_RANKED")]
    public void BannedInRanked()
    {
        var s = Playing(memoryAllowed: false); var me = s.Active; Put(s, me, 0, Card("Ventara")); s.Players[me].Memories.Add(FreeSpirit);
        Assert.Equal("NOT_ALLOWED_IN_RANKED", Fails(s, 66, 0));
    }

    [Fact(DisplayName = "Ultimate Secret Rare: tiêu đủ 50 Meter (không hoàn), sát thương và hồi máu ×1,5; thẻ thường net −20")]
    public void SecretUltimate()
    {
        (int Meter, int Loss) Run(string rarity)
        {
            var s = Playing(); var me = s.Active; Put(s, me, 0, Card("Umbryx", rarity)); var foe = Put(s, 1 - me, 0, Card("Terrakin", hp: 9000));
            s.Meter = 100; BattleEngine.Apply(s, new BattleAction(me, "ultimate", Slot: 0));
            return (s.Meter, 9000 - foe.Hp);
        }
        var normal = Run("common"); var secret = Run("secret");
        Assert.Equal(80, normal.Meter); Assert.Equal(50, secret.Meter);
        Assert.InRange((double)secret.Loss / normal.Loss, 1.45, 1.55);
    }
}
