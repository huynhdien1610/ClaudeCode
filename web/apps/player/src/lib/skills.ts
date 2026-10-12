// Tên hiển thị của kỹ năng cấp thẻ (id khớp backend: docs/SKILLS_ANIMA.md). Tên riêng giữ nguyên tiếng Anh ở mọi ngôn ngữ.
export const SKILL_NAME: Record<string, string> = {
  "luminara.dawns_embrace": "Dawn's Embrace", "luminara.radiant_aura": "Radiant Aura", "luminara.hopes_echo": "Hope's Echo", "luminara.blinding_light": "Blinding Light",
  "umbryx.silent_tear": "Silent Tear", "umbryx.veil_of_sorrow": "Veil of Sorrow", "umbryx.lonely_howl": "Lonely Howl", "umbryx.nightmare_touch": "Nightmare Touch",
  "pyraxis.ember_strike": "Ember Strike", "pyraxis.rage_bound": "Rage Bound", "pyraxis.fury_unleashed": "Fury Unleashed", "pyraxis.flame_vortex": "Flame Vortex",
  "aqualis.tidal_memory": "Tidal Memory", "aqualis.deep_calm": "Deep Calm", "aqualis.echo_of_loss": "Echo of Loss", "aqualis.undertow": "Undertow",
  "terrakin.stone_wall": "Stone Wall", "terrakin.unyielding": "Unyielding", "terrakin.avalanche": "Avalanche", "terrakin.earths_grasp": "Earth's Grasp",
  "ventara.wind_step": "Wind Step", "ventara.free_spirit": "Free Spirit", "ventara.curiosity": "Curiosity", "ventara.gust_barrier": "Gust Barrier",
  "voltaris.surge_bolt": "Surge Bolt", "voltaris.overcharge": "Overcharge", "voltaris.chain_reaction": "Chain Reaction", "voltaris.static_field": "Static Field",
  "nihilum.void_touch": "Void Touch", "nihilum.emptiness": "Emptiness", "nihilum.despair": "Despair", "nihilum.oblivion": "Oblivion",
};
export const skillName = (id: string | null | undefined) => (id ? SKILL_NAME[id] ?? null : null);
/** Từ rarity "rare" trở lên thì thẻ có viền cầu vồng (cấp độ hiệu ứng tăng dần). */
export const rainbowTier = (rarity: string): 0 | 1 | 2 | 3 | 4 => ({ rare: 1, epic: 2, legendary: 3, secret: 4 } as Record<string, 1 | 2 | 3 | 4>)[rarity] ?? 0;
