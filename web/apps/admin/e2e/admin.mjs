// E2E trang admin: nhiều vai trò, backend + PostgreSQL thật. Chạy bằng run.sh (hoặc: BASE_URL=http://localhost:3100 API=http://127.0.0.1:5080 node e2e/admin.mjs).
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");
import { mkdirSync } from "node:fs";

const BASE = process.env.BASE_URL ?? "http://localhost:3100";
const API = process.env.API ?? "http://127.0.0.1:5080";
const SHOTS = process.env.SHOTS ?? "/tmp/anima-e2e-admin";
mkdirSync(SHOTS, { recursive: true });
const PASS = "admin-demo-pass";
const results = []; const problems = [];
const step = async (name, fn) => { try { await fn(); results.push(["PASS", name]); } catch (e) { results.push(["FAIL", name, String(e.message).split("\n")[0]]); throw e; } };
const ok = (c, m) => { if (!c) throw new Error(m); };
const eq = (a, b, m) => { if (a !== b) throw new Error(`${m}: expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`); };

// Người chơi thật tạo qua API công khai.
const stamp = Date.now();
const email = `e2e-admin-${stamp}@example.com`;
const reg = await fetch(`${API}/v1/accounts`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, password: "correct horse", birthDate: "1990-01-01", country: "VN", locale: "vi", timezone: "Asia/Ho_Chi_Minh" }) });
ok(reg.ok, `đăng ký người chơi thất bại: ${reg.status}`);
const playerId = (await reg.json()).account?.id ?? null;

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: "vi-VN" });
const page = await ctx.newPage();
let expected4xx = 0;
page.on("pageerror", (e) => problems.push(`pageerror: ${e.message}`));
page.on("console", (m) => { if (m.type() === "error" && !/favicon|Failed to load resource/.test(m.text())) problems.push(`console: ${m.text()}`); });
page.on("response", (r) => { if (r.url().includes("/admin/v1/") && r.status() >= 400) { if (expected4xx-- <= 0) problems.push(`HTTP ${r.status()}: ${r.url()}`); } });
const expectErr = (n = 1) => { expected4xx = n; };

const login = async (role, pw = PASS) => {
  await page.goto(BASE); await page.waitForLoadState("networkidle");
  await page.getByLabel("Email").fill(`${role}@anima.local`); await page.getByLabel("Mật khẩu").fill(pw);
  await page.getByRole("button", { name: "Đăng nhập" }).click();
};
const logged = async (role) => { await login(role); await page.locator("#whoami").waitFor(); };
const logout = async () => { await page.getByRole("button", { name: "Đăng xuất" }).click(); await page.getByRole("button", { name: "Đăng nhập" }).waitFor(); };
const nav = async (a) => { await page.locator(`[data-nav="${a}"]`).click(); await page.waitForLoadState("networkidle"); };
const navs = async () => page.locator("[data-nav]").evaluateAll((els) => els.map((e) => e.getAttribute("data-nav")));
const alertText = async () => (await page.getByRole("alert").first().innerText());
const statusText = async () => (await page.getByRole("status").first().innerText());

