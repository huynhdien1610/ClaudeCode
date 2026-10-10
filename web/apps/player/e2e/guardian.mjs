// E2E đồng ý của người giám hộ: tài khoản 16 tuổi không mua được Gem → xin đồng ý → người giám hộ xác nhận qua liên kết → mua được.
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");
import { readFileSync } from "node:fs";

const BASE = process.env.BASE_URL ?? "http://localhost:3000";
const API = process.env.API ?? "http://127.0.0.1:5080";
const LOG = process.env.API_LOG ?? "/tmp/anima-api.log";
const ok = (c, m) => { if (!c) throw new Error(m); };

const email = `minor-${Date.now()}@example.com`;
const guardianEmail = `parent-${Date.now()}@example.com`;
const y = new Date().getUTCFullYear() - 16;
const reg = await fetch(`${API}/v1/accounts`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, password: "correct horse", birthDate: `${y}-01-15`, country: "VN", locale: "vi", timezone: "Asia/Ho_Chi_Minh" }) });
ok(reg.ok, `đăng ký thất bại ${reg.status}`);

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: "vi-VN" });
const page = await ctx.newPage();
const problems = [];
page.on("pageerror", (e) => problems.push(`pageerror: ${e.message}`));
let code = 0;
try {
  await page.goto(BASE + "/login"); await page.waitForLoadState("networkidle");
  await page.getByTestId("email").fill(email); await page.getByTestId("password").fill("correct horse"); await page.getByTestId("submit").click();
  await page.waitForURL("**/home");

  // 1. Chưa có đồng ý: bấm mua Gem bị từ chối với thông báo tiếng Việt
  await page.goto(BASE + "/wallet"); await page.getByTestId("buy-gem_100").click();
  await page.getByText("cha mẹ/người giám hộ đồng ý trước").waitFor();
  ok(!page.url().includes("/pay/sandbox"), "không được sang trang thanh toán");

  // 2. Xin đồng ý ở trang Tài khoản
  await page.goto(BASE + "/account"); await page.getByTestId("guardian-panel").waitFor();
  await page.getByTestId("guardian-email").fill(guardianEmail); await page.getByTestId("guardian-send").click();
  await page.getByText("Đã gửi yêu cầu").first().waitFor();

  // 3. Người giám hộ (phiên ẩn danh, không đăng nhập) mở liên kết có mã lấy từ nhật ký dev
  let token = "";
  for (let i = 0; i < 20 && !token; i++) { const m = readFileSync(LOG, "utf8").match(new RegExp(`guardian consent token for ${guardianEmail}: (\\S+)`)); token = m?.[1] ?? ""; if (!token) await new Promise((r) => setTimeout(r, 250)); }
  ok(token, "không thấy mã xác nhận trong nhật ký API");
  const gp = await (await browser.newContext({ locale: "vi-VN" })).newPage();
  await gp.goto(`${BASE}/guardian?token=${token}`); await gp.getByTestId("guardian-confirm").click(); await gp.getByTestId("guardian-ok").waitFor();
  await gp.goto(`${BASE}/guardian?token=${token}`); await gp.getByTestId("guardian-confirm").click();
  await gp.getByText("không hợp lệ hoặc đã hết hạn").waitFor();           // dùng lại mã bị từ chối

  // 4. Giờ mua Gem được
  await page.goto(BASE + "/account"); await page.getByTestId("guardian-done").waitFor();
  await page.goto(BASE + "/wallet"); await page.getByTestId("buy-gem_100").click();
  await page.waitForURL("**/pay/sandbox?order=*");
  console.log("PASS - Người giám hộ: 16 tuổi bị chặn nạp → xin đồng ý → xác nhận qua liên kết (dùng một lần) → nạp được");
} catch (e) {
  await page.screenshot({ path: "/tmp/anima-e2e/guardian-failure.png" }).catch(() => undefined);
  console.error("FAIL -", e.message); code = 1;
}
await browser.close();
if (problems.length) { console.log(problems.join("\n")); code = 1; }
process.exit(code);
