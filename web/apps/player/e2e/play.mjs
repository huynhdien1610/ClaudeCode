// E2E: chơi trọn vòng đời trên trình duyệt thật với backend và PostgreSQL thật.
// Chạy: NODE_PATH=$(npm root -g) BASE_URL=http://localhost:3000 API_LOG=/tmp/anima-api.log node e2e/play.mjs
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");   // dùng được cả playwright cài cục bộ lẫn toàn cục (NODE_PATH)
import { readFileSync, mkdirSync } from "node:fs";

const BASE = process.env.BASE_URL ?? "http://localhost:3000";
const LOG = process.env.API_LOG ?? "/tmp/anima-api.log";
const SHOTS = process.env.SHOTS ?? "/tmp/anima-e2e";
mkdirSync(SHOTS, { recursive: true });

const results = []; const problems = [];
const step = async (name, fn) => { try { await fn(); results.push(["PASS", name]); } catch (e) { results.push(["FAIL", name, String(e.message).split("\n")[0]]); throw e; } };
const eq = (a, b, msg) => { if (a !== b) throw new Error(`${msg}: expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`); };
const ok = (c, msg) => { if (!c) throw new Error(msg); };

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: "en-US", timezoneId: "Asia/Ho_Chi_Minh" });
const page = await ctx.newPage();
page.on("pageerror", (e) => problems.push(`pageerror: ${e.message}`));
page.on("console", (m) => { if (m.type() === "error" && !/favicon|fonts\.g|Failed to load resource/.test(m.text())) problems.push(`console: ${m.text()}`); });
// Yêu cầu bị huỷ vì điều hướng (ERR_ABORTED) là bình thường; mọi lỗi mạng khác thì không.
page.on("requestfailed", (r) => { if (!/fonts\.g|favicon/.test(r.url()) && r.failure()?.errorText !== "net::ERR_ABORTED") problems.push(`requestfailed: ${r.url()} ${r.failure()?.errorText}`); });
// Mọi phản hồi lỗi từ API đều là vấn đề, trừ đăng nhập sai mật khẩu có chủ đích.
page.on("response", (r) => { if (r.url().includes("/api/") && r.status() >= 400 && !(r.url().endsWith("/v1/auth/login") && r.status() === 401)) problems.push(`HTTP ${r.status()}: ${r.url()}`); });

const num = async (sel) => Number((await page.locator(sel).first().innerText()).replace(/[^\d-]/g, ""));
const balances = async () => {
  const txt = await page.getByTestId("balances").innerText();   // "<coin> Coin · <gem> Gem"
  const m = txt.replace(/[,. ]/g, "").match(/(\d+)\s*Coin\D+(\d+)\s*Gem/i) ?? txt.replace(/[,. ]/g, "").match(/(\d+)\D+(\d+)/);
  return { coin: Number(m[1]), gem: Number(m[2]) };
};
/** Chờ (có giới hạn) tới khi phần tử có đúng nội dung; báo giá trị cuối cùng nếu hết giờ. */
const waitText = async (testid, expected, timeout = 8000) => {
  const loc = page.getByTestId(testid).first();
  try { await page.waitForFunction(([id, v]) => document.querySelector(`[data-testid="${id}"]`)?.textContent?.trim() === v, [testid, expected], { timeout }); }
  catch { throw new Error(`${testid}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(await loc.innerText().catch(() => null))}`); }
};
const goto = async (path) => { await page.goto(BASE + path); await page.waitForLoadState("networkidle"); };

/** Mở overlay pack: xé, lật từng thẻ tới hết, về tổng kết rồi đóng. */
async function playOpening({ skip = false } = {}) {
  await page.getByTestId("pack-opening").waitFor();
  await page.getByTestId("tear").click();
  if (skip) await page.getByTestId("skip").click();
  else {
    for (let i = 0; i < 12; i++) {
      const stage = await page.getByTestId("pack-opening").getAttribute("data-stage");
      if (stage === "summary") break;
      await page.getByTestId("reveal").waitFor();
      await page.getByTestId("flip").click({ timeout: 3000 }).catch(() => undefined);
      await page.getByTestId("next").click();
    }
  }
  await page.getByTestId("summary").waitFor();
  const cards = await page.getByTestId("summary").locator(".card").count();
  await page.screenshot({ path: `${SHOTS}/summary.png` });
  await page.getByTestId("done").click();
  await page.getByTestId("pack-opening").waitFor({ state: "detached" });
  return cards;
}

