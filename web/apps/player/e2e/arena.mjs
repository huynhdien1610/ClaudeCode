// E2E Đấu trường xếp hạng: hai người chơi thật (hai trình duyệt) tìm trận, được ghép, chọn tay đầu, một bên đầu hàng;
// kiểm kết quả, điểm kỹ năng (Glicko-2) và Arena Point ở cả hai phía.
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");

const BASE = process.env.BASE_URL ?? "http://localhost:3000";
const API = process.env.API ?? "http://127.0.0.1:5080";
const ok = (c, m) => { if (!c) throw new Error(m); };

async function makePlayer(tag) {
  let token = "";
  const call = async (method, path, body, idem) => {
    const res = await fetch(API + path, { method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(idem ? { "Idempotency-Key": crypto.randomUUID() } : {}) }, body: body ? JSON.stringify(body) : undefined });
    const text = await res.text(); const json = text ? JSON.parse(text) : null;
    if (!res.ok) throw new Error(`${method} ${path} → ${res.status} ${text}`);
    return json;
  };
  const email = `arena-${tag}-${Date.now()}@example.com`;
  token = (await call("POST", "/v1/accounts", { email, password: "correct horse", birthDate: "1990-01-01", country: "VN", locale: "vi", timezone: "Asia/Ho_Chi_Minh" })).accessToken;
  for (let round = 0; round < 2; round++) {
    await call("POST", "/v1/dev/topup", { gem: 700 }, true);
    const bought = await call("POST", "/v1/packs/awakening-standard/purchase", { currency: "GEM", quantity: 7 }, true);
    for (const id of bought.packInstanceIds) await call("POST", `/v1/pack-instances/${id}/open`);
  }
  const picked = []; const perDef = {}; const perRar = {};
  for (const i of (await call("GET", "/v1/collection")).items) {
    const c = i.card; const cap = c.rarity === "legendary" || c.rarity === "secret" ? 1 : 2; const rcap = { epic: 4, legendary: 2, secret: 1 }[c.rarity] ?? 99;
    if ((perDef[c.id] ?? 0) >= cap || (perRar[c.rarity] ?? 0) >= rcap) continue;
    perDef[c.id] = (perDef[c.id] ?? 0) + 1; perRar[c.rarity] = (perRar[c.rarity] ?? 0) + 1; picked.push(i.id); if (picked.length === 30) break;
  }
  ok(picked.length === 30, `chỉ chọn được ${picked.length} thẻ`);
  const deck = await call("POST", "/v1/decks", { name: "Bộ xếp hạng", cardInstanceIds: picked });
  await call("POST", `/v1/decks/${deck.id}/default`, {});
  return email;
}

const [emailA, emailB] = [await makePlayer("a"), await makePlayer("b")];
const browser = await chromium.launch();
const mk = async (email) => {
  const page = await (await browser.newContext({ viewport: { width: 1280, height: 1000 }, locale: "vi-VN" })).newPage();
  page.on("pageerror", (e) => problems.push(`pageerror: ${e.message}`));
  await page.goto(BASE + "/login"); await page.waitForLoadState("networkidle");
  await page.getByTestId("email").fill(email); await page.getByTestId("password").fill("correct horse"); await page.getByTestId("submit").click();
  await page.waitForURL("**/home"); await page.goto(BASE + "/arena"); await page.getByTestId("arena-join").waitFor();
  return page;
};
const problems = [];
let code = 0;
try {
  const [a, b] = [await mk(emailA), await mk(emailB)];
  await a.getByTestId("arena-join").click(); await a.getByTestId("arena-searching").waitFor();
  await b.getByTestId("arena-join").click();
  for (const p of [a, b]) { await p.getByTestId("battle-board").waitFor({ timeout: 15000 }); await p.getByTestId("arena-clock").waitFor(); }
  for (const p of [a, b]) { await p.getByTestId("battle-keep").click(); }
  // Bên nào tới lượt thì có nút "Kết thúc lượt"; bên đó đầu hàng.
  let loser = null;
  for (let i = 0; i < 40 && !loser; i++) { for (const p of [a, b]) if ((await p.getByTestId("battle-end").count()) > 0) loser = p; if (!loser) await a.waitForTimeout(500); }
  ok(loser, "không bên nào tới lượt");
  const winner = loser === a ? b : a;
  await loser.getByTestId("battle-concede").click();
  await loser.getByTestId("battle-result").waitFor(); await winner.getByTestId("battle-result").waitFor({ timeout: 10000 });
  ok((await loser.getByTestId("battle-result").getAttribute("data-won")) === "false", "bên đầu hàng phải thua");
  ok((await winner.getByTestId("battle-result").getAttribute("data-won")) === "true", "bên kia phải thắng");
  ok(/\+10 AP/.test(await winner.getByTestId("arena-rating").innerText()), "người thắng nhận 10 AP");
  ok(/-/.test(await loser.getByTestId("arena-rating").innerText()), "người thua mất điểm");

  await winner.getByTestId("battle-back").click(); await winner.getByTestId("arena-stats").waitFor();
  await loser.getByTestId("battle-back").click(); await loser.getByTestId("arena-stats").waitFor();
  const rw = parseInt((await winner.getByTestId("arena-rating-now").innerText()).match(/\d+/)[0], 10);
  const rl = parseInt((await loser.getByTestId("arena-rating-now").innerText()).match(/\d+/)[0], 10);
  ok(rw > 1500 && rl < 1500, `điểm sau trận: thắng ${rw}, thua ${rl}`);
  ok(/10/.test(await winner.getByTestId("arena-ap").innerText()), "người thắng có 10 Arena Point");
  await winner.getByTestId("arena-history").waitFor(); await winner.getByTestId("arena-board").waitFor();
  console.log("PASS - Đấu trường: hai người chơi được ghép, chốt tay đầu, một bên đầu hàng; thắng +10 AP, điểm Glicko-2 thắng tăng/thua giảm, có lịch sử và bảng xếp hạng");
} catch (e) {
  console.error("FAIL -", e.message); code = 1;
}
await browser.close();
if (problems.length) { console.log(problems.join("\n")); code = 1; }
process.exit(code);
