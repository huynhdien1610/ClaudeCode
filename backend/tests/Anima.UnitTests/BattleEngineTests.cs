using Anima.Battle.Engine;
using Xunit;

namespace Anima.UnitTests;

/// <summary>Luật trận đấu: SC-BTL-01..12, SC-ELM-01..05 (BR-BTL, BR-ELM).</summary>
public class BattleEngineTests
{
    private static BattleCard C(string element, int atk = 1000, int def = 500, int hp = 2000, int cost = 1, string name = "x", string type = "anima") =>
        new(1, name, element, type, "common", cost, atk, def, hp);

    private static List<BattleCard> Deck(int n = 30) => Enumerable.Range(0, n).Select(i => C(Elements(i), name: $"c{i}")).ToList();
    private static string Elements(int i) => Anima.Contracts.Elements.Cycle[i % 7];

    private static BattleState Fresh(byte seed = 1, List<BattleCard>? d0 = null, List<BattleCard>? d1 = null) =>
        BattleEngine.Start(new BattleSetup([seed], d0 ?? Deck(), d1 ?? Deck()));

    /// <summary>Hai bên giữ tay đầu → vào lượt 1 của người đi trước.</summary>
    private static BattleState Playing(byte seed = 1, List<BattleCard>? d0 = null, List<BattleCard>? d1 = null)
    {
        var s = Fresh(seed, d0, d1);
        BattleEngine.Apply(s, new BattleAction(0, "keep")); BattleEngine.Apply(s, new BattleAction(1, "keep"));
        Assert.Equal(BattlePhase.Main, s.Phase);
        return s;
    }

    private static void End(BattleState s) => BattleEngine.Apply(s, new BattleAction(s.Active, "end"));
    private static BattleException Fails(BattleState s, BattleAction a) => Assert.Throws<BattleException>(() => BattleEngine.Apply(s, a));
    private static FieldAnima Put(BattlePlayer p, int slot, BattleCard c, bool summoned = false) =>
        p.Field[slot] = new FieldAnima { Card = c, Atk = c.Atk, Def = c.Def, Hp = c.Hp, MaxHp = c.Hp, Summoned = summoned };

    // ---------- Hệ và sát thương ----------
    [Theory(DisplayName = "SC-ELM-01: hệ số theo cặp hệ")]
    [InlineData("Umbryx", "Aqualis", 1250)]
    [InlineData("Pyraxis", "Terrakin", 1250)]
    [InlineData("Aqualis", "Ventara", 1250)]
    [InlineData("Terrakin", "Voltaris", 1250)]
    [InlineData("Ventara", "Luminara", 1250)]
    [InlineData("Voltaris", "Umbryx", 1250)]
    [InlineData("Luminara", "Pyraxis", 1250)]
    [InlineData("Pyraxis", "Luminara", 750)]
    [InlineData("Voltaris", "Aqualis", 1000)]
    [InlineData("Nihilum", "Luminara", 1100)]
    [InlineData("Luminara", "Nihilum", 1500)]
    [InlineData("Pyraxis", "Nihilum", 1000)]
    [InlineData("Nihilum", "Nihilum", 1000)]
    [InlineData("Umbryx", "Umbryx", 1000)]
    public void Multipliers(string atk, string def, int expected) => Assert.Equal(expected, BattleEngine.Multiplier(atk, def));

    [Theory(DisplayName = "SC-BTL-03: công thức sát thương (tối thiểu 100, làm tròn hàng chục từ 5 lên)")]
    [InlineData("Voltaris", 1800, "Aqualis", 900, 900)]
    [InlineData("Voltaris", 800, "Aqualis", 1000, 100)]
    [InlineData("Umbryx", 1600, "Aqualis", 700, 1130)]
    [InlineData("Aqualis", 1600, "Umbryx", 700, 680)]
    [InlineData("Nihilum", 1600, "Terrakin", 700, 990)]
    [InlineData("Luminara", 1600, "Nihilum", 700, 1350)]
    public void DamageTable(string ae, int atk, string de, int def, int expected) => Assert.Equal(expected, BattleEngine.Damage(ae, atk, de, def));

