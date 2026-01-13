#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE=(docker compose -f "${ROOT_DIR}/infra/docker-compose.yml" --profile apps)
ADMIN_UI_DIR="${ROOT_DIR}/apps/admin-ui"
ADMIN_UI_PORT=3000
ADMIN_UI_PID="/tmp/embeddra-admin-ui.pid"
ADMIN_UI_LOG="/tmp/embeddra-admin-ui.log"
START_UI=1
OPEN_BROWSER=1
RESET_VOLUMES=0
STOP_SERVICES=0
CLEAN_BUILD=0
RESTART_SERVICES=0
RESET_UI=0

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/start-all.sh           # start infra + backend services
  ./scripts/start-all.sh --restart  # restart all services (quick)
  ./scripts/start-all.sh --reset-ui # reset only Admin UI (node_modules, .next)
  ./scripts/start-all.sh --fresh    # reset volumes and start clean
  ./scripts/start-all.sh --clean    # clean build FE/BE, reset volumes, restart everything
  ./scripts/start-all.sh --stop     # stop all services
  ./scripts/start-all.sh --no-ui    # skip admin ui
  ./scripts/start-all.sh --no-open  # skip opening browser tabs

Examples:
  # Quick restart (when services are running but need refresh)
  ./scripts/start-all.sh --restart

  # UI changed, need to reset frontend only
  ./scripts/start-all.sh --reset-ui

  # Everything broken, full clean restart
  ./scripts/start-all.sh --clean
USAGE
}

