namespace Anima.Battle.Engine;

public enum SkillKind { Active, Passive, Trigger, Ultimate, Fusion }
public enum SkillTarget { None, Enemy, Ally, Self, AllEnemies, AllAllies }

public sealed record SkillDef(string Id, string Element, SkillKind Kind, SkillTarget Target, int Cooldown, string Name, bool OncePerMatch = false);

/// <summary>Danh mục kỹ năng (docs/SKILLS_ANIMA.md). Mỗi hệ có 4 kỹ năng cấp thẻ; Resonance/Ultimate/Fusion là khả năng cấp đội.</summary>
public static class SkillCatalog
{
    private static SkillDef A(string id, string el, SkillTarget t, int cd, string name) => new(id, el, SkillKind.Active, t, cd, name);
    private static SkillDef P(string id, string el, string name) => new(id, el, SkillKind.Passive, SkillTarget.None, 0, name);
    private static SkillDef T(string id, string el, string name, bool once = false) => new(id, el, SkillKind.Trigger, SkillTarget.None, 0, name, once);
    private static SkillDef U(string id, string el, SkillTarget t, string name) => new(id, el, SkillKind.Ultimate, t, 0, name);
    private static SkillDef F(string id, SkillTarget t, string name) => new(id, "", SkillKind.Fusion, t, 0, name, true);

    public static readonly IReadOnlyList<SkillDef> All =
    [
        A("luminara.dawns_embrace", "Luminara", SkillTarget.Ally, 2, "Dawn's Embrace"), P("luminara.radiant_aura", "Luminara", "Radiant Aura"),
        T("luminara.hopes_echo", "Luminara", "Hope's Echo", true), A("luminara.blinding_light", "Luminara", SkillTarget.Enemy, 3, "Blinding Light"),
        A("umbryx.silent_tear", "Umbryx", SkillTarget.Enemy, 1, "Silent Tear"), P("umbryx.veil_of_sorrow", "Umbryx", "Veil of Sorrow"),
        T("umbryx.lonely_howl", "Umbryx", "Lonely Howl", true), A("umbryx.nightmare_touch", "Umbryx", SkillTarget.Enemy, 3, "Nightmare Touch"),
        A("pyraxis.ember_strike", "Pyraxis", SkillTarget.Enemy, 2, "Ember Strike"), P("pyraxis.rage_bound", "Pyraxis", "Rage Bound"),
        T("pyraxis.fury_unleashed", "Pyraxis", "Fury Unleashed"), A("pyraxis.flame_vortex", "Pyraxis", SkillTarget.AllEnemies, 3, "Flame Vortex"),
        A("aqualis.tidal_memory", "Aqualis", SkillTarget.Enemy, 1, "Tidal Memory"), P("aqualis.deep_calm", "Aqualis", "Deep Calm"),
        T("aqualis.echo_of_loss", "Aqualis", "Echo of Loss"), A("aqualis.undertow", "Aqualis", SkillTarget.Enemy, 3, "Undertow"),
        A("terrakin.stone_wall", "Terrakin", SkillTarget.Self, 3, "Stone Wall"), P("terrakin.unyielding", "Terrakin", "Unyielding"),
        T("terrakin.avalanche", "Terrakin", "Avalanche"), A("terrakin.earths_grasp", "Terrakin", SkillTarget.Enemy, 2, "Earth's Grasp"),
        A("ventara.wind_step", "Ventara", SkillTarget.Ally, 1, "Wind Step"), P("ventara.free_spirit", "Ventara", "Free Spirit"),
        T("ventara.curiosity", "Ventara", "Curiosity"), A("ventara.gust_barrier", "Ventara", SkillTarget.Self, 2, "Gust Barrier"),
        A("voltaris.surge_bolt", "Voltaris", SkillTarget.Enemy, 2, "Surge Bolt"), P("voltaris.overcharge", "Voltaris", "Overcharge"),
        T("voltaris.chain_reaction", "Voltaris", "Chain Reaction"), A("voltaris.static_field", "Voltaris", SkillTarget.AllEnemies, 3, "Static Field"),
        A("nihilum.void_touch", "Nihilum", SkillTarget.Enemy, 2, "Void Touch"), P("nihilum.emptiness", "Nihilum", "Emptiness"),
        T("nihilum.despair", "Nihilum", "Despair"), A("nihilum.oblivion", "Nihilum", SkillTarget.Enemy, 3, "Oblivion"),
        U("luminara.ultimate", "Luminara", SkillTarget.Ally, "Seraphel's Blessing"), U("umbryx.ultimate", "Umbryx", SkillTarget.AllEnemies, "Nocturne's Requiem"),
        U("pyraxis.ultimate", "Pyraxis", SkillTarget.Enemy, "Emberfang's Wrath"), U("aqualis.ultimate", "Aqualis", SkillTarget.AllAllies, "Tidemourn's Lament"),
        U("terrakin.ultimate", "Terrakin", SkillTarget.Self, "Stoneward's Vow"), U("ventara.ultimate", "Ventara", SkillTarget.AllEnemies, "Zephyrion's Gambit"),
        U("voltaris.ultimate", "Voltaris", SkillTarget.AllEnemies, "Surgeflux's Revelation"), U("nihilum.ultimate", "Nihilum", SkillTarget.Enemy, "The Nameless"),
        F("dawnbreak", SkillTarget.None, "Dawnbreak"), F("silent_storm", SkillTarget.None, "Silent Storm"), F("eternal_tide", SkillTarget.None, "Eternal Tide"),
        F("chaos_theory_fusion", SkillTarget.None, "Chaos Theory"), F("voids_edge", SkillTarget.Enemy, "Void's Edge"), F("hopes_last_stand", SkillTarget.Ally, "Hope's Last Stand"),
    ];

    private static readonly Dictionary<string, SkillDef> ById = All.ToDictionary(s => s.Id);
    public static SkillDef? Find(string? id) => id is not null && ById.TryGetValue(id, out var s) ? s : null;

    /// <summary>4 kỹ năng cấp thẻ của một hệ theo thứ tự gán: Active A, Passive, Trigger, Active B.</summary>
    public static IReadOnlyList<string> CardSkills(string element) => All.Where(s => s.Element == element && s.Kind is SkillKind.Active or SkillKind.Passive or SkillKind.Trigger && !s.Id.EndsWith(".ultimate")).Select(s => s.Id).ToList();

    public static SkillDef UltimateOf(string element) => ById[$"{element.ToLowerInvariant()}.ultimate"];

    /// <summary>Hai hệ cần có trên sân cho từng Fusion.</summary>
    public static readonly IReadOnlyDictionary<string, (string A, string B)> FusionElements = new Dictionary<string, (string, string)>
    {
        ["dawnbreak"] = ("Luminara", "Pyraxis"),
        ["silent_storm"] = ("Umbryx", "Voltaris"),
        ["eternal_tide"] = ("Aqualis", "Terrakin"),
        ["chaos_theory_fusion"] = ("Ventara", "Voltaris"),
        ["voids_edge"] = ("Nihilum", "Umbryx"),
        ["hopes_last_stand"] = ("Luminara", "Nihilum"),
    };
}
