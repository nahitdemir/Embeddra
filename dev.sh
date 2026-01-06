#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE=(docker compose -f "${ROOT_DIR}/infra/docker-compose.yml")

usage() {
  cat <<'USAGE'
Usage:
  ./dev.sh up       # start infra + Admin/Search/Worker
  ./dev.sh down     # stop infra
  ./dev.sh ps       # list infra services
  ./dev.sh logs     # follow infra logs
USAGE
}

cmd="${1:-up}"

case "${cmd}" in
  up)
    "${COMPOSE[@]}" up -d
    echo "Starting Admin, Search, Worker. Ctrl+C to stop all."
    dotnet run --project "${ROOT_DIR}/apps/Admin/Embeddra.Admin.WebApi" &
    p1=$!
    dotnet run --project "${ROOT_DIR}/apps/Search/Embeddra.Search.WebApi" &
    p2=$!
    dotnet run --project "${ROOT_DIR}/apps/Worker/Embeddra.Worker.Host" &
    p3=$!
    trap 'kill "${p1}" "${p2}" "${p3}" 2>/dev/null' INT TERM
    wait
    ;;
  down)
    "${COMPOSE[@]}" down
    ;;
  ps)
    "${COMPOSE[@]}" ps
    ;;
  logs)
    "${COMPOSE[@]}" logs -f --tail=100
    ;;
  *)
    usage
    exit 1
    ;;
esac