wait_for_http() {
  local url="$1"
  local name="$2"
  local timeout="${3:-60}"
  local expected="${4:-.*}"
  local waited=0
  local code=""
  local interval=1
  local max_interval=3

  echo -n "  Waiting for ${name}..."
  
  while [ "${waited}" -lt "${timeout}" ]; do
    code="$(curl_http_code "${url}")"
    if [ "${code}" != "000" ]; then
      if echo "${code}" | grep -qE "^(${expected})$"; then
        echo " ✓ (${code})"
        return 0
      fi
    fi
    
    # Show progress dots
    if [ $((waited % 5)) -eq 0 ]; then
      echo -n "."
    fi
    
    # Adaptive interval: faster at start, slower later
    if [ "${waited}" -lt 10 ]; then
      interval=1
    elif [ "${waited}" -lt 30 ]; then
      interval=2
    else
      interval="${max_interval}"
    fi
    
    sleep "${interval}"
    waited=$((waited + interval))
  done

  echo " ✗ (timeout after ${timeout}s, last code: ${code})"
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

port_in_use() {
  local port="$1"
  if command -v lsof >/dev/null 2>&1; then
    lsof -iTCP:"${port}" -sTCP:LISTEN >/dev/null 2>&1
    return $?
  fi

  if command -v nc >/dev/null 2>&1; then
    nc -z localhost "${port}" >/dev/null 2>&1
    return $?
  fi

  python3 - <<PY >/dev/null 2>&1
import socket
sock = socket.socket()
try:
    sock.settimeout(0.5)
    sock.connect(("127.0.0.1", int("${port}")))
    print("open")
except Exception:
    raise SystemExit(1)
finally:
    sock.close()
PY
}

wait_for_tcp() {
  local host="$1"
  local port="$2"
  local name="$3"
  local timeout="${4:-60}"
  local waited=0
  local interval=1
  local max_interval=3

  echo -n "  Waiting for ${name}..."
  
  while [ "${waited}" -lt "${timeout}" ]; do
    if command -v nc >/dev/null 2>&1; then
      if nc -z "${host}" "${port}" >/dev/null 2>&1; then
        echo " ✓ (TCP port open)"
        return 0
      fi
    elif command -v timeout >/dev/null 2>&1 && command -v bash >/dev/null 2>&1; then
      if timeout 1 bash -c "echo > /dev/tcp/${host}/${port}" >/dev/null 2>&1; then
        echo " ✓ (TCP port open)"
        return 0
      fi
    else
      # Fallback: try with Python
      if python3 - <<PY >/dev/null 2>&1
import socket
sock = socket.socket()
try:
    sock.settimeout(1)
    sock.connect(("${host}", int("${port}")))
    print("open")
except Exception:
    raise SystemExit(1)
finally:
    sock.close()
PY
      then
        echo " ✓ (TCP port open)"
        return 0
      fi
    fi
    
    # Show progress dots
    if [ $((waited % 5)) -eq 0 ]; then
      echo -n "."
    fi
    
    # Adaptive interval: faster at start, slower later
    if [ "${waited}" -lt 10 ]; then
      interval=1
    elif [ "${waited}" -lt 30 ]; then
      interval=2
    else
      interval="${max_interval}"
    fi
    
    sleep "${interval}"
    waited=$((waited + interval))
  done

  echo " ✗ (timeout after ${timeout}s)"
  return 1
}

stop_admin_ui() {
  if [ -f "${ADMIN_UI_PID}" ]; then
    pid="$(cat "${ADMIN_UI_PID}")"
    if ps -p "${pid}" >/dev/null 2>&1; then
      echo "Stopping Admin UI (PID: ${pid})..."
      kill "${pid}" >/dev/null 2>&1 || true
      sleep 1
      # Force kill if still running
      if ps -p "${pid}" >/dev/null 2>&1; then
        kill -9 "${pid}" >/dev/null 2>&1 || true
      fi
    fi
    rm -f "${ADMIN_UI_PID}"
  fi
  
  # Also kill any processes using the port
  if port_in_use "${ADMIN_UI_PORT}"; then
    echo "Killing processes on port ${ADMIN_UI_PORT}..."
    if command -v lsof >/dev/null 2>&1; then
      lsof -tiTCP:"${ADMIN_UI_PORT}" | xargs kill -9 >/dev/null 2>&1 || true
    fi
  fi
}

stop_dotnet_services() {
  echo "Stopping .NET services..."
  # Kill any dotnet processes related to our services
  pkill -f "Embeddra.Admin.WebApi" >/dev/null 2>&1 || true
  pkill -f "Embeddra.Search.WebApi" >/dev/null 2>&1 || true
  pkill -f "Embeddra.Worker.Host" >/dev/null 2>&1 || true
  sleep 1
}

clean_frontend() {
  echo "Cleaning Frontend (Admin UI)..."
  stop_admin_ui
  
  if [ -d "${ADMIN_UI_DIR}" ]; then
    cd "${ADMIN_UI_DIR}"
    echo "  - Removing node_modules..."
    rm -rf node_modules .next
    echo "  - Installing dependencies..."
    npm install
    echo "  - Frontend cleaned and ready"
  fi
}

clean_backend() {
  echo "Cleaning Backend (.NET services)..."
  stop_dotnet_services
  
  echo "  - Running dotnet clean..."
  cd "${ROOT_DIR}"
  dotnet clean Embeddra.sln --verbosity quiet || true
  
  echo "  - Running dotnet restore..."
  dotnet restore Embeddra.sln --verbosity quiet || true
  
  echo "  - Running dotnet build..."
  dotnet build Embeddra.sln --no-restore --verbosity quiet || true
  
  echo "  - Backend cleaned and ready"
}

clean_all() {
  echo "=========================================="
  echo "CLEANING ALL SERVICES (FE + BE)"
  echo "=========================================="
  
  # Stop all services first
  echo "Stopping all services..."
  "${COMPOSE[@]}" down -v 2>/dev/null || true
  stop_admin_ui
  stop_dotnet_services
  
  # Clean frontend
  clean_frontend
  
  # Clean backend
  clean_backend
  
  echo "=========================================="
  echo "CLEAN COMPLETE - Ready to start"
  echo "=========================================="
}

start_admin_ui() {
  if [ "${START_UI}" -ne 1 ]; then
    return
  fi

  if ! command -v npm >/dev/null 2>&1; then
    echo "[WARN] npm not found. Admin UI not started."
    return
  fi

  # Stop any existing instance
  stop_admin_ui

  if [ ! -d "${ADMIN_UI_DIR}/node_modules" ]; then
    echo "Installing admin-ui dependencies..."
    (cd "${ADMIN_UI_DIR}" && npm install)
  fi

  echo "Starting Admin UI..."
  (cd "${ADMIN_UI_DIR}" && nohup npm run dev -- --hostname 0.0.0.0 --port "${ADMIN_UI_PORT}" \
    > "${ADMIN_UI_LOG}" 2>&1 & echo $! > "${ADMIN_UI_PID}")

  echo ""
  wait_for_http "http://localhost:${ADMIN_UI_PORT}" "Admin UI" 60 "200|301|302|304" || true
}

open_url() {
  local url="$1"
  if [ "${OPEN_BROWSER}" -ne 1 ]; then
    return
  fi

  if command -v open >/dev/null 2>&1; then
    open "${url}" >/dev/null 2>&1 || true
    return
  fi

  if command -v xdg-open >/dev/null 2>&1; then
    xdg-open "${url}" >/dev/null 2>&1 || true
    return
  fi

  if command -v cmd.exe >/dev/null 2>&1; then
    cmd.exe /c start "${url}" >/dev/null 2>&1 || true
    return
  fi

  echo "Open in browser: ${url}"
}

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker not found. Please install Docker Desktop first."
  exit 1
fi

while [ $# -gt 0 ]; do
  case "$1" in
    --help|-h)
      usage
      exit 0
      ;;
    --stop)
      STOP_SERVICES=1
      shift
      ;;
    --restart)
      RESTART_SERVICES=1
      shift
      ;;
    --reset-ui)
      RESET_UI=1
      shift
      ;;
    --fresh)
      RESET_VOLUMES=1
      shift
      ;;
    --clean)
      CLEAN_BUILD=1
      RESET_VOLUMES=1
      shift
      ;;
    --no-ui)
      START_UI=0
      shift
      ;;
    --no-open)
      OPEN_BROWSER=0
      shift
      ;;
    *)
      usage
      exit 1
      ;;
  esac
