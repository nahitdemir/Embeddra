#!/usr/bin/env sh
set -e

KIBANA_URL="${KIBANA_URL:-http://kibana:5601}"
APM_VERSION="${APM_VERSION:-8.12.2}"
KIBANA_USERNAME="${KIBANA_USERNAME:-}"
KIBANA_PASSWORD="${KIBANA_PASSWORD:-}"

AUTH=""
if [ -n "$KIBANA_USERNAME" ] || [ -n "$KIBANA_PASSWORD" ]; then
  AUTH="-u ${KIBANA_USERNAME:-elastic}:${KIBANA_PASSWORD}"
fi

ready=0
# Faster polling - check every 2 seconds, max 40 seconds (20 attempts)
for i in $(seq 1 20); do
  code=$(curl -s $AUTH -o /dev/null -w "%{http_code}" "$KIBANA_URL/api/status" 2>/dev/null || echo "000")
  if [ "$code" = "200" ]; then
    ready=1
    break
  fi
  if [ $((i % 5)) -eq 0 ]; then
    echo "Waiting for Kibana (status $code)..."
  fi
  sleep 2
done

if [ "$ready" -ne 1 ]; then
  echo "Kibana not ready. Aborting Fleet setup."
  exit 1
fi

echo "Setting up Fleet..."
fleet_ready=0
for i in $(seq 1 10); do
  code=$(curl -s -o /dev/null -w "%{http_code}" \
    $AUTH \
    -X POST "$KIBANA_URL/api/fleet/setup" \
    -H "kbn-xsrf: true" \
    -H "Content-Type: application/json" \
    -d "{}" 2>/dev/null || echo "000")
  if [ "$code" = "200" ] || [ "$code" = "204" ]; then
    fleet_ready=1
    break
  fi
  sleep 2
done

if [ "$fleet_ready" -ne 1 ]; then
  echo "Fleet setup failed."
  exit 1
fi

echo "Installing APM package..."
apm_ready=0
for i in $(seq 1 10); do
  code=$(curl -s -o /dev/null -w "%{http_code}" \
    $AUTH \
    -X POST "$KIBANA_URL/api/fleet/epm/packages/apm/$APM_VERSION" \
    -H "kbn-xsrf: true" \
    -H "Content-Type: application/json" \
    -d "{\"force\":true}" 2>/dev/null || echo "000")
  if [ "$code" = "200" ] || [ "$code" = "204" ] || [ "$code" = "409" ]; then
    apm_ready=1
    break
  fi
  sleep 2
done

if [ "$apm_ready" -ne 1 ]; then
  echo "APM package install failed."
  exit 1
fi

echo "Fleet setup and APM package installed."
