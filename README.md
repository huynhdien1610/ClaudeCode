# ANIMA: Echoes of the Heart

Nền tảng sưu tầm thẻ bài số (app mobile + website người chơi + website admin) với IP gốc dựa trên hệ thống 8 hệ cảm xúc. Phát hành toàn cầu; tiếng Việt, tiếng Anh, tiếng Trung (giản thể, phồn thể).

**Trạng thái:** Concept & Design Phase

## Tài liệu

- [Master Document](docs/ANIMA_Master_Document.md) — v1.4: tổng quan dự án, mô hình kinh doanh, feature set, thiết kế animation mở pack, hệ thống kinh tế, BRD, IP & câu chuyện, roadmap, kiến trúc & tech stack, Đấu trường.
- [PRD](docs/PRD_ANIMA.md) — Product Requirements Document v0.5 (Draft): tầm nhìn, persona, North Star, phạm vi MVP, user journey, kế hoạch đo lường, kế hoạch phát hành.
- [BRD](docs/BRD_ANIMA.md) — Business Requirements Document v1.0 — baseline hợp nhất, nguồn yêu cầu duy nhất (Draft, chờ PO duyệt): mục tiêu, phạm vi theo release, quy trình TO-BE, business rules, kịch bản BDD, phân quyền, dữ liệu, NFR, mâu thuẫn và câu hỏi mở.
- [BDD](docs/BDD_ANIMA.md) — Đặc tả hành vi v0.5 (Draft): 223 kịch bản và 50 sơ đồ kịch bản phủ mọi Business Rule, kèm traceability.
- [Tech Stack](docs/TECH_STACK.md) — Tech stack v0.6: đã chốt Unity 6 và ASP.NET Core (.NET 10); PostgreSQL, Redis, Firebase; cloud chưa chốt; kiến trúc, tích hợp, CI/CD, ánh xạ NFR.
- [Solution Design](docs/SOLUTION_DESIGN.md) — v0.4: kiến trúc, cấu trúc repo, module backend, engine Đấu trường, mô hình dữ liệu, API, luồng xử lý, bảo mật, Git contract, chiến lược kiểm thử.
- [Sprint Plan](docs/SPRINT_PLAN.md) — 12 sprint × 2 tuần, 132 task, DoR/DoD, đường găng; dữ liệu gốc ở `.vibe/backlog.json`.

## Chạy thử (backend + website)

```bash
infra/dev-postgres.sh start                        # PostgreSQL cục bộ (không cần Docker)
dotnet run --project backend/src/Anima.Api         # API: http://localhost:5080 (tự migrate + seed 100 thẻ tạm)
pnpm -C web install && pnpm -C web dev             # Website người chơi: http://localhost:3000
web/apps/player/e2e/run.sh                         # hoặc chạy cả bộ E2E trên trình duyệt thật
```

Chi tiết: [backend/README.md](backend/README.md), [web/apps/player/README.md](web/apps/player/README.md). Website hiện chơi được vòng R1: đăng ký, gói chào mừng, mua và mở pack, bộ sưu tập, Lò rèn, ví, kiểm chứng công bằng, 4 ngôn ngữ.

## Prototype giao diện

Mở `prototype/index.html` trong trình duyệt: app mobile, website người chơi (cùng file `player.html`, đổi chế độ ở thanh trên cùng) và website admin, cùng `battle.html` — một trận Đấu trường mẫu với máy (CR-004). Giao diện sáng, phẳng; chuyển được 4 ngôn ngữ (vi, en, zh-Hans, zh-Hant). Dữ liệu giả, không cần backend.

## Kế hoạch triển khai

- Trạng thái và gate: `.vibe/checkpoint.json`
- Ngữ cảnh cho người/agent nhận task: `.vibe/project-context.md`
- Kiểm tra và sinh lại bảng task: `python3 tools/render_sprint_plan.py`