try {
  const email = `e2e${Date.now()}@test.local`;

  await step("Đăng ký tài khoản mới (giao diện tiếng Anh theo trình duyệt)", async () => {
    await goto("/login");
    await page.screenshot({ path: `${SHOTS}/login.png` });
    await page.getByTestId("switch").click();
    await page.getByTestId("email").fill(email);
    await page.getByTestId("password").fill("correct horse");
    await page.getByTestId("birth").fill("1995-04-12");
    await page.getByTestId("country").selectOption("VN");
    await page.getByTestId("submit").click();
    await page.waitForURL("**/home");
    eq(await page.locator("html").getAttribute("lang"), "en", "lang");
  });

  await step("Có 100 Coin thưởng đăng nhập và 1 pack chưa mở (gói chào mừng)", async () => {
    await page.getByTestId("open-welcome").waitFor();
    eq((await balances()).coin, 100, "coin");
    await waitText("unopened-count", "1");
    await page.screenshot({ path: `${SHOTS}/home.png` });
  });

  await step("Mở Gói chào mừng: xé → lật từng thẻ → tổng kết 5 thẻ", async () => {
    await page.getByTestId("open-welcome").click();
    eq(await playOpening(), 5, "số thẻ");
    await waitText("progress-text", "5/100 cards");
  });

  await step("Ví: nạp thử 1000 Gem", async () => {
    await goto("/wallet");
    await page.getByTestId("topup-1000").click();
    await page.waitForFunction(() => document.querySelector('[data-testid="gem"]')?.textContent?.replace(/\D/g, "") === "1000");
  });

  await step("Cửa hàng: tỷ lệ rơi công khai tổng 100%, mua 3 pack bằng Gem", async () => {
    await goto("/store");
    await page.getByTestId("odds").waitFor();
    const pcts = await page.getByTestId("odds").locator(".odds-row span:last-child").allInnerTexts();
    eq(pcts.length, 6, "số rarity");
    eq(pcts.reduce((s, x) => s + parseFloat(x), 0), 100, "tổng tỷ lệ");
    await page.getByTestId("qty").fill("3");
    await page.getByTestId("buy-gem").click();
    await page.waitForFunction(() => /700/.test(document.querySelector('[data-testid="balances"]')?.textContent ?? ""));
    await page.screenshot({ path: `${SHOTS}/store.png` });
  });

  await step("Mở 3 pack: pack đầu xem đủ hoạt ảnh, hai pack sau dùng Skip; đủ 15 thẻ mới", async () => {
    await goto("/packs");
    eq(await page.getByTestId("pack-item").count(), 3, "pack chưa mở");
    for (let i = 0; i < 3; i++) {
      await page.getByTestId("open-pack").first().click();
      eq(await playOpening({ skip: i > 0 }), 5, `pack ${i + 1}`);
      await page.waitForTimeout(150);
    }
    await page.getByText("You have no unopened packs.").waitFor();
  });

  await step("Bộ sưu tập: 20 thẻ, lọc theo rarity/hệ, xem chi tiết và truyện", async () => {
    await goto("/collection");
    await page.getByTestId("owned-card").first().waitFor();
    eq(await page.getByTestId("owned-card").count(), 20, "số thẻ");
    await page.getByTestId("f-rarity").selectOption("common");
    await page.waitForTimeout(400);
    const commons = await page.getByTestId("owned-card").count();
    ok(commons > 0 && commons <= 20, "lọc common");
    await page.getByTestId("f-rarity").selectOption("");
    await page.waitForTimeout(300);
    await page.getByTestId("owned-card").first().click();
    await page.getByTestId("card-detail").waitFor();
    await page.getByTestId("story").waitFor();
    await page.screenshot({ path: `${SHOTS}/detail.png` });
    await page.keyboard.press("Escape");
  });

  await step("Ví: đổi 10 Gem → 90 Coin để có phí rèn", async () => {
    await goto("/wallet");
    await page.getByTestId("conv-amount").fill("10");
    await page.getByTestId("conv-go").click();
    await page.waitForFunction(() => document.querySelector('[data-testid="coin"]')?.textContent?.replace(/\D/g, "") === "190");
  });

  await step("Lò rèn: chọn 2 thẻ, rèn bằng Coin, lật thẻ chưa lật", async () => {
    await goto("/forge");
    await page.getByTestId("forge-card").first().waitFor();
    eq(await page.getByTestId("forge-card").count(), 15, "thẻ rèn được (không tính 5 thẻ gắn chặt tài khoản)");
    ok(await page.getByTestId("forge-coin").isDisabled(), "chưa chọn đủ 2 thẻ thì nút bị khóa");
    await page.getByTestId("forge-card").nth(0).locator("button").click();
    await page.getByTestId("forge-card").nth(1).locator("button").click();
    await page.screenshot({ path: `${SHOTS}/forge.png` });
    await page.getByTestId("forge-coin").click();
    await page.getByTestId("sealed-section").waitFor();
    eq(await page.getByTestId("forge-card").count(), 13, "2 thẻ đã bị hủy");
    eq((await balances()).coin, 140, "phí rèn 50 Coin");
    await page.getByTestId("reveal-sealed").click();
    await playOpening();
  });

  await step("Xác thực SĐT bằng OTP và quy đổi Coin → Gem", async () => {
    await goto("/account");
    const phone = "+849" + String(Math.floor(Math.random() * 1e8)).padStart(8, "0");   // mỗi số chỉ xác thực được cho một tài khoản (BR-ACC-02)
    await page.getByTestId("phone").fill(phone);
    await page.getByTestId("send-otp").click();
    await page.getByTestId("otp").waitFor();
    await page.waitForTimeout(300);
    const log = readFileSync(LOG, "utf8");
    const code = [...log.matchAll(new RegExp(`DEV OTP for \\${phone}: (\\d{6})`, "g"))].at(-1)?.[1];
    ok(code, "không thấy mã OTP trong log backend");
    await page.getByTestId("otp").fill(code);
    await page.getByTestId("verify-otp").click();
    await page.getByTestId("phone-done").waitFor();
    eq(await page.getByTestId("status").innerText(), "Verified", "trạng thái");
    await goto("/wallet");
    await page.getByTestId("conv-dir").selectOption("COIN_TO_GEM");
    await page.getByTestId("conv-amount").fill("110");
    await page.getByTestId("conv-go").click();
    await page.waitForFunction(() => document.querySelector('[data-testid="gem"]')?.textContent?.replace(/\D/g, "") === "700");   // 690 + 10
  });

  await step("Công bằng: đổi seed công bố seed cũ, công cụ kiểm chứng tính lại và khớp mã băm", async () => {
    await goto("/account");
    const hashBefore = await page.getByTestId("seed-hash").innerText();
    await page.getByTestId("rotate").click();
    await page.getByTestId("revealed").waitFor();
    const hashAfter = await page.getByTestId("seed-hash").innerText();
    ok(hashBefore !== hashAfter, "mã băm phải đổi");
    eq(await page.getByTestId("nonce").innerText(), "1", "nonce về 1");
    await page.getByTestId("v-run").click();
    await page.getByTestId("v-result").waitFor();
    const rolls = (await page.getByTestId("v-rolls").innerText()).split(",").map((x) => Number(x.trim()));
    eq(rolls.length, 5, "số giá trị quay");
    ok(rolls.every((r) => r >= 0 && r < 1_000_000), "giá trị quay trong [0, 1,000,000)");
    ok((await page.getByTestId("v-result").innerText()).includes("✓"), "mã băm khớp seed đã công bố");
    await page.screenshot({ path: `${SHOTS}/fairness.png` });
  });

  await step("Đổi ngôn ngữ sang 简体中文 / 繁體中文 / Tiếng Việt: giao diện và thẻ đổi theo, lưu theo tài khoản", async () => {
    await page.locator(".topbar select").selectOption("zh-Hans");
    await page.getByRole("link", { name: "商店" }).waitFor();
    eq(await page.locator("html").getAttribute("lang"), "zh-Hans", "lang");
    await goto("/collection");
    await page.getByTestId("owned-card").first().waitFor();
    await page.screenshot({ path: `${SHOTS}/zh-hans.png` });
    await page.locator(".topbar select").selectOption("zh-Hant");
    await page.getByRole("link", { name: "熔煉爐" }).waitFor();
    await page.screenshot({ path: `${SHOTS}/zh-hant.png` });
    await page.locator(".topbar select").selectOption("vi");
    await page.getByRole("link", { name: "Bộ sưu tập" }).waitFor();
    await page.reload(); await page.waitForLoadState("networkidle");           // ngôn ngữ phải còn sau khi tải lại
    await page.getByRole("link", { name: "Cửa hàng" }).waitFor();
  });

  await step("Ngôn ngữ đã lưu theo tài khoản: phiên trình duyệt mới (không có localStorage) đăng nhập vẫn thấy tiếng Việt", async () => {
    const fresh = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: "en-US" });
    const p2 = await fresh.newPage();
    await p2.goto(BASE + "/login"); await p2.waitForLoadState("networkidle");
    await p2.getByTestId("email").fill(email); await p2.getByTestId("password").fill("correct horse"); await p2.getByTestId("submit").click();
    await p2.waitForURL("**/home");
    await p2.locator(".nav").getByRole("link", { name: "Cửa hàng", exact: true }).waitFor();
    eq(await p2.locator("html").getAttribute("lang"), "vi", "lang theo tài khoản");
    await fresh.close();
  });

  await step("Đăng xuất rồi đăng nhập lại: dữ liệu giữ nguyên, trang cần đăng nhập bị chuyển về /login", async () => {
    await page.getByRole("button", { name: "Đăng xuất" }).click();
    await page.waitForURL("**/login");
    await goto("/wallet");
    await page.waitForURL("**/login");
    await page.getByTestId("email").fill(email);
    await page.getByTestId("password").fill("correct horse");
    await page.getByTestId("submit").click();
    await page.waitForURL("**/home");
    await page.getByTestId("progress-text").waitFor();
    eq(await page.getByTestId("progress-text").innerText().then((s) => s.split("/")[0].trim()) !== "", true, "tiến độ còn");
  });

  await step("Nhiệm vụ Tân thủ: ngày 1 nhận được 1 pack cơ bản gắn chặt tài khoản; ngày 2 chưa tới nên chưa nhận được", async () => {
    await goto("/quests");
    const d1 = page.locator('[data-testid="quest-day"][data-day="1"]');
    await d1.waitFor();
    eq(await d1.getAttribute("data-state"), "ready", "ngày 1 sẵn sàng (đã mở gói chào mừng)");
    ok((await d1.innerText()).includes("Hôm nay"), "ngày 1 là hôm nay");
    const d2 = page.locator('[data-testid="quest-day"][data-day="2"]');
    ok(await d2.getByTestId("claim-quest").isDisabled(), "ngày 2 chưa tới phải bị khóa");
    await d1.getByTestId("claim-quest").click();
    await page.locator('[data-testid="quest-day"][data-day="1"][data-state="claimed"]').waitFor();
    await goto("/packs");
    await page.getByTestId("pack-item").first().waitFor();
    const txt = await page.getByTestId("pack-list").innerText();
    ok(txt.includes("Pack cơ bản") && txt.includes("Gắn chặt"), `thiếu pack cơ bản gắn chặt: ${txt}`);
    await page.screenshot({ path: `${SHOTS}/quests.png` });
  });

  await step("Nạp Gem qua cổng thanh toán (sandbox): mua gói → trang cổng → thanh toán → Gem tăng đúng gói; có dòng \"Nạp Gem\" trong lịch sử", async () => {
    await goto("/wallet");
    const before = (await balances()).gem;
    await page.getByTestId("buy-gem_100").click();
    await page.waitForURL("**/pay/sandbox?order=*");
    const box = page.getByTestId("sandbox-order"); await box.waitFor();
    eq(await box.getAttribute("data-status"), "Created", "đơn mới chờ thanh toán");
    await page.getByTestId("sandbox-pay").click();
    await page.locator('[data-testid="sandbox-order"][data-status="Paid"]').waitFor();
    await page.getByTestId("back-wallet").click();
    await page.waitForURL("**/wallet");
    await page.waitForFunction((v) => { const m = document.querySelector('[data-testid="gem"]')?.textContent?.replace(/\D/g, ""); return Number(m) === v; }, before + 100, { timeout: 8000 });
    ok((await page.getByTestId("ledger").innerText()).includes("Nạp Gem"), "thiếu dòng Nạp Gem trong lịch sử");
    await page.screenshot({ path: `${SHOTS}/payment.png` });
  });

  await step("Sai mật khẩu hiện thông báo lỗi theo mã (tiếng Việt)", async () => {
    await page.getByRole("button", { name: "Đăng xuất" }).click();
    await page.waitForURL("**/login");
    await page.getByTestId("email").fill(email);
    await page.getByTestId("password").fill("wrong password");
    await page.getByTestId("submit").click();
    await page.getByText("Sai email hoặc mật khẩu.").waitFor();
  });

  await step("Giao diện điện thoại (390px) không tràn ngang", async () => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.getByTestId("password").fill("correct horse");
    await page.getByTestId("submit").click();
    await page.waitForURL("**/home");
    for (const p of ["/home", "/store", "/quests", "/collection", "/forge", "/wallet", "/account"]) {
      await goto(p);
      const [sw, iw] = await page.evaluate(() => [document.documentElement.scrollWidth, innerWidth]);
      ok(sw <= iw + 1, `${p} tràn ngang: ${sw} > ${iw}`);
    }
    await goto("/collection"); await page.screenshot({ path: `${SHOTS}/mobile-collection.png`, fullPage: false });
  });
} catch (e) {
  await page.screenshot({ path: `${SHOTS}/failure.png` }).catch(() => undefined);
  console.error("E2E dừng ở bước lỗi:", e.message);
}

await browser.close();
for (const r of results) console.log(r[0], "-", r[1], r[2] ? `\n      ${r[2]}` : "");
const failed = results.filter((r) => r[0] === "FAIL").length;
if (problems.length) { console.log("\nVấn đề trình duyệt/mạng:"); [...new Set(problems)].forEach((p) => console.log(" -", p)); }
console.log(`\n${results.length - failed}/${results.length} bước đạt, ${problems.length} cảnh báo trình duyệt. Ảnh chụp: ${SHOTS}`);
process.exit(failed || problems.length ? 1 : 0);
