#!/usr/bin/env bash
# Chạy E2E trọn bộ từ máy sạch: PostgreSQL cục bộ → backend (database mới) → website (bản build) → Playwright → dọn dẹp.
# Cần: dotnet 10, pnpm, playwright (npm i -g playwright hoặc cài cục bộ) và Chromium.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/../../../.." && pwd)
DB=anima_e2e
LOG=${API_LOG:-/tmp/anima-api.log}
CS=$("$ROOT/infra/dev-postgres.sh" start)
PSQL="psql ${CS//;/ }"; PSQL=$(echo "$CS" | sed 's/Host=/host=/;s/;Port=/ port=/;s/;Username=/ user=/;s/;Database=postgres//')
psql "$PSQL" -qc "DROP DATABASE IF EXISTS $DB WITH (FORCE)" -c "CREATE DATABASE $DB" 2>&1 | grep -v NOTICE || true

pids=()
cleanup() { for p in "${pids[@]:-}"; do kill "$p" 2>/dev/null || true; done; }
trap cleanup EXIT

dotnet build "$ROOT/backend/src/Anima.Api" -v q 2>&1 | grep -E "error|rror\(s\)" || true
(cd "$ROOT/backend/src/Anima.Api" && \
  ConnectionStrings__Anima="${CS/Database=postgres/Database=$DB}" Identity__Pbkdf2Iterations=1000 Identity__DevLogOtp=true Dev__MockTopUp=true Seed__Enabled=true \
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5080 exec dotnet bin/Debug/net10.0/Anima.Api.dll > "$LOG" 2>&1) &
pids+=($!)
for _ in $(seq 1 60); do curl -sf http://127.0.0.1:5080/healthz >/dev/null && break; sleep 1; done

cd "$ROOT/web/apps/player"
NEXT_PUBLIC_DEV_TOPUP=1 pnpm build > /tmp/anima-web-build.log 2>&1
(exec pnpm start > /tmp/anima-web.log 2>&1) &
pids+=($!)
for _ in $(seq 1 60); do curl -sf http://localhost:3000/login >/dev/null && break; sleep 1; done

NODE_PATH="${NODE_PATH:-$(npm root -g)}" BASE_URL=http://localhost:3000 API_LOG="$LOG" node e2e/play.mjs
NODE_PATH="${NODE_PATH:-$(npm root -g)}" BASE_URL=http://localhost:3000 API=http://127.0.0.1:5080 node e2e/battle.mjs
