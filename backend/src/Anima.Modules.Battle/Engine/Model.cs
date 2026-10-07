namespace Anima.Battle.Engine;

/// <summary>Chỉ số thẻ chốt tại lúc bắt đầu trận (thẻ phát hành là bất biến, BR-CARD-06).</summary>
public sealed record BattleCard(int DefId, string Name, string Element, string Type, string Rarity, int Cost, int Atk, int Def, int Hp);

public sealed class FieldAnima
{
    public required BattleCard Card { get; init; }
    public int Atk { get; set; }
    public int Def { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    /// <summary>Cộng ATK tạm đến hết lượt (chuỗi nhân quả).</summary>
    public int TempAtk { get; set; }
    public bool Summoned { get; set; }      // vừa ra sân trong lượt này
    public bool Attacked { get; set; }
    public int EffAtk => Atk + TempAtk;
    public int EffDef => Def;
}

public sealed class BattlePlayer
{
    public const int Slots = 3;
    public int Keeper { get; set; } = BattleEngine.KeeperHp;
    public List<BattleCard> Deck { get; set; } = [];
    public List<BattleCard> Hand { get; } = [];
    public FieldAnima?[] Field { get; } = new FieldAnima?[Slots];
    public List<BattleCard> Graveyard { get; } = [];
    public bool MulliganUsed { get; set; }
    public bool Decided { get; set; }
    public int OwnTurns { get; set; }
    public int Energy { get; set; }
    public int ConsecutiveTimeouts { get; set; }
    public bool ChainUsed { get; set; }
    public bool FieldEmpty => Field.All(f => f is null);
}

public enum BattlePhase { Mulligan, Main, Finished }

public sealed record BattleEvent(int Seq, string Kind, int Player, IReadOnlyDictionary<string, object?> Data);
public sealed record BattleResult(int Winner, string Reason);

public sealed record BattleAction(int Player, string Type, int? Hand = null, int? Slot = null, int? Target = null);

public sealed class BattleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class BattleState
{
    public required BattlePlayer[] Players { get; init; }
    public required DeterministicRng Rng { get; init; }
    public BattlePhase Phase { get; set; } = BattlePhase.Mulligan;
    public int Active { get; set; }
    public int First { get; set; }
    public int TurnNumber { get; set; }              // tổng số lượt đã bắt đầu (cả hai bên)
    public BattleResult? Result { get; set; }
    public List<BattleEvent> Events { get; } = [];
    public int ActionCount { get; set; }
    public BattlePlayer Me => Players[Active];
    public BattlePlayer Opp => Players[1 - Active];
}
