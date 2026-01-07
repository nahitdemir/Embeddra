#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE=(docker compose -f "${ROOT_DIR}/infra/docker-compose.yml" --profile apps)

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/start-all.sh          # start infra + backend services
  ./scripts/start-all.sh --fresh  # reset volumes and start clean
  ./scripts/start-all.sh --stop   # stop all services
USAGE
}

wait_for_http() {
  local url="$1"
  local name="$2"
  local timeout="${3:-60}"
  local expected="${4:-.*}"
  local waited=0
  local code=""

  while [ "${waited}" -lt "${timeout}" ]; do
    code="$(curl_http_code "${url}")"
    if [ "${code}" != "000" ] && [[ "${code}" =~ ^(${expected})$ ]]; then
      echo "[OK] ${name} reachable (${code})"
      return 0
    fi
    sleep 2
    waited=$((waited + 2))
  done

  echo "[WARN] ${name} not ready yet (${url})"
  return 1
}

curl_http_code() {
  local url="$1"
  local code=""

  code="$(curl -s --http1.1 --ipv4 -o /dev/null -w "%{http_code}" "${url}" 2>/dev/null || true)"
  if [ "${code}" != "000" ]; then
    echo "${code}"
    return 0
  fi

  code="$(curl -s --http1.1 --ipv6 -o /dev/null -w "%{http_code}" "${url}" 2>/dev/null || true)"
  echo "${code}"
}

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker not found. Please install Docker Desktop first."
  exit 1
fi

case "${1:-}" in
  --help|-h)
    usage
    exit 0
    ;;
  --stop)
    "${COMPOSE[@]}" down
    echo "All services stopped."
    exit 0
    ;;
  --fresh)
    "${COMPOSE[@]}" down -v
    ;;
  "")
    ;;
  *)
    usage
    exit 1
    ;;
esac

echo "Starting Embeddra stack (infra + backend services)..."
${COMPOSE[@]} up -d

echo "Waiting for core services..."
failed=0
if ! wait_for_http "http://localhost:9200" "Elasticsearch" 60 "200|401"; then failed=1; fi
if ! wait_for_http "http://localhost:5601" "Kibana" 90 "200|302"; then failed=1; fi
if ! wait_for_http "http://localhost:8200" "APM Server" 60 "200"; then failed=1; fi
if ! wait_for_http "http://localhost:5114/health" "Admin API" 90 "200"; then failed=1; fi
if ! wait_for_http "http://localhost:5222/health" "Search API" 90 "200"; then failed=1; fi
if ! wait_for_http "http://localhost:5310/health" "Worker" 90 "200"; then failed=1; fi

cat <<'INFO'

Tarayici/terminal URL listesi:
- Status ekrani: http://localhost:5310
- Admin API: http://localhost:5114
- Admin Swagger: http://localhost:5114/swagger
- Admin Health: http://localhost:5114/health
- Search API: http://localhost:5222
- Search Swagger: http://localhost:5222/swagger
- Search Health: http://localhost:5222/health
- Worker Health: http://localhost:5310/health
- Kibana: http://localhost:5601 (login: elastic / embeddra)
- Kibana APM: http://localhost:5601/app/apm/services
- Kibana Logs: http://localhost:5601/app/logs/stream
- Elasticsearch: http://localhost:9200 (basic auth: elastic / embeddra)
- APM Server: http://localhost:8200
- RabbitMQ: http://localhost:15672 (embeddra / embeddra)
- Postgres: localhost:5433 (db: embeddra, user: embeddra, pass: embeddra)
- Redis: localhost:6379

INFO

if [ "${failed}" -ne 0 ]; then
  echo "Not: Bazi servisler henuz hazir olmayabilir. Biraz bekleyip tekrar deneyin."
  echo "Kontrol icin: docker compose -f infra/docker-compose.yml --profile apps logs -f --tail=100"
fi