    [Fact(DisplayName = "Vòng sinh và vòng khắc đúng bảng BR-ELM-01/02 cho cả 7 hệ")]
    public void CyclesConsistent()
    {
        var cycle = Anima.Contracts.Elements.Cycle;
        for (var i = 0; i < 7; i++)
        {
            Assert.True(BattleEngine.Generates(cycle[i], cycle[(i + 1) % 7]));
            Assert.False(BattleEngine.Generates(cycle[(i + 1) % 7], cycle[i]));
            Assert.True(BattleEngine.Counters(cycle[i], cycle[(i + 2) % 7]));
        }
        Assert.False(BattleEngine.Generates("Nihilum", "Umbryx"));
    }

    // ---------- Lượt, Cộng hưởng, tấn công ----------
    [Theory(DisplayName = "SC-BTL-01: Cộng hưởng của lượt n = min(n, 6), không cộng dồn")]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(6, 6)]
    [InlineData(9, 6)]
    public void EnergyPerTurn(int ownTurn, int expected)
    {
        var s = Playing(); var first = s.Active;
        while (s.Players[first].OwnTurns < ownTurn) End(s);
        Assert.Equal(first, s.Active);
        Assert.Equal(expected, s.Me.Energy);
    }

    [Fact(DisplayName = "BR-BTL-02/03: tay đầu 5 lá, người đi sau bốc thêm 1; đầu mỗi lượt bốc 1")]
    public void OpeningHands()
    {
        var s = Playing();
        var first = s.Players[s.First]; var second = s.Players[1 - s.First];
        Assert.Equal(6, first.Hand.Count);          // 5 + bốc đầu lượt 1
        Assert.Equal(6, second.Hand.Count);         // 5 + 1 lá bù đầu trận
        End(s); Assert.Equal(7, second.Hand.Count); // lượt 1 của người đi sau: bốc thêm 1
    }

    [Fact(DisplayName = "SC-BTL-02: Anima vừa ra sân không tấn công trong lượt đó; mỗi Anima chỉ đánh 1 lần/lượt")]
    public void SummoningSickness()
    {
        var s = Playing(); var me = s.Me;
        me.Hand.Insert(0, C("Pyraxis", name: "Cinderpup", cost: 1));
        BattleEngine.Apply(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 0));
        Put(s.Opp, 0, C("Aqualis"));
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, new BattleAction(s.Active, "attack", Slot: 0, Target: 0)).Code);
        End(s); End(s);                                                    // sang lượt sau của mình
        var opp = s.Opp; Put(opp, 0, C("Aqualis", hp: 5000));
        BattleEngine.Apply(s, new BattleAction(s.Active, "attack", Slot: 0, Target: 0));
        Assert.Equal("ATTACK_NOT_ALLOWED", Fails(s, new BattleAction(s.Active, "attack", Slot: 0, Target: 0)).Code);
    }

    [Fact(DisplayName = "SC-BTL-04/05: chỉ đánh thẳng Keeper khi sân đối phương trống; sát thương = ATK hiệu lực")]
    public void DirectAttack()
    {
        var s = Playing();
        Put(s.Me, 0, C("Voltaris", atk: 1800)); Put(s.Opp, 1, C("Aqualis"));
        Assert.Equal("DIRECT_ATTACK_NOT_ALLOWED", Fails(s, new BattleAction(s.Active, "attack", Slot: 0, Target: -1)).Code);
        Assert.False(s.Me.Field[0]!.Attacked);                              // lần bị từ chối không tiêu lần tấn công
        s.Opp.Field[1] = null;
        BattleEngine.Apply(s, new BattleAction(s.Active, "attack", Slot: 0, Target: -1));
        Assert.Equal(6200, s.Opp.Keeper);
    }

    [Fact(DisplayName = "SC-BTL-06: Anima có HP về đúng 0 thì rời sân vào mộ")]
    public void ExactLethalGoesToGraveyard()
    {
        var s = Playing();
        Put(s.Me, 0, C("Voltaris", atk: 1800)); Put(s.Opp, 0, C("Aqualis", def: 900, hp: 900));    // 900 × 1.0 = 900
        BattleEngine.Apply(s, new BattleAction(s.Active, "attack", Slot: 0, Target: 0));
        Assert.Null(s.Opp.Field[0]); Assert.Single(s.Opp.Graveyard);
    }

    [Fact(DisplayName = "Ra sân: đủ Cộng hưởng, ô trống, chỉ Anima; thẻ hỗ trợ chưa chơi được ở bản này")]
    public void PlayValidation()
    {
        var s = Playing(); var me = s.Me;
        me.Hand.Clear(); me.Hand.AddRange([C("Umbryx", cost: 5), C("Umbryx", cost: 1), C("Umbryx", cost: 1, type: "echo")]);
        Assert.Equal("INSUFFICIENT_RESONANCE", Fails(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 0)).Code);
        Assert.Equal("CARD_NOT_PLAYABLE", Fails(s, new BattleAction(s.Active, "play", Hand: 2, Slot: 0)).Code);
        BattleEngine.Apply(s, new BattleAction(s.Active, "play", Hand: 1, Slot: 0));
        me.Hand.Insert(0, C("Umbryx", cost: 1));
        Assert.Equal("SLOT_OCCUPIED", Fails(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 0)).Code);
        Assert.Equal("NOT_YOUR_TURN", Fails(s, new BattleAction(1 - s.Active, "end")).Code);
    }

    // ---------- Sinh và chuỗi nhân quả ----------
    [Fact(DisplayName = "SC-ELM-02: ra sân khi có hệ sinh ra mình → +200 ATK và +200 HP")]
    public void Generation()
    {
        var s = Playing(); Put(s.Me, 0, C("Umbryx"));
        s.Me.Hand.Insert(0, C("Pyraxis", atk: 1500, hp: 2000, cost: 1));
        BattleEngine.Apply(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 1));
        Assert.Equal(1700, s.Me.Field[1]!.Atk); Assert.Equal(2200, s.Me.Field[1]!.Hp); Assert.Equal(2200, s.Me.Field[1]!.MaxHp);
    }

    [Fact(DisplayName = "SC-ELM-03: ngược chiều vòng sinh thì không được thưởng")]
    public void ReverseGenerationNoBonus()
    {
        var s = Playing(); Put(s.Me, 0, C("Pyraxis"));
        s.Me.Hand.Insert(0, C("Umbryx", atk: 1500, cost: 1));
        BattleEngine.Apply(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 1));
        Assert.Equal(1500, s.Me.Field[1]!.Atk);
    }

    [Fact(DisplayName = "SC-ELM-04: Nihilum không nhận hiệu ứng sinh dù sân có đủ 7 hệ")]
    public void NihilumGetsNoGeneration()
    {
        var s = Playing(); Put(s.Me, 0, C("Umbryx")); Put(s.Me, 1, C("Pyraxis")); Put(s.Me, 2, C("Luminara"));
        s.Me.Hand.Insert(0, C("Nihilum", atk: 2000, cost: 1));
        s.Me.Field[0] = null;
        BattleEngine.Apply(s, new BattleAction(s.Active, "play", Hand: 0, Slot: 0));
        Assert.Equal(2000, s.Me.Field[0]!.Atk);
    }

    [Theory(DisplayName = "SC-ELM-05: chuỗi nhân quả — 3 hệ liên tiếp (kể cả vòng khép kín) cả 3 +300 ATK; không liên tiếp thì không")]
    [InlineData("Umbryx,Pyraxis,Aqualis", true)]
    [InlineData("Luminara,Umbryx,Pyraxis", true)]
    [InlineData("Umbryx,Aqualis,Terrakin", false)]
    public void Chain(string elements, bool active)
    {
        var s = Playing(); var es = elements.Split(',');
        for (var i = 0; i < 3; i++) Put(s.Me, i, C(es[i], atk: 1000));
        Put(s.Opp, 0, C("Terrakin", hp: 9000));
        BattleEngine.Apply(s, new BattleAction(s.Active, "attack", Slot: 0, Target: 0));
        Assert.Equal(active, s.Me.ChainUsed);
        Assert.All(s.Me.Field, f => Assert.Equal(active ? 1300 : 1000, f!.EffAtk));
        End(s);
        Assert.All(s.Players[1 - s.Active].Field, f => Assert.Equal(0, f!.TempAtk));          // hết lượt thì hết thưởng
    }

    [Fact(DisplayName = "BR-ELM-06: chuỗi kích hoạt tối đa 1 lần mỗi lượt")]
    public void ChainOncePerTurn()
    {
        var s = Playing();
        foreach (var (e, i) in new[] { "Umbryx", "Pyraxis", "Aqualis" }.Select((e, i) => (e, i))) Put(s.Me, i, C(e, atk: 1000));
        Put(s.Opp, 0, C("Terrakin", hp: 90000, def: 0));
        BattleEngine.Apply(s, new BattleAction(s.Active, "attack", Slot: 0, Target: 0));
        BattleEngine.Apply(s, new BattleAction(s.Active, "attack", Slot: 1, Target: 0));
        Assert.All(s.Me.Field, f => Assert.Equal(1300, f!.EffAtk));                           // không cộng lần hai
    }

    // ---------- Điều kiện thắng ----------
    [Fact(DisplayName = "Thắng khi Keeper đối thủ về 0")]
    public void KeeperDown()
    {
        var s = Playing(); s.Opp.Keeper = 1500;
        Put(s.Me, 0, C("Umbryx", atk: 1500));
        BattleEngine.Apply(s, new BattleAction(s.Active, "attack", Slot: 0, Target: -1));
        Assert.Equal(new BattleResult(s.Active, "KEEPER_DOWN"), s.Result);
        Assert.Equal("MATCH_FINISHED", Fails(s, new BattleAction(s.Active, "end")).Code);
    }

    [Fact(DisplayName = "SC-BTL-09: phải bốc khi hết bài thì thua")]
    public void DeckOut()
    {
        var s = Playing(); var first = s.Active;
        s.Players[1 - first].Deck.Clear();
        End(s);                                                                // lượt của người hết bài
        Assert.Equal(new BattleResult(first, "DECK_OUT"), s.Result);
    }

    [Theory(DisplayName = "SC-BTL-07: đột tử — từ lượt thứ 10 của mỗi người mất 500, 1000, 1500… máu Keeper")]
    [InlineData(9, 5000)]
    [InlineData(10, 4500)]
    [InlineData(11, 4000)]
    public void SuddenDeath(int ownTurn, int keeper)
    {
        var s = Playing(); var first = s.Active;
        while (s.Players[first].OwnTurns < ownTurn - 1) End(s);
        s.Players[first].Keeper = 5000;                                         // "Keeper còn 5,000 máu" ngay trước lượt cần kiểm
        End(s); End(s);
        Assert.Equal(ownTurn, s.Players[first].OwnTurns);
        Assert.Equal(keeper, s.Players[first].Keeper);
    }

    [Fact(DisplayName = "Đột tử đưa Keeper về 0 thì thua")]
    public void SuddenDeathKills()
    {
        var s = Playing(); var first = s.Active;
        s.Players[first].Keeper = 400;
        while (s.Phase == BattlePhase.Main) End(s);
        Assert.Equal(new BattleResult(1 - first, "SUDDEN_DEATH"), s.Result);
    }

    [Fact(DisplayName = "SC-BTL-08: hết giờ — lần 1 và 2 chuyển lượt, lần 3 liên tiếp thì thua; có thao tác thì đếm lại")]
    public void Timeouts()
    {
        var s = Playing(); var p = s.Active;
        BattleEngine.Apply(s, new BattleAction(p, "timeout")); Assert.NotEqual(p, s.Active);
        BattleEngine.Apply(s, new BattleAction(s.Active, "end"));
        BattleEngine.Apply(s, new BattleAction(p, "timeout")); BattleEngine.Apply(s, new BattleAction(1 - p, "end"));
        Assert.Equal(2, s.Players[p].ConsecutiveTimeouts);
        BattleEngine.Apply(s, new BattleAction(p, "timeout"));
        Assert.Equal(new BattleResult(1 - p, "TIMEOUT"), s.Result);

        var t = Playing(); var q = t.Active;
        BattleEngine.Apply(t, new BattleAction(q, "timeout")); End(t); BattleEngine.Apply(t, new BattleAction(q, "timeout")); End(t);
        End(t);                                                                 // q chủ động kết thúc lượt → đếm lại
        Assert.Equal(0, t.Players[q].ConsecutiveTimeouts);
    }

    [Fact(DisplayName = "BR-BTL-07: đầu hàng thì đối thủ thắng, kể cả khi không phải lượt mình hoặc còn đang đổi tay")]
    public void Concede()
    {
        var s = Fresh();
        BattleEngine.Apply(s, new BattleAction(0, "concede"));
        Assert.Equal(new BattleResult(1, "CONCEDE"), s.Result);
        var t = Playing(); BattleEngine.Apply(t, new BattleAction(1 - t.Active, "concede"));
        Assert.Equal(new BattleResult(t.Active, "CONCEDE"), t.Result);
    }

    // ---------- Đổi tay ----------
    [Fact(DisplayName = "SC-BTL-11: đổi tay một lần (trả cả tay, xào, bốc lại); lần hai bị MULLIGAN_USED")]
    public void Mulligan()
    {
        var s = Fresh(); var before = s.Players[0].Hand.Select(c => c.Name).ToList();
        BattleEngine.Apply(s, new BattleAction(0, "mulligan"));
        Assert.Equal(5, s.Players[0].Hand.Count); Assert.Equal(25, s.Players[0].Deck.Count);
        Assert.Equal("MULLIGAN_USED", Fails(s, new BattleAction(0, "mulligan")).Code);
        BattleEngine.Apply(s, new BattleAction(1, "keep"));
        Assert.Equal(BattlePhase.Main, s.Phase);
        Assert.Equal("MULLIGAN_USED", Fails(s, new BattleAction(0, "mulligan")).Code);
        Assert.NotEmpty(before);
    }

    // ---------- Tất định và phát lại ----------
    private static List<BattleAction> BotMatch(byte[] seed, out BattleState final, List<BattleCard>? d0 = null, List<BattleCard>? d1 = null)
    {
        var setup = new BattleSetup(seed, d0 ?? Deck(), d1 ?? Deck());
        var s = BattleEngine.Start(setup); var log = new List<BattleAction>();
        for (var guard = 0; s.Phase != BattlePhase.Finished && guard < 5000; guard++)
        {
            var who = s.Phase == BattlePhase.Mulligan ? Array.FindIndex(s.Players, p => !p.Decided) : s.Active;
            var a = Bot.Next(s, who); BattleEngine.Apply(s, a); log.Add(a);
        }
        final = s;
        return log;
    }

    [Fact(DisplayName = "SC-BTL-12 / BR-BTL-10: phát lại từ seed + danh sách hành động cho đúng kết quả và máu Keeper")]
    public void ReplayIsIdentical()
    {
        foreach (var seed in new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var log = BotMatch([seed], out var original);
            Assert.NotNull(original.Result);
            var replay = BattleEngine.Start(new BattleSetup([seed], Deck(), Deck()));
            foreach (var a in log) BattleEngine.Apply(replay, a);
            Assert.Equal(original.Result, replay.Result);
            Assert.Equal(original.Players[0].Keeper, replay.Players[0].Keeper);
            Assert.Equal(original.Players[1].Keeper, replay.Players[1].Keeper);
            Assert.Equal(original.Events.Count, replay.Events.Count);
        }
    }

    [Fact(DisplayName = "Máy đấu máy luôn kết thúc (không kẹt vô hạn) và cả hai bên đều có thể thắng khi đổi seed")]
    public void BotMatchesTerminate()
    {
        var winners = new HashSet<int>();
        for (byte seed = 1; seed <= 30; seed++) { BotMatch([seed], out var s); Assert.NotNull(s.Result); winners.Add(s.Result!.Winner); }
        Assert.Equal(2, winners.Count);
    }

    [Fact(DisplayName = "Cùng seed cho cùng thứ tự xào và người đi trước; khác seed cho kết quả khác; cả hai người đi trước đều xuất hiện")]
    public void RngDeterminism()
    {
        var a = Fresh(5); var b = Fresh(5);
        Assert.Equal(a.First, b.First);
        Assert.Equal(a.Players[0].Hand.Select(c => c.Name), b.Players[0].Hand.Select(c => c.Name));
        var firsts = Enumerable.Range(1, 40).Select(i => Fresh((byte)i).First).ToHashSet();
        Assert.Equal(2, firsts.Count);
        var r1 = new DeterministicRng([9]); var r2 = new DeterministicRng([9]);
        Assert.Equal(Enumerable.Range(0, 50).Select(_ => r1.Next(1000)), Enumerable.Range(0, 50).Select(_ => r2.Next(1000)));
    }
}