done

if [ "${STOP_SERVICES}" -eq 1 ]; then
  echo "Stopping all services..."
  "${COMPOSE[@]}" down
  stop_admin_ui
  stop_dotnet_services
  echo "All services stopped."
  exit 0
fi

if [ "${RESTART_SERVICES}" -eq 1 ]; then
  echo "=========================================="
  echo "RESTARTING ALL SERVICES (QUICK)"
  echo "=========================================="
  echo "Restarting Docker services..."
  "${COMPOSE[@]}" restart
  stop_admin_ui
  
  echo ""
  echo "Waiting for infrastructure services..."
  echo ""
  
  failed=0
  if ! wait_for_http "http://localhost:9200" "Elasticsearch" 120 "200|401"; then 
    failed=1
    echo "    ⚠ Elasticsearch may still be starting. This is usually OK for quick restarts."
  fi
  if ! wait_for_http "http://localhost:8200" "APM Server" 60 "200"; then failed=1; fi
  if ! wait_for_http "http://localhost:5601" "Kibana" 120 "200|302"; then failed=1; fi
  
  echo ""
  echo "Waiting for backend services..."
  echo ""
  
  if ! wait_for_http "http://localhost:5114/health" "Admin API" 120 "200"; then failed=1; fi
  if ! wait_for_http "http://localhost:5222/health" "Search API" 120 "200"; then failed=1; fi
  if ! wait_for_http "http://localhost:5310/health" "Worker" 120 "200"; then failed=1; fi
  
  start_admin_ui
  
  # Show URLs and exit (skip the normal startup flow)
  cat <<'INFO'

==========================================
🌐 BROWSER URL'LERİ - TARAYICIDA KONTROL ET
==========================================

📱 FRONTEND (Admin UI)
   • Ana Sayfa:        http://localhost:3000
   • Control Center:   http://localhost:3000/
   • Platform Login:   http://localhost:3000/platform/login
   • Tenant Login:     http://localhost:3000/tenant/login

🔧 BACKEND APIs
   • Admin API:        http://localhost:5114
   • Admin Swagger:    http://localhost:5114/swagger
   • Admin Health:     http://localhost:5114/health
   • Search API:       http://localhost:5222
   • Search Swagger:   http://localhost:5222/swagger
   • Search Health:    http://localhost:5222/health
   • Worker Health:    http://localhost:5310/health

📊 OBSERVABILITY (Elastic Stack)
   • Kibana:           http://localhost:5601
     Login: elastic / embeddra
   • Kibana APM:       http://localhost:5601/app/apm/services
   • Kibana Logs:      http://localhost:5601/app/logs/stream
   • Elasticsearch:    http://localhost:9200
     Auth: elastic / embeddra
   • APM Server:       http://localhost:8200

🗄️  INFRASTRUCTURE
   • RabbitMQ:         http://localhost:15672
     Login: embeddra / embeddra
   • Postgres:         localhost:5433
     DB: embeddra | User: embeddra | Pass: embeddra
   • Redis:           localhost:6379

==========================================

INFO

  if [ "${failed}" -ne 0 ]; then
    echo "⚠ Not: Bazı servisler henüz hazır olmayabilir. Biraz bekleyip tekrar deneyin."
  fi

  if [ "${START_UI}" -eq 1 ] && port_in_use "${ADMIN_UI_PORT}"; then
    echo "🌐 Opening browser tabs..."
    open_url "http://localhost:${ADMIN_UI_PORT}"
    sleep 1
    open_url "http://localhost:${ADMIN_UI_PORT}/platform/login"
  fi
  
  exit 0
elif [ "${RESET_UI}" -eq 1 ]; then
  echo "=========================================="
  echo "RESETTING ADMIN UI ONLY"
  echo "=========================================="
  stop_admin_ui
  if [ -d "${ADMIN_UI_DIR}" ]; then
    cd "${ADMIN_UI_DIR}"
    echo "Removing node_modules and .next..."
    rm -rf node_modules .next
    echo "Installing dependencies..."
    npm install
    echo "UI reset complete. Starting..."
  fi
  start_admin_ui
  echo ""
  echo "Admin UI reset and restarted."
  exit 0
