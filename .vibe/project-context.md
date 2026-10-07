# Project context — ANIMA

> Đọc file này trước khi nhận task. Nguồn task: `.vibe/backlog.json`. Trạng thái: `.vibe/checkpoint.json`.

## Sản phẩm

- Nền tảng sưu tầm thẻ bài số: app mobile (iOS/Android), website người chơi, website admin.
- Tài liệu: `docs/PRD_ANIMA.md`, `docs/BRD_ANIMA.md`, `docs/BDD_ANIMA.md`, `docs/SOLUTION_DESIGN.md`, `docs/TECH_STACK.md`, `docs/SPRINT_PLAN.md`.
- Prototype giao diện tham khảo: `prototype/index.html` (chưa phải design handoff).

## Stack fingerprint

| Thành phần | Công nghệ | Thư mục |
|---|---|---|
| Backend | .NET 10 LTS, ASP.NET Core minimal API, EF Core + Npgsql, xUnit, Reqnroll, Testcontainers | `backend/` |
| Contract | OpenAPI 3.1 `contracts/openapi/anima.v1.yaml`; `Anima.Contracts` netstandard2.1 dùng chung với Unity | `contracts/`, `backend/src/Anima.Contracts/` |
| App mobile | Unity 6 LTS, URP, UI Toolkit, Addressables | `mobile/AnimaUnity/` |
| Web người chơi | Next.js (App Router) + TypeScript, Unity Web nhúng cho PackOpening | `web/apps/player/` |
| Web admin | Vite + React + Refine + Ant Design | `web/apps/admin/` |
| Frontend workspace | pnpm + Turborepo | `web/` |
| Dữ liệu | PostgreSQL 17, Redis | `infra/docker-compose.yml` |
| Cloud | **Chưa chốt (T-02)** — không viết code phụ thuộc dịch vụ riêng của cloud | `infra/` |

## Commands

> Trạng thái: **backend đã kiểm chứng** (2026-10-07: build, test, format chạy được; `infra/dev-postgres.sh` thay cho Docker khi không có Docker). Lệnh web và Unity vẫn dự kiến.

| Mục đích | Lệnh |
|---|---|
| Hạ tầng local | `docker compose -f infra/docker-compose.yml up -d` |
| Build backend | `dotnet build backend/Anima.sln` |
| Test backend | `dotnet test backend/Anima.sln` |
| Format backend | `dotnet format backend/Anima.sln --verify-no-changes` |
| Cài web | `pnpm -C web install` |
| Build/test/lint web | `pnpm -C web turbo run build test lint` |
| Sinh API client | `pnpm -C web --filter api-client generate` |
| Unity test | GameCI `unity-test-runner` (EditMode + PlayMode) qua `.github/workflows/unity.yml` |
| Kiểm tra kế hoạch | `python3 tools/render_sprint_plan.py` |

## Git contract

- Trunk-based; `main` luôn deploy được.
- Nhánh: `feat/T###-mo-ta`, `fix/T###-...`, `chore/T###-...`.
- Commit: Conventional Commits, có `T###` trong message.
- PR bắt buộc review; `risk_tier=high` cần 2 reviewer, 1 người không viết code đó.
- CI bắt buộc: build, test, lint/format, ArchTests, kiểm tra OpenAPI breaking change, gitleaks.
- Squash merge.

## Quy tắc cho task

- Chỉ sửa file trong `allowed_files` của task.
- Mã lỗi nghiệp vụ lấy đúng tên trong `docs/BDD_ANIMA.md`.
- Không ghi PII vào log. Tiền dùng `Money` (long), không dùng số thực.
- Client không quyết định số dư, kết quả pack hay phần thưởng.
