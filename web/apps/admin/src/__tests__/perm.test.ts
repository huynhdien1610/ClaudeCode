import assert from "node:assert/strict";
import test from "node:test";
import { can, canView, isOwn, parsePct, pct, sumPpm } from "../perm.ts";

test("ppm ↔ phần trăm", () => {
  assert.equal(pct(450_000), "45%");
  assert.equal(pct(30_000), "3%");
  assert.equal(pct(12_345), "1.2345%");
  assert.equal(parsePct("45"), 450_000);
  assert.equal(parsePct("0,5"), 5_000);
  assert.ok(Number.isNaN(parsePct("abc")));
  assert.equal(sumPpm([{ ppm: 600_000 }, { ppm: 400_000 }]), 1_000_000);
});

test("vai trò chỉ thấy khu vực của mình", () => {
  assert.ok(canView("cs_agent", "accounts"));
  assert.ok(!canView("cs_agent", "audit"));
  assert.ok(!canView("content_manager", "dashboard"));
  assert.ok(canView("super_admin", "admins") && !canView("fraud_analyst", "admins"));
});

test("hành động theo vai trò (BRD 12.2)", () => {
  assert.ok(can("economy_manager", "odds.draft") && !can("super_admin", "odds.draft"));
  assert.ok(can("super_admin", "odds.approve"));
  assert.ok(can("cs_agent", "comp.create") && !can("fraud_analyst", "comp.create"));
  assert.ok(can("fraud_analyst", "comp.decide") && !can("cs_agent", "comp.decide"));
  assert.ok(can("super_admin", "account.unban") && !can("fraud_analyst", "account.unban"));
  assert.ok(!can("finance_viewer", "economy.propose"));
});

test("maker-checker: không tự duyệt", () => {
  assert.ok(isOwn("A@x.io", "a@x.io"));
  assert.ok(!isOwn("a@x.io", "b@x.io"));
  assert.ok(!isOwn("a@x.io", null));
});
