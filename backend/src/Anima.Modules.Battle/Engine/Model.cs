namespace Anima.Battle.Engine;

/// <summary>Chỉ số thẻ chốt tại lúc bắt đầu trận (thẻ phát hành là bất biến, BR-CARD-06).</summary>
public sealed record BattleCard(int DefId, string Name, string Element, string Type, string Rarity, int Cost, int Atk, int Def, int Hp, string? Skill = null);

/// <summary>Hiệu ứng tạm thời trên một Anima. <c>Turns</c> giảm 1 ở cuối lượt của chủ sở hữu Anima.</summary>
public sealed class Status
{
    public required string Kind { get; init; }
    public int Turns { get; set; }
    /// <summary>Phần trăm (atk_pct, def_pct, spd_pct, dodge_pct, hp_pct) hoặc sát thương mỗi lớp (burn).</summary>
    public double Value { get; set; }
    public int Stacks { get; set; } = 1;
}

public sealed class FieldAnima
{
    public required BattleCard Card { get; init; }
    public int Uid { get; init; }
    public string? SkillId => Card.Skill;
    public int Atk { get; set; }
    public int Def { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    /// <summary>HP tối đa gốc (đã gồm +200 từ Sinh); MaxHp = gốc × (1 + thưởng %).</summary>
    public int BaseMaxHp { get; set; }
    /// <summary>Cộng ATK tạm đến hết lượt (chuỗi nhân quả).</summary>
    public int TempAtk { get; set; }
    public bool Summoned { get; set; }      // vừa ra sân trong lượt này
    public bool Attacked { get; set; }      // đã dùng hành động trong lượt này (đánh hoặc kỹ năng)
    public List<Status> Statuses { get; } = [];
    public Dictionary<string, int> Cd { get; } = [];
    public HashSet<string> Used { get; } = [];
    // Tính lại sau mỗi thay đổi (BattleEngine.Recalc):
    public int AtkPct { get; set; }
    public int DefPct { get; set; }
    public int SpdPct { get; set; }
    public int DodgePct { get; set; }
    public int? DefBase { get; set; }
    public int EffAtk => (int)((long)(Atk + TempAtk) * (100 + AtkPct) / 100);
    public int EffDef => (int)((long)(DefBase ?? Def) * (100 + DefPct) / 100);
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
    /// <summary>Cờ dùng một lần/trận của cả đội (Sacred Bond, The Void Calls, các Fusion...).</summary>
    public HashSet<string> TeamUsed { get; } = [];
    public int SurgeLeft { get; set; }
    public bool FusionThisTurn { get; set; }
    public bool FieldEmpty => Field.All(f => f is null);
}

public enum BattlePhase { Mulligan, Main, Finished }

public sealed record BattleEvent(int Seq, string Kind, int Player, IReadOnlyDictionary<string, object?> Data);
public sealed record BattleResult(int Winner, string Reason);

public sealed record BattleAction(int Player, string Type, int? Hand = null, int? Slot = null, int? Target = null, string? Id = null);

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
    /// <summary>Emotion Meter chung hai bên, 0–100.</summary>
    public int Meter { get; set; }
    public int NextUid { get; set; } = 1;
    public BattlePlayer Me => Players[Active];
    public BattlePlayer Opp => Players[1 - Active];
}
