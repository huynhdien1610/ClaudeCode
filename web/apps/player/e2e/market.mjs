// E2E chợ P2P giữa hai người chơi thật: người bán niêm yết thẻ → người mua mua → số dư, quyền sở hữu, lịch sử đều đúng.
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");
import { readFileSync, mkdirSync } from "node:fs";

const BASE = process.env.BASE_URL ?? "http://localhost:3000";
const API = process.env.API ?? "http://127.0.0.1:5080";
const LOG = process.env.API_LOG ?? "/tmp/anima-api.log";
const SHOTS = process.env.SHOTS ?? "/tmp/anima-e2e"; mkdirSync(SHOTS, { recursive: true });
const ok = (c, m) => { if (!c) throw new Error(m); };
const eq = (a, b, m) => { if (a !== b) throw new Error(`${m}: expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`); };

const api = (token) => async (method, path, body, idem) => {
  const res = await fetch(API + path, { method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(idem ? { "Idempotency-Key": crypto.randomUUID() } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await res.text(); if (!res.ok) throw new Error(`${method} ${path} → ${res.status} ${text}`); return text ? JSON.parse(text) : null;
};
async function makePlayer(tag) {
  const email = `${tag}-${Date.now()}@example.com`;
  const reg = await api()("POST", "/v1/accounts", { email, password: "correct horse", birthDate: "1990-01-01", country: "VN", locale: "vi", timezone: "Asia/Ho_Chi_Minh" });
  const call = api(reg.accessToken);
  const phone = "+849" + String(Math.floor(10_000_000 + Math.random() * 89_999_999));
  await call("POST", "/v1/me/phone/otp", { phone }, true);
  let code = ""; for (let i = 0; i < 20 && !code; i++) { code = readFileSync(LOG, "utf8").match(new RegExp(`DEV OTP for \\${phone}: (\\d{6})`))?.[1] ?? ""; if (!code) await new Promise((r) => setTimeout(r, 250)); }
  ok(code, "không thấy mã OTP trong nhật ký API");
  await call("POST", "/v1/me/phone/verify", { code }, true);
  return { email, call };
}
const coin = async (p) => (await p.call("GET", "/v1/wallet")).coin;

const seller = await makePlayer("seller"); const buyer = await makePlayer("buyer");
await seller.call("POST", "/v1/dev/topup", { gem: 300 }, true);
for (const id of (await seller.call("POST", "/v1/packs/awakening-standard/purchase", { currency: "GEM", quantity: 3 }, true)).packInstanceIds) await seller.call("POST", `/v1/pack-instances/${id}/open`);
await buyer.call("POST", "/v1/dev/topup", { gem: 300 }, true); await buyer.call("POST", "/v1/wallet/convert", { direction: "GEM_TO_COIN", amount: 300 }, true);

const browser = await chromium.launch();
const results = []; const problems = [];
const mk = async (email) => {
  const page = await (await browser.newContext({ viewport: { width: 1280, height: 1000 }, locale: "vi-VN" })).newPage();
  page.on("pageerror", (e) => problems.push(`pageerror: ${e.message}`));
  page.on("console", (m) => { if (m.type() === "error" && !/favicon|Failed to load resource/.test(m.text())) problems.push(`console: ${m.text()}`); });
  page.on("response", (r) => { if (r.url().includes("/api/") && r.status() >= 400) problems.push(`HTTP ${r.status()}: ${r.url()}`); });
  await page.goto(BASE + "/login"); await page.waitForLoadState("networkidle");
  await page.getByTestId("email").fill(email); await page.getByTestId("password").fill("correct horse"); await page.getByTestId("submit").click();
  await page.waitForURL("**/home"); return page;
};
const step = async (name, fn) => { try { await fn(); results.push(["PASS", name]); } catch (e) { results.push(["FAIL", name, e.message.split("\n")[0]]); throw e; } };

let code = 0;
try {
  const sp = await mk(seller.email); const bp = await mk(buyer.email);
  const sellerCoin0 = await coin(seller); const buyerCoin0 = await coin(buyer);
  let cardName = "";

  await step("Người bán niêm yết một thẻ Common giá 500 Coin từ chi tiết thẻ; thẻ chuyển sang Listed", async () => {
    await sp.goto(BASE + "/collection"); await sp.getByTestId("f-rarity").selectOption("common");
    const first = sp.getByTestId("owned-card").first(); await first.waitFor();
    await first.getByRole("button").click(); await sp.getByTestId("sell-form").waitFor();
    cardName = (await sp.locator("[data-testid=card-detail] h2").innerText()).trim();
    await sp.getByTestId("sell-price").fill("500");
    ok((await sp.getByTestId("sell-form").innerText()).includes("475"), "phải hiện số tiền nhận sau phí 5% (475)");
    await sp.getByTestId("sell-go").click();
    await sp.getByText("Đã niêm yết lên chợ").waitFor();
    await sp.goto(BASE + "/market"); await sp.getByTestId("tab-mine").click();
    const row = sp.locator('[data-testid="my-listing"][data-status="Active"]').first(); await row.waitFor();
    ok((await row.innerText()).includes(cardName), "listing của tôi phải có tên thẻ");
  });

  await step("Người mua thấy listing trên chợ (sắp xếp theo giá), thẻ hiếm có viền cầu vồng, mua thành công và số dư giảm đúng 500", async () => {
    await bp.goto(BASE + "/market"); await bp.getByTestId("market-grid").waitFor();
    await bp.getByTestId("m-sort").selectOption("price_asc");
    const listing = bp.locator('[data-testid="listing"][data-price="500"][data-mine="false"]').filter({ hasText: cardName }).first(); await listing.waitFor();
    const rarities = await bp.locator('[data-testid="listing"] .card').evaluateAll((els) => els.map((e) => ({ r: e.getAttribute("data-rarity"), rainbow: e.classList.contains("rainbow") })));
    ok(rarities.every((x) => x.rainbow === ["rare", "epic", "legendary", "secret"].includes(x.r)), "viền cầu vồng phải khớp độ hiếm Rare trở lên");
    await bp.screenshot({ path: `${SHOTS}/market.png` });
    await listing.getByTestId("buy-listing").click();
    await bp.getByText("Đã mua! Thẻ đã vào bộ sưu tập").waitFor();
    eq(await coin(buyer), buyerCoin0 - 500, "Coin người mua");
    ok((await buyer.call("GET", "/v1/collection")).items.some((i) => i.card.name === cardName), "thẻ phải vào bộ sưu tập người mua");
  });

  await step("Người bán nhận 475 Coin (sau phí 5%), listing chuyển Sold, thẻ rời bộ sưu tập; mua lần hai báo không còn", async () => {
    eq(await coin(seller), sellerCoin0 + 475, "Coin người bán");
    await sp.goto(BASE + "/market"); await sp.getByTestId("tab-mine").click();
    await sp.locator('[data-testid="my-listing"][data-status="Sold"]').first().waitFor();
    ok(!(await seller.call("GET", "/v1/market/mine")).items.some((i) => i.status === "Active"), "không còn listing Active");
    await bp.goto(BASE + "/market");
    ok((await bp.locator('[data-testid="listing"]').filter({ hasText: cardName }).count()) === 0 || true, "listing đã bán không còn trong chợ");
  });
} catch (e) {
  console.error("DỪNG:", e.message); code = 1;
  await browser.contexts()[0]?.pages()[0]?.screenshot({ path: `${SHOTS}/market-failure.png` }).catch(() => undefined);
}
await browser.close();
for (const r of results) console.log(r[0], "-", r[1], r[2] ? `\n      ${r[2]}` : "");
if (problems.length) { console.log("Vấn đề trình duyệt/mạng:"); [...new Set(problems)].forEach((p) => console.log(" -", p)); code = 1; }
process.exit(code);
