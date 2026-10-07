#!/usr/bin/env bash
# Chạy PostgreSQL cục bộ khi không có Docker (dùng trong sandbox / máy dev không cài Docker).
# Có Docker thì dùng: docker compose -f infra/docker-compose.yml up -d
# Dùng: infra/dev-postgres.sh start|stop|status   → in ra chuỗi kết nối.
set -euo pipefail
PGBIN=$(ls -d /usr/lib/postgresql/*/bin | sort -V | tail -1)
DATA=${ANIMA_PGDATA:-/var/tmp/anima-pgdata}
PORT=${ANIMA_PGPORT:-55432}
RUN="runuser -u postgres --"
[ "$(id -u)" = 0 ] || RUN=""
case "${1:-start}" in
  start)
    if [ ! -d "$DATA/base" ]; then
      mkdir -p "$DATA"; [ -n "$RUN" ] && chown postgres:postgres "$DATA"
      $RUN "$PGBIN/initdb" -D "$DATA" -A trust -U postgres >/dev/null
    fi
    if ! $RUN "$PGBIN/pg_ctl" -D "$DATA" status >/dev/null 2>&1; then
      $RUN "$PGBIN/pg_ctl" -D "$DATA" -o "-p $PORT -k /tmp -c fsync=off -c listen_addresses=127.0.0.1" -l "$DATA/server.log" -w start >/dev/null
    fi
    echo "Host=127.0.0.1;Port=$PORT;Username=postgres;Database=postgres"
    ;;
  stop) $RUN "$PGBIN/pg_ctl" -D "$DATA" -m fast stop ;;
  status) $RUN "$PGBIN/pg_ctl" -D "$DATA" status ;;
esac
