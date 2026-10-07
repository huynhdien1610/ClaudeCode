# ANIMA — Website quản trị (Admin)

Vite + React 19 + TypeScript (strict). Chỉ dùng tiếng Việt (công cụ nội bộ). Gọi backend qua `/admin/v1/*` (Vite chuyển tiếp tới `API_URL`, mặc định `http://127.0.0.1:5080`). Token admin lưu ở `sessionStorage` (mất khi đóng tab); JWT admin tách hẳn token người chơi.

> Lệch so với tài liệu kỹ thuật: SAD dự kiến Refine + Ant Design; bản này dùng Vite + React thuần, giao diện chung token với web người chơi, để gọn và dễ kiểm thử. Có thể thay bằng Refine sau mà không đổi API.

## Chạy

```bash
infra/dev-postgres.sh start
dotnet run --project backend/src/Anima.Api          # Development tự tạo admin demo
pnpm -C web install && pnpm -C web dev:admin        # http://localhost:3100
```

Admin demo (chỉ môi trường Development, `Admin:SeedDemoUsers`): `<vai trò>@anima.local` / `admin-demo-pass`, với vai trò `cs_agent`, `content_manager`, `economy_manager`, `fraud_analyst`, `finance_viewer`, `super_admin` (và `super_admin2`). Ở môi trường thật đặt `Admin:BootstrapEmail` + `Admin:BootstrapPassword` để tạo Super Admin đầu tiên và `Admin:JwtKey` riêng.

## Màn hình theo vai trò (BRD 12.2)

| Khu vực | Ai thấy | Ghi chú |
| --- | --- | --- |
| Dashboard | Economy, Finance, Super | số liệu tổng hợp từ các module |
| Tài khoản | CS, Fraud, Super | email/SĐT bị che; **Xem rõ** (Fraud, Super) ghi audit; khóa/ban cần lý do; gỡ ban chỉ Super |
| Tỷ lệ rơi | Economy (soạn), Economy/Super (duyệt) | nhập %, tổng phải đúng 100%; người soạn không tự duyệt |
| Tham số kinh tế | Economy (đề xuất), Economy/Super (duyệt) | maker-checker, giá trị ngoài khoảng bị từ chối |
| Thẻ | Content, Economy, Super | ngừng phát hành (không xóa thẻ đã phát hành) |
| Bồi thường | CS (tạo), Fraud (duyệt) | chỉ Coin, bắt buộc ticket; không bao giờ cộng Gem trực tiếp |
| Audit log | Fraud, Super | chỉ thêm; có cả các lần bị từ chối |
| Quản trị viên | Super | tạo/khóa admin; token của admin bị khóa mất hiệu lực ngay |

UI chỉ ẩn/hiện theo vai trò (`src/perm.ts`); backend mới là nơi quyết định và ghi audit khi từ chối (SC-ADM-03).

| Lệnh | Việc |
| --- | --- |
| `pnpm -C web --filter @anima/admin test` | test đơn vị (quyền, ppm) |
| `pnpm -C web --filter @anima/admin build` | typecheck + build |
| `web/apps/admin/e2e/run.sh` | E2E Playwright nhiều vai trò với backend + PostgreSQL thật (cổng 5081/3100) |
