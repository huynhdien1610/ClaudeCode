// Luật bộ bài phía client (BR-DECK-01 → 03), để báo lỗi ngay khi đang soạn. Server vẫn là nơi quyết định.
export const DECK_SIZE = 30, MIN_ANIMA = 24, MAX_SUPPORT = 6, MAX_COPIES = 2, MAX_DECKS = 10;
const RARITY_CAP: Record<string, number> = { epic: 4, legendary: 2, secret: 1 };

export type DeckEntry = { defId: number; type: string; rarity: string };
export type DeckStats = { total: number; anima: number; support: number; epic: number; legendary: number; secret: number };

export function deckStats(cards: DeckEntry[]): DeckStats {
  const anima = cards.filter((c) => c.type === "anima").length;
  const n = (r: string) => cards.filter((c) => c.rarity === r).length;
  return { total: cards.length, anima, support: cards.length - anima, epic: n("epic"), legendary: n("legendary"), secret: n("secret") };
}

/** Trả về mã luật bị vi phạm (không lặp), cùng mã với server. */
export function validateDeck(cards: DeckEntry[]): string[] {
  const s = deckStats(cards); const v = new Set<string>();
  if (s.total !== DECK_SIZE) v.add("DECK_SIZE");
  if (s.anima < MIN_ANIMA) v.add("DECK_ANIMA_MIN");
  if (s.support > MAX_SUPPORT) v.add("DECK_SUPPORT_MAX");
  const byDef = new Map<number, DeckEntry[]>();
  for (const c of cards) byDef.set(c.defId, [...(byDef.get(c.defId) ?? []), c]);
  for (const g of byDef.values()) if (g.length > copyCap(g[0]!.rarity)) v.add("DECK_COPY_LIMIT");
  for (const [r, cap] of Object.entries(RARITY_CAP)) if (cards.filter((c) => c.rarity === r).length > cap) v.add("DECK_RARITY_LIMIT");
  return [...v];
}

export const copyCap = (rarity: string) => (rarity === "legendary" || rarity === "secret" ? 1 : MAX_COPIES);

/** Thêm `c` vào bộ có vi phạm luật "cứng" (bản sao, độ hiếm, hỗ trợ, quá 30) không? Dùng để khóa nút thêm. */
export function canAdd(cards: DeckEntry[], c: DeckEntry): boolean {
  if (cards.length >= DECK_SIZE) return false;
  const next = [...cards, c];
  const bad = validateDeck(next).filter((r) => r !== "DECK_SIZE" && r !== "DECK_ANIMA_MIN");
  return bad.length === 0;
}
