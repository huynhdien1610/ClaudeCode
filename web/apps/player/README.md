# ANIMA — Website người chơi

Next.js 16 (App Router) + TypeScript, gọi backend qua `/api/*` (Next chuyển tiếp tới `API_URL`, mặc định `http://127.0.0.1:5080`). Không dùng thư viện UI; giao diện sáng, phẳng, kế thừa token của `prototype/`.

## Chạy

```bash
infra/dev-postgres.sh start
dotnet run --project backend/src/Anima.Api          # backend: http://localhost:5080
pnpm -C web install && pnpm -C web dev               # website: http://localhost:3000
```

Biến môi trường: xem `.env.example`. `NEXT_PUBLIC_DEV_TOPUP=1` hiện khung nạp Gem thử (backend phải bật `Dev:MockTopUp`, mặc định bật ở Development).

| Lệnh | Việc |
|---|---|
| `pnpm -C web typecheck` | Kiểm tra kiểu |
| `pnpm -C web test` | Test đơn vị (khóa dịch, nhận diện ngôn ngữ, mã lỗi) |
| `pnpm -C web build` | Build production |
| `web/apps/player/e2e/run.sh` | E2E trọn bộ: PostgreSQL + backend + website + Playwright trên Chromium, 16 bước, ảnh chụp ở `/tmp/anima-e2e` |

## Màn hình

| Đường dẫn | Nội dung | Quy tắc BRD |
|---|---|---|
| `/login` | Đăng nhập, đăng ký (tuổi, quốc gia, múi giờ trình duyệt) | BR-ACC-01, BR-GEO-03/04 |
| `/home` | Gói chào mừng, số pack chưa mở, tiến độ set | BR-NEW-01 |
| `/store` | Pack, tỷ lệ rơi công khai, mua bằng Coin/Gem, pity | BR-PACK-01/05 |
| `/packs` | Pack chưa mở; mở với hoạt ảnh Entry → Reveal → Summary, có Skip, Climax Epic+, flash Legendary+ | BR-PACK-02/06/07/08, NFR-09 |
| `/collection` | Lọc theo hệ/rarity/loại/tên, chi tiết thẻ `#n/N`, truyện chỉ khi đã sở hữu | US-05.* |
| `/forge` | Chọn 2 thẻ, trả phí Coin hoặc Gem, thẻ chưa lật, tỷ lệ rèn công khai | BR-FRG-* |
| `/wallet` | Số dư, quy đổi Gem ↔ Coin, lịch sử bút toán, nạp thử | BR-WAL-05/06 |
| `/account` | Ngôn ngữ, xác thực SĐT (OTP), công bằng: seed, đổi seed, công cụ kiểm chứng | BR-PF-*, BR-ACC-05 |

Bốn ngôn ngữ (vi, en, zh-Hans, zh-Hant): `src/lib/dict.ts`. Test kiểm tra mọi ngôn ngữ đủ khóa và cùng biến `{x}` (BR-I18N-02). Ngôn ngữ lấy theo trình duyệt lần đầu, sau đó lưu theo tài khoản và đồng bộ qua `PUT /v1/me/locale`. Mã lỗi hiển thị theo `code` của backend (BR-I18N-04).

## Khác tài liệu / việc còn lại

- Token đăng nhập đang lưu ở `localStorage` cho bản thử; khi lên production nên chuyển sang cookie httpOnly qua một BFF (Next route handlers) để giảm rủi ro XSS.
- Dùng từ điển tự viết thay `next-intl` (SAD 18.2): chưa cần route theo ngôn ngữ vì mọi trang đều sau đăng nhập. Chuyển sang next-intl khi làm trang công khai (SEO, `/vi`, `/en`...).
- Chưa có: nhúng Unity Web cho mở pack (T-07, spike T009), trang nạp Gem qua cổng thanh toán, hồ sơ công khai, chợ, ví NFT, trang quản trị (`web/apps/admin`).
- Hoạt ảnh mở pack là bản CSS rút gọn (LiteReveal), chưa phải cinematic theo timeline Master Document §4.5.
- Chưa kiểm thử trên Safari/Firefox; chưa có kiểm tra accessibility tự động (axe).
