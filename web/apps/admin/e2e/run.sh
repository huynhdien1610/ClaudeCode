#!/usr/bin/env bash
# E2E admin trọn bộ: PostgreSQL cục bộ → backend (DB mới, có tài khoản admin demo) → admin web (bản build) → Playwright → dọn dẹp.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/../../../.." && pwd)
DB=anima_e2e_admin
LOG=${API_LOG:-/tmp/anima-api-admin.log}
CS=$("$ROOT/infra/dev-postgres.sh" start)
PSQL=$(echo "$CS" | sed 's/Host=/host=/;s/;Port=/ port=/;s/;Username=/ user=/;s/;Database=postgres//')
psql "$PSQL" -qc "DROP DATABASE IF EXISTS $DB WITH (FORCE)" -c "CREATE DATABASE $DB" 2>&1 | grep -v NOTICE || true

pids=()
cleanup() { for p in "${pids[@]:-}"; do kill "$p" 2>/dev/null || true; done; }
trap cleanup EXIT

dotnet build "$ROOT/backend/src/Anima.Api" -v q 2>&1 | grep -E "error|rror\(s\)" || true
(cd "$ROOT/backend/src/Anima.Api" && \
  ConnectionStrings__Anima="${CS/Database=postgres/Database=$DB}" Identity__Pbkdf2Iterations=1000 Admin__Pbkdf2Iterations=1000 Admin__SeedDemoUsers=true Seed__Enabled=true \
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5081 exec dotnet bin/Debug/net10.0/Anima.Api.dll > "$LOG" 2>&1) &
pids+=($!)
for _ in $(seq 1 60); do curl -sf http://127.0.0.1:5081/healthz >/dev/null && break; sleep 1; done

cd "$ROOT/web/apps/admin"
pnpm build > /tmp/anima-admin-build.log 2>&1
(API_URL=http://127.0.0.1:5081 exec pnpm preview > /tmp/anima-admin-web.log 2>&1) &
pids+=($!)
for _ in $(seq 1 30); do curl -sf http://localhost:3100/ >/dev/null && break; sleep 1; done

NODE_PATH="${NODE_PATH:-$(npm root -g)}" BASE_URL=http://localhost:3100 API=http://127.0.0.1:5081 node e2e/admin.mjs
