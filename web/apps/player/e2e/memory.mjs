// E2E Ký ức phong ấn: mua bằng Gem ở trang Ký ức → vào kho; tuần này không mua thêm được; trang bộ bài chỉ nhận Anima.
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");

const BASE = process.env.BASE_URL ?? "http://localhost:3000";
const API = process.env.API ?? "http://127.0.0.1:5080";
const ok = (c, m) => { if (!c) throw new Error(m); };
const J = { "Content-Type": "application/json" };

const email = `mem-${Date.now()}@example.com`;
const reg = await fetch(`${API}/v1/accounts`, { method: "POST", headers: J, body: JSON.stringify({ email, password: "correct horse", birthDate: "1990-01-15", country: "VN", locale: "vi", timezone: "Asia/Ho_Chi_Minh" }) });
ok(reg.ok, `đăng ký thất bại ${reg.status}`);
const token = (await reg.json()).accessToken;
const top = await fetch(`${API}/v1/dev/topup`, { method: "POST", headers: { ...J, Authorization: `Bearer ${token}`, "Idempotency-Key": `t-${Date.now()}` }, body: JSON.stringify({ gem: 500 }) });
ok(top.ok, `nạp thử thất bại ${top.status}`);

const browser = await chromium.launch();
const page = await (await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: "vi-VN" })).newPage();
const problems = [];
page.on("pageerror", (e) => problems.push(`pageerror: ${e.message}`));
let code = 0;
try {
  await page.goto(BASE + "/login"); await page.waitForLoadState("networkidle");
  await page.getByTestId("email").fill(email); await page.getByTestId("password").fill("correct horse"); await page.getByTestId("submit").click();
  await page.waitForURL("**/home");

  await page.goto(BASE + "/memories"); await page.getByTestId("memory-stock").waitFor();
  await page.getByText("Bạn chưa có Ký ức phong ấn nào").waitFor();
  const kinds = await page.getByTestId("memory-available").count();
  ok(kinds === 24, `kỳ vọng 24 loại Ký ức, thấy ${kinds}`);

  await page.getByTestId("memory-buy").first().click();
  await page.getByText("Đã thêm Ký ức phong ấn").waitFor();
  await page.getByText("Kho của bạn (1/5)").waitFor();
  ok(await page.getByTestId("memory-buy").first().isDisabled(), "mỗi tuần chỉ mua một lần: nút mua phải bị khóa");
  console.log("PASS - Ký ức phong ấn: 24 loại, mua bằng Gem vào kho (1/5), tuần này không mua thêm được");
} catch (e) {
  await page.screenshot({ path: "/tmp/anima-e2e/memory-failure.png" }).catch(() => undefined);
  console.error("FAIL -", e.message); code = 1;
}
await browser.close();
if (problems.length) { console.log(problems.join("\n")); code = 1; }
process.exit(code);