elif [ "${CLEAN_BUILD}" -eq 1 ]; then
  clean_all
elif [ "${RESET_VOLUMES}" -eq 1 ]; then
  echo "Stopping services and removing volumes..."
  "${COMPOSE[@]}" down -v
  stop_admin_ui
  stop_dotnet_services
fi

echo "Starting Embeddra stack (infra + backend services)..."
echo "  Using health checks for faster startup detection..."
${COMPOSE[@]} up -d

echo ""
echo "Waiting for infrastructure services (with health checks)..."
echo ""

failed=0

# Wait for infrastructure services - health checks should make this faster
# Elasticsearch - base service, should be ready first (can take longer on restart)
if ! wait_for_http "http://localhost:9200" "Elasticsearch" 120 "200|401"; then 
  failed=1
  echo "    ⚠ Elasticsearch may still be starting. This is usually OK - it will be ready soon."
fi

# Postgres - use TCP port check instead of HTTP
if ! wait_for_tcp "localhost" "5433" "Postgres" 30; then 
  failed=1
fi

# APM Server - depends on Elasticsearch
if ! wait_for_http "http://localhost:8200" "APM Server" 60 "200"; then 
  failed=1
fi

# Kibana - depends on Elasticsearch, can take longer
if ! wait_for_http "http://localhost:5601" "Kibana" 120 "200|302"; then 
  failed=1
fi

# Backend services (depend on infrastructure, now with health checks)
echo ""
echo "Waiting for backend services (with health checks)..."
echo ""

# Backend services - increased timeout for first build
if ! wait_for_http "http://localhost:5114/health" "Admin API" 120 "200"; then 
  failed=1
  echo "    ⚠ Admin API may still be building. Check logs: docker compose -f infra/docker-compose.yml --profile apps logs admin-api"
fi

if ! wait_for_http "http://localhost:5222/health" "Search API" 120 "200"; then 
  failed=1
  echo "    ⚠ Search API may still be building. Check logs: docker compose -f infra/docker-compose.yml --profile apps logs search-api"
fi

if ! wait_for_http "http://localhost:5310/health" "Worker" 120 "200"; then 
  failed=1
  echo "    ⚠ Worker may still be building. Check logs: docker compose -f infra/docker-compose.yml --profile apps logs worker"
fi

start_admin_ui

echo ""
echo "=========================================="
echo "🌐 BROWSER URL'LERİ - TARAYICIDA KONTROL ET"
echo "=========================================="
echo ""
echo "📱 FRONTEND (Admin UI)"
echo "   • Ana Sayfa:        http://localhost:3000"
echo "   • Control Center:   http://localhost:3000/"
echo "   • Platform Login:    http://localhost:3000/platform/login"
echo "   • Tenant Login:     http://localhost:3000/tenant/login"
echo ""
echo "🔧 BACKEND APIs"
echo "   • Admin API:        http://localhost:5114"
echo "   • Admin Swagger:    http://localhost:5114/swagger"
echo "   • Admin Health:     http://localhost:5114/health"
echo "   • Search API:       http://localhost:5222"
echo "   • Search Swagger:   http://localhost:5222/swagger"
echo "   • Search Health:    http://localhost:5222/health"
echo "   • Worker Health:    http://localhost:5310/health"
echo ""
echo "📊 OBSERVABILITY (Elastic Stack)"
echo "   • Kibana:           http://localhost:5601"
echo "     Login: elastic / embeddra"
echo "   • Kibana APM:       http://localhost:5601/app/apm/services"
echo "   • Kibana Logs:      http://localhost:5601/app/logs/stream"
echo "   • Elasticsearch:    http://localhost:9200"
echo "     Auth: elastic / embeddra"
echo "   • APM Server:       http://localhost:8200"
echo ""
echo "🗄️  INFRASTRUCTURE"
echo "   • RabbitMQ:         http://localhost:15672"
echo "     Login: embeddra / embeddra"
echo "   • Postgres:         localhost:5433"
echo "     DB: embeddra | User: embeddra | Pass: embeddra"
echo "   • Redis:           localhost:6379"
echo ""
echo "=========================================="
echo ""

if [ "${failed}" -ne 0 ]; then
  echo "Not: Bazi servisler henuz hazir olmayabilir. Biraz bekleyip tekrar deneyin."
  echo "Kontrol icin: docker compose -f infra/docker-compose.yml --profile apps logs -f --tail=100"
fi

if [ "${START_UI}" -eq 1 ] && port_in_use "${ADMIN_UI_PORT}"; then
  echo "🌐 Opening browser tabs..."
  open_url "http://localhost:${ADMIN_UI_PORT}"
  sleep 1
  open_url "http://localhost:${ADMIN_UI_PORT}/platform/login"
fi
