#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/run-e2e.sh [--fresh]

Options:
  --fresh    Reset docker volumes before starting services
USAGE
}

case "${1:-}" in
  --help|-h)
    usage
    exit 0
    ;;
  --fresh)
    ./scripts/start-all.sh --fresh --no-ui --no-open
    ;;
  "")
    ./scripts/start-all.sh --no-ui --no-open
    ;;
  *)
    usage
    exit 1
    ;;
esac

./scripts/e2e.sh