try {
  await step("đăng nhập sai mật khẩu bị từ chối, không vào được", async () => {
    expectErr(1); await login("super_admin", "wrong-password");
    ok((await alertText()).includes("INVALID_CREDENTIALS"), "thiếu thông báo INVALID_CREDENTIALS");
    eq(await page.locator("#whoami").count(), 0, "không được vào");
  });

  await step("menu theo vai trò: Economy thấy Tỷ lệ rơi, không thấy Audit/Tài khoản/Quản trị", async () => {
    await logged("economy_manager");
    const n = await navs();
    ok(n.includes("odds") && n.includes("economy") && n.includes("dashboard"), `menu: ${n}`);
    ok(!n.includes("audit") && !n.includes("accounts") && !n.includes("admins"), `menu thừa: ${n}`);
  });

  let draftVersion;
  await step("Economy soạn tỷ lệ: tổng ≠ 100% thì chặn lưu; đủ 100% thì lưu bản nháp, không tự duyệt được", async () => {
    await nav("odds");
    await page.getByRole("button", { name: "Soạn version mới" }).click();
    await page.getByLabel("common", { exact: true }).fill("50");           // chắc chắn lệch tổng
    ok(await page.getByRole("button", { name: "Lưu bản nháp" }).isDisabled(), "tổng sai vẫn lưu được");
    // 6 độ hiếm: 45 / 30 / 15 / 6 / 3 / 1 = 100
    for (const [r, v] of [["common", "45"], ["uncommon", "30"], ["rare", "15"], ["epic", "6"], ["legendary", "3"], ["secret", "1"]]) await page.getByLabel(r, { exact: true }).fill(v);
    ok(await page.getByRole("button", { name: "Lưu bản nháp" }).isEnabled(), "đủ 100% nhưng không lưu được");
    await page.getByRole("button", { name: "Lưu bản nháp" }).click();
    await page.locator('[data-row="odds"][data-version]').filter({ hasText: "draft" }).first().waitFor();
    const row = page.locator('[data-row="odds"]').filter({ hasText: "draft" }).first();
    draftVersion = await row.getAttribute("data-version");
    ok((await row.innerText()).includes("Cần người khác duyệt"), "người soạn phải bị chặn tự duyệt");
    eq(await row.getByRole("button", { name: "Duyệt" }).count(), 0, "có nút Duyệt cho chính người soạn");
    await page.screenshot({ path: `${SHOTS}/odds-draft.png` });
  });

  await step("Economy đề xuất tham số; không tự duyệt được", async () => {
    await nav("economy");
    await page.getByLabel("Tham số").selectOption("forge_fee_coin");
    await page.getByLabel("Giá trị mới", { exact: false }).fill("555");
    await page.getByRole("button", { name: "Gửi đề xuất" }).click();
    const row = page.locator('[data-row="change"][data-key="forge_fee_coin"]').first();
    await row.waitFor();
    ok((await row.innerText()).includes("Cần người khác duyệt"), "phải chặn tự duyệt");
  });
  await logout();

  await step("Super Admin duyệt tỷ lệ (version đang áp dụng) và tham số", async () => {
    await logged("super_admin");
    await nav("odds");
    const row = page.locator(`[data-row="odds"][data-version="${draftVersion}"]`);
    await row.getByRole("button", { name: "Duyệt" }).click();
    await row.getByText("approved").waitFor();
    await nav("economy");
    await page.locator('[data-row="change"][data-key="forge_fee_coin"]').first().getByRole("button", { name: "Duyệt" }).click();
    await page.locator('[data-param="forge_fee_coin"]').filter({ hasText: "555" }).waitFor();
  });

  await step("Super Admin thấy Quản trị viên và tạo admin mới", async () => {
    await nav("admins");
    await page.getByLabel("Email admin").fill(`new-${stamp}@anima.local`);
    await page.getByLabel("Mật khẩu admin").fill("short");
    ok(await page.getByRole("button", { name: "Tạo", exact: true }).isDisabled(), "mật khẩu ngắn vẫn tạo được");
    await page.getByLabel("Mật khẩu admin").fill("a-long-enough-password");
    await page.getByRole("button", { name: "Tạo", exact: true }).click();
    await page.getByText(`new-${stamp}@anima.local`).waitFor();
  });
  await logout();

  let compTicket = `TCK-${stamp}`;
  await step("CS tìm người chơi: dữ liệu bị che, không xem rõ được; tạo bồi thường; không duyệt được", async () => {
    await logged("cs_agent");
    const n = await navs(); ok(!n.includes("audit") && n.includes("compensations"), `menu CS: ${n}`);
    await nav("accounts");
    await page.getByLabel("Từ khóa").fill(email); await page.getByRole("button", { name: "Tìm" }).click();
    const row = page.locator('[data-row="account"]').first(); await row.waitFor();
    const txt = await row.innerText();
    ok(!txt.includes(email) && txt.includes("***"), `email phải bị che: ${txt}`);
    await row.getByRole("button", { name: "Chi tiết" }).click();
    await page.locator("#account-detail").waitFor();
    ok((await page.locator("#account-detail").innerText()).includes("Vai trò của bạn không được xem"), "CS không được xem rõ");
    eq(await page.getByRole("button", { name: "Ban", exact: true }).count(), 0, "CS không có nút Ban");
    const id = (await page.locator("#account-detail .mono").first().innerText()).trim(); eq(id, playerId, "id tài khoản");
    await nav("compensations");
    await page.getByLabel("ID tài khoản").fill(id); await page.getByLabel("Coin", { exact: true }).fill("500"); await page.getByLabel("Ticket").fill(compTicket); await page.getByLabel("Lý do").fill("Lỗi mở pack");
    await page.getByRole("button", { name: "Tạo", exact: true }).click();
    const c = page.locator(`[data-row="comp"][data-ticket="${compTicket}"]`); await c.waitFor();
    eq(await c.getByRole("button", { name: "Duyệt" }).count(), 0, "CS không được duyệt");
  });
  await logout();

  await step("Fraud: xem rõ PII (ghi audit), ban cần lý do, duyệt bồi thường", async () => {
    await logged("fraud_analyst");
    await nav("accounts");
    await page.getByLabel("Từ khóa").fill(email); await page.getByRole("button", { name: "Tìm" }).click();
    await page.locator('[data-row="account"]').first().getByRole("button", { name: "Chi tiết" }).click();
    await page.locator("#account-detail").waitFor();
    await page.getByRole("button", { name: /Xem rõ/ }).click();
    await page.locator("#pii").waitFor();
    ok((await page.locator("#pii").innerText()).includes(email), "PII phải hiện rõ email");
    expectErr(1);
    await page.getByRole("button", { name: "Ban", exact: true }).click();
    ok((await alertText()).includes("REASON_REQUIRED"), "ban không lý do phải bị từ chối");
    await page.getByLabel("Lý do", { exact: true }).fill("Nghi gian lận thanh toán");
    await page.getByRole("button", { name: "Ban", exact: true }).click();
    await page.locator("#account-detail").getByText("banned", { exact: true }).first().waitFor();
    eq(await page.getByRole("button", { name: "Gỡ ban" }).count(), 0, "Fraud không được gỡ ban");
    await nav("compensations");
    const c = page.locator(`[data-row="comp"][data-ticket="${compTicket}"]`);
    await c.getByRole("button", { name: "Duyệt" }).click();
    await c.getByText("approved").waitFor();
    await nav("audit");
    await page.locator('[data-row="audit"][data-action="PII_VIEW"]').first().waitFor();
    await page.locator('[data-row="audit"][data-action="BAN"]').first().waitFor();
    await page.locator('[data-row="audit"][data-action="COMP_APPROVE"]').first().waitFor();
    await page.getByLabel("Lọc hành động").fill("ban");
    await page.waitForFunction(() => [...document.querySelectorAll('[data-row="audit"]')].every((r) => r.getAttribute("data-action") === "BAN"));
    await page.screenshot({ path: `${SHOTS}/audit.png` });
  });
  await logout();

  await step("Super Admin gỡ ban; tài khoản về active", async () => {
    await logged("super_admin");
    await nav("accounts");
    await page.getByLabel("Từ khóa").fill(email); await page.getByRole("button", { name: "Tìm" }).click();
    await page.locator('[data-row="account"]').first().getByRole("button", { name: "Chi tiết" }).click();
    await page.getByLabel("Lý do", { exact: true }).fill("Đã xác minh, hiểu nhầm");
    await page.getByRole("button", { name: "Gỡ ban" }).click();
    await page.locator("#account-detail").getByText("verified", { exact: false }).first().waitFor();
    await nav("dashboard");
    await page.locator("[data-metric]").first().waitFor();
    await page.screenshot({ path: `${SHOTS}/dashboard.png` });
  });
  await logout();

  await step("Content Manager ngừng phát hành một thẻ; không thấy mục Tỷ lệ rơi", async () => {
    await logged("content_manager");
    const n = await navs(); ok(!n.includes("odds") && n.includes("cards"), `menu content: ${n}`);
    await nav("cards");
    const row = page.locator('[data-row="card"]').filter({ hasText: "common" }).last();
    await row.getByRole("button", { name: "Ngừng phát hành" }).click();
    await row.getByText("đã ngừng").waitFor();
  });

  await step("phiên hết hạn/token sai: về màn đăng nhập", async () => {
    await page.evaluate(() => sessionStorage.setItem("anima.admin.token", "bad.token.value"));
    expectErr(2); await page.reload();
    await page.getByRole("button", { name: "Đăng nhập" }).waitFor();
  });
} finally {
  await browser.close();
  console.log(results.map((r) => r.join("  ")).join("\n"));
  if (problems.length) console.log("\nVẤN ĐỀ:\n" + problems.join("\n"));
}
const failed = results.filter((r) => r[0] === "FAIL").length;
console.log(`\n${results.length - failed}/${results.length} bước đạt`);
process.exit(failed || problems.length ? 1 : 0);
