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
for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31 32 33 34 35 36 37 38 39 40 41 42 43 44 45 46 47 48 49 50 51 52 53 54 55 56 57 58 59 60; do
  code=$(curl -s $AUTH -o /dev/null -w "%{http_code}" "$KIBANA_URL/api/status" || true)
  code="${code:-000}"
  if [ "$code" = "200" ]; then
    ready=1
    break
  fi
  echo "Waiting for Kibana (status $code)..."
  sleep 3
done

if [ "$ready" -ne 1 ]; then
  echo "Kibana not ready. Aborting Fleet setup."
  exit 1
fi

echo "Setting up Fleet..."
fleet_ready=0
for i in 1 2 3 4 5 6 7 8 9 10; do
  code=$(curl -s -o /dev/null -w "%{http_code}" \
    $AUTH \
    -X POST "$KIBANA_URL/api/fleet/setup" \
    -H "kbn-xsrf: true" \
    -H "Content-Type: application/json" \
    -d "{}" || true)
  code="${code:-000}"
  if [ "$code" = "200" ] || [ "$code" = "204" ]; then
    fleet_ready=1
    break
  fi
  sleep 3
done

if [ "$fleet_ready" -ne 1 ]; then
  echo "Fleet setup failed."
  exit 1
fi

echo "Installing APM package..."
apm_ready=0
for i in 1 2 3 4 5 6 7 8 9 10; do
  code=$(curl -s -o /dev/null -w "%{http_code}" \
    $AUTH \
    -X POST "$KIBANA_URL/api/fleet/epm/packages/apm/$APM_VERSION" \
    -H "kbn-xsrf: true" \
    -H "Content-Type: application/json" \
    -d "{\"force\":true}" || true)
  code="${code:-000}"
  if [ "$code" = "200" ] || [ "$code" = "204" ] || [ "$code" = "409" ]; then
    apm_ready=1
    break
  fi
  sleep 3
done

if [ "$apm_ready" -ne 1 ]; then
  echo "APM package install failed."
  exit 1
fi

echo "Fleet setup and APM package installed."
