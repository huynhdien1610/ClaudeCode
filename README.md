# ANIMA: Echoes of the Heart

Nền tảng sưu tầm thẻ bài số (app mobile + website người chơi + website admin) với IP gốc dựa trên hệ thống 8 hệ cảm xúc.

**Trạng thái:** Concept & Design Phase

## Tài liệu

- [Master Document](docs/ANIMA_Master_Document.md) — v1.1: tổng quan dự án, mô hình kinh doanh, feature set, thiết kế animation mở pack, hệ thống kinh tế, BRD, IP & câu chuyện, roadmap, kiến trúc & tech stack.
- [PRD](docs/PRD_ANIMA.md) — Product Requirements Document v0.2 (Draft): tầm nhìn, persona, North Star, phạm vi MVP, user journey, kế hoạch đo lường, kế hoạch phát hành.
- [BRD](docs/BRD_ANIMA.md) — Business Requirements Document v0.3 (Draft): mục tiêu, phạm vi theo release, quy trình TO-BE, business rules, kịch bản BDD, phân quyền, dữ liệu, NFR, mâu thuẫn và câu hỏi mở.
- [BDD](docs/BDD_ANIMA.md) — Đặc tả hành vi v0.2 (Draft): 150 kịch bản và 22 sơ đồ kịch bản phủ mọi Business Rule, kèm traceability.
- [Tech Stack](docs/TECH_STACK.md) — Tech stack v0.3: đã chốt Unity 6 và ASP.NET Core (.NET 10); PostgreSQL, Redis, Firebase; cloud chưa chốt; kiến trúc, tích hợp, CI/CD, ánh xạ NFR.
- [Solution Design](docs/SOLUTION_DESIGN.md) — Kiến trúc, cấu trúc repo, module backend, mô hình dữ liệu, API, luồng xử lý, bảo mật, Git contract, chiến lược kiểm thử.
- [Sprint Plan](docs/SPRINT_PLAN.md) — 12 sprint × 2 tuần, 111 task, DoR/DoD, đường găng; dữ liệu gốc ở `.vibe/backlog.json`.

## Prototype giao diện

Mở `prototype/index.html` trong trình duyệt: app mobile, website người chơi (cùng file `player.html`, đổi chế độ ở thanh trên cùng) và website admin. Giao diện sáng, phẳng. Dữ liệu giả, không cần backend.

## Kế hoạch triển khai

- Trạng thái và gate: `.vibe/checkpoint.json`
- Ngữ cảnh cho người/agent nhận task: `.vibe/project-context.md`
- Kiểm tra và sinh lại bảng task: `python3 tools/render_sprint_plan.py`
