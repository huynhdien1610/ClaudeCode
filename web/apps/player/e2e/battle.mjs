// E2E trận luyện tập: tạo người chơi có bộ 30 lá hợp lệ qua API, rồi chơi trọn một trận với máy bằng giao diện thật.
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");
import { mkdirSync } from "node:fs";

const BASE = process.env.BASE_URL ?? "http://localhost:3000";
const API = process.env.API ?? "http://127.0.0.1:5080";
const SHOTS = process.env.SHOTS ?? "/tmp/anima-e2e";
mkdirSync(SHOTS, { recursive: true });
const ok = (c, m) => { if (!c) throw new Error(m); };

// ---- Chuẩn bị qua API ----
let token = "";
const call = async (method, path, body, idem) => {
  const res = await fetch(API + path, { method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(idem ? { "Idempotency-Key": crypto.randomUUID() } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await res.text(); const json = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error(`${method} ${path} → ${res.status} ${text}`);
  return json;
};
const email = `battle-${Date.now()}@example.com`;
const reg = await call("POST", "/v1/accounts", { email, password: "correct horse", birthDate: "1990-01-01", country: "VN", locale: "vi", timezone: "Asia/Ho_Chi_Minh" });
token = reg.accessToken;
for (let round = 0; round < 2; round++) {
  await call("POST", "/v1/dev/topup", { gem: 700 }, true);
  const bought = await call("POST", "/v1/packs/awakening-standard/purchase", { currency: "GEM", quantity: 7 }, true);
  for (const id of bought.packInstanceIds) await call("POST", `/v1/pack-instances/${id}/open`);
}
const items = (await call("GET", "/v1/collection")).items;
const picked = []; const perDef = {}; const perRar = {}; let support = 0;
for (const i of items) {
  const c = i.card; const cap = c.rarity === "legendary" || c.rarity === "secret" ? 1 : 2; const rcap = { epic: 4, legendary: 2, secret: 1 }[c.rarity] ?? 99;
  const sup = c.type !== "anima";
  if ((sup && support >= 6) || (perDef[c.id] ?? 0) >= cap || (perRar[c.rarity] ?? 0) >= rcap) continue;
  perDef[c.id] = (perDef[c.id] ?? 0) + 1; perRar[c.rarity] = (perRar[c.rarity] ?? 0) + 1; if (sup) support++;
  picked.push(i.id); if (picked.length === 30) break;
}
ok(picked.length === 30, `chỉ chọn được ${picked.length} thẻ hợp lệ`);
const deck = await call("POST", "/v1/decks", { name: "Bộ chiến E2E", cardInstanceIds: picked });
ok(deck.valid, "bộ bài phải hợp lệ");
await call("POST", `/v1/decks/${deck.id}/default`, {});

// ---- Giao diện ----
const browser = await chromium.launch();
const page = await (await browser.newContext({ viewport: { width: 1280, height: 1000 }, locale: "vi-VN" })).newPage();
const problems = [];
page.on("pageerror", (e) => problems.push(`pageerror: ${e.message}`));
page.on("console", (m) => { if (m.type() === "error" && !/favicon|Failed to load resource/.test(m.text())) problems.push(`console: ${m.text()}`); });
page.on("response", (r) => { if (r.url().includes("/api/") && r.status() >= 400) problems.push(`HTTP ${r.status()}: ${r.url()}`); });

let code = 0;
try {
  await page.goto(BASE + "/login"); await page.waitForLoadState("networkidle");
  await page.getByTestId("email").fill(email); await page.getByTestId("password").fill("correct horse"); await page.getByTestId("submit").click();
  await page.waitForURL("**/home");
  await page.goto(BASE + "/battle"); await page.getByTestId("battle-start").waitFor();
  await page.getByTestId("battle-start").click();
  await page.getByTestId("battle-keep").waitFor();
  await page.getByTestId("battle-mulligan").click();                       // đổi tay một lần
  await page.getByTestId("battle-keep").waitFor({ state: "detached" });        // đã vào lượt chơi
  ok((await page.getByTestId("battle-mulligan").count()) === 0, "không được đổi tay lần hai");

  const enabled = (id) => page.getByTestId(id).and(page.locator(":not([disabled])"));
  let turns = 0;
  for (let step = 0; step < 900; step++) {
    if (await page.getByTestId("battle-result").count()) break;
    if (!(await enabled("battle-end").count())) { await page.waitForTimeout(60); continue; }          // chưa tới lượt hoặc đang gửi
    const mySlotEmpty = async () => { const n = await page.locator('[data-testid^="my-slot-"]').count(); for (let i = 0; i < n; i++) { const b = page.getByTestId(`my-slot-${i}`); if (!(await b.isDisabled()) && (await b.innerText()).trim() === "—") return b; } return null; };
    if (await enabled("hand-card").count()) {
      await enabled("hand-card").first().click();
      const slot = await mySlotEmpty();
      if (slot) { await slot.click(); await page.waitForTimeout(40); continue; }
      await enabled("hand-card").first().click().catch(() => {});      // bỏ chọn nếu không còn ô trống
    }
    const attackers = page.locator('[data-testid^="my-slot-"]:not([disabled])');
    let acted = false;
    for (let i = 0; i < await attackers.count(); i++) {
      const b = attackers.nth(i); if ((await b.innerText()).trim() === "—") continue;
      await b.click();
      if (await enabled("attack-keeper").count()) await page.getByTestId("attack-keeper").click();
      else { const t = page.locator('[data-testid^="opp-slot-"]:not([disabled])').first(); if (await t.count()) await t.click(); else continue; }
      acted = true; await page.waitForTimeout(40); break;
    }
    if (acted) continue;
    await page.getByTestId("battle-end").click(); turns++; await page.waitForTimeout(60);
  }
  await page.getByTestId("battle-result").waitFor({ timeout: 5000 });
  const won = await page.getByTestId("battle-result").getAttribute("data-won");
  ok(won === "true" || won === "false", "kết quả hiển thị");
  ok((await page.getByTestId("battle-log").locator("li").count()) > 0, "nhật ký trận trống");
  await page.screenshot({ path: `${SHOTS}/battle.png` });
  await page.getByTestId("battle-back").click();
  await page.getByTestId("battle-history").waitFor();
  ok((await page.getByTestId("battle-history").locator("tr").count()) === 1, "lịch sử phải có 1 trận");
  console.log(`PASS - Trận luyện tập: chơi trọn ${turns} lượt kết thúc bằng giao diện, kết quả ${won === "true" ? "thắng" : "thua"}, lịch sử có 1 trận`);
} catch (e) {
  await page.screenshot({ path: `${SHOTS}/battle-failure.png` }).catch(() => undefined);
  console.error("FAIL -", e.message); code = 1;
}
await browser.close();
if (problems.length) { console.log("Vấn đề trình duyệt/mạng:"); [...new Set(problems)].forEach((p) => console.log(" -", p)); code = 1; }
process.exit(code);
