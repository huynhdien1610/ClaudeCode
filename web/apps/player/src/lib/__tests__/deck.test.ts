import test from "node:test";
import assert from "node:assert/strict";
import { canAdd, validateDeck, type DeckEntry } from "../deck.ts";

const a = (defId: number, rarity = "common", type = "anima"): DeckEntry => ({ defId, rarity, type });
const valid = () => Array.from({ length: 15 }, (_, i) => [a(i + 1), a(i + 1)]).flat();

test("bộ 30 lá hợp lệ", () => assert.deepEqual(validateDeck(valid()), []));
test("sai số lượng", () => { assert.deepEqual(validateDeck(valid().slice(1)), ["DECK_SIZE"]); });
test("tối đa 6 lá hỗ trợ; dưới 24 Anima", () => {
  const withSupport = (n: number) => [...Array.from({ length: 30 - n }, (_, i) => a(i + 1)), ...Array.from({ length: n }, (_, i) => a(100 + i, "rare", "echo"))];
  assert.deepEqual(validateDeck(withSupport(6)), []);
  assert.deepEqual(validateDeck(withSupport(7)).sort(), ["DECK_ANIMA_MIN", "DECK_SUPPORT_MAX"]);
});
test("bản sao: tối đa 2; Legendary/Secret 1", () => {
  const base = Array.from({ length: 28 }, (_, i) => a(i + 1));
  assert.deepEqual(validateDeck([...base.slice(0, 27), a(99), a(99), a(99)]), ["DECK_COPY_LIMIT"]);
  assert.deepEqual(validateDeck([...base, a(900, "legendary"), a(900, "legendary")]), ["DECK_COPY_LIMIT"]);
});
test("giới hạn độ hiếm: 4 Epic, 2 Legendary, 1 Secret", () => {
  const deck = (r: string, n: number) => [...Array.from({ length: 30 - n }, (_, i) => a(i + 1)), ...Array.from({ length: n }, (_, i) => a(500 + i, r))];
  assert.deepEqual(validateDeck(deck("epic", 4)), []); assert.deepEqual(validateDeck(deck("epic", 5)), ["DECK_RARITY_LIMIT"]);
  assert.deepEqual(validateDeck(deck("secret", 2)), ["DECK_RARITY_LIMIT"]);
});
test("canAdd khóa thêm lá vượt luật cứng nhưng cho phép khi chưa đủ 30", () => {
  assert.ok(canAdd([a(1)], a(1)));
  assert.ok(!canAdd([a(1), a(1)], a(1)));
  assert.ok(!canAdd([a(9, "legendary")], a(9, "legendary")));
  assert.ok(!canAdd(valid(), a(77)));
});
