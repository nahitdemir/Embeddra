#!/usr/bin/env bash
set -euo pipefail

ADMIN_URL="${ADMIN_URL:-http://localhost:5114}"
SEARCH_URL="${SEARCH_URL:-http://localhost:5222}"
PLATFORM_KEY="${PLATFORM_KEY:-dev-platform-key}"
TENANT_ID="${TENANT_ID:-demo}"
TENANT_NAME="${TENANT_NAME:-Demo Store}"
TENANT_OWNER_EMAIL="${TENANT_OWNER_EMAIL:-owner@demo.com}"
TENANT_OWNER_PASSWORD="${TENANT_OWNER_PASSWORD:-password123}"
TENANT_OWNER_NAME="${TENANT_OWNER_NAME:-Demo Owner}"
COUNT="${COUNT:-40}"

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/e2e.sh

Environment overrides:
  ADMIN_URL, SEARCH_URL, PLATFORM_KEY, TENANT_ID, TENANT_NAME,
  TENANT_OWNER_EMAIL, TENANT_OWNER_PASSWORD, TENANT_OWNER_NAME, COUNT
USAGE
}

wait_for() {
  local url="$1"
  local name="$2"
  local timeout="${3:-60}"
  local waited=0
  local code=""

  while [ "${waited}" -lt "${timeout}" ]; do
    code="$(curl -s -o /dev/null -w "%{http_code}" "${url}" || true)"
    if [ "${code}" != "000" ] && [ "${code}" -ge 200 ] && [ "${code}" -lt 500 ]; then
      echo "[OK] ${name} reachable (${code})"
      return 0
    fi
    sleep 2
    waited=$((waited + 2))
  done

  echo "[WARN] ${name} not ready (${url})"
  return 1
}

json_get() {
  python3 -c 'import json,sys; data=json.load(sys.stdin); path=sys.argv[1]; current=data; 
for key in path.split("."):
  if isinstance(current, dict) and key in current:
    current = current[key]
  else:
    current = None
    break
print("" if current is None else current)' "$1"
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

echo "==> Waiting for services"
wait_for "${ADMIN_URL%/}/health" "Admin API" 90
wait_for "${SEARCH_URL%/}/health" "Search API" 90

echo "==> Ensuring tenant ${TENANT_ID}"
create_tenant_payload=$(printf '{"tenantId":"%s","name":"%s"}' "${TENANT_ID}" "${TENANT_NAME}")
tenant_status="$(curl -s -o /tmp/tenant_resp.json -w "%{http_code}" \
  -X POST "${ADMIN_URL%/}/tenants" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${PLATFORM_KEY}" \
  --data "${create_tenant_payload}")"

if [[ "${tenant_status}" != "201" && "${tenant_status}" != "409" ]]; then
  echo "Tenant create failed (${tenant_status}): $(cat /tmp/tenant_resp.json)" >&2
  exit 1
fi

if [[ "${tenant_status}" == "409" ]]; then
  echo "Tenant already exists."
else
  echo "Tenant created."
fi

echo "==> Creating tenant API key"
key_name="e2e-$(date +%s)"
key_payload=$(printf '{"name":"%s","description":"e2e seed"}' "${key_name}")
api_key_resp="$(curl -s -X POST "${ADMIN_URL%/}/api-keys" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${PLATFORM_KEY}" \
  -H "X-Tenant-Id: ${TENANT_ID}" \
  --data "${key_payload}")"

TENANT_API_KEY="$(printf '%s' "${api_key_resp}" | json_get apiKey)"
if [[ -z "${TENANT_API_KEY}" ]]; then
  echo "API key create failed: ${api_key_resp}" >&2
  exit 1
fi

echo "Tenant API key issued."

echo "==> Creating tenant owner user"
user_payload=$(printf '{"tenantId":"%s","email":"%s","name":"%s","password":"%s","role":"owner"}' \
  "${TENANT_ID}" "${TENANT_OWNER_EMAIL}" "${TENANT_OWNER_NAME}" "${TENANT_OWNER_PASSWORD}")
user_status="$(curl -s -o /tmp/user_resp.json -w "%{http_code}" \
  -X POST "${ADMIN_URL%/}/auth/users" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${PLATFORM_KEY}" \
  --data "${user_payload}")"

if [[ "${user_status}" != "201" && "${user_status}" != "409" ]]; then
  echo "User create failed (${user_status}): $(cat /tmp/user_resp.json)" >&2
  exit 1
fi

if [[ "${user_status}" == "409" ]]; then
  echo "User already exists."
else
  echo "User created."
fi

echo "==> Logging in as tenant owner"
login_payload=$(printf '{"tenantId":"%s","email":"%s","password":"%s"}' \
  "${TENANT_ID}" "${TENANT_OWNER_EMAIL}" "${TENANT_OWNER_PASSWORD}")
login_resp="$(curl -s -X POST "${ADMIN_URL%/}/auth/login" \
  -H "Content-Type: application/json" \
  --data "${login_payload}")"

AUTH_TOKEN="$(printf '%s' "${login_resp}" | json_get token)"
if [[ -z "${AUTH_TOKEN}" ]]; then
  echo "Login failed: ${login_resp}" >&2
  exit 1
fi

echo "Login OK."

echo "==> Seeding data"
seed_resp="$(API_KEY="${TENANT_API_KEY}" TENANT_ID="${TENANT_ID}" COUNT="${COUNT}" ADMIN_URL="${ADMIN_URL}" ./scripts/seed-data.sh)"
JOB_ID="$(printf '%s' "${seed_resp}" | json_get job_id)"
if [[ -z "${JOB_ID}" ]]; then
  echo "Seed failed: ${seed_resp}" >&2
  exit 1
fi

echo "Seed job ${JOB_ID} queued."

echo "==> Waiting for ingestion job to complete"
job_status=""
for _ in $(seq 1 60); do
  job_resp="$(curl -s "${ADMIN_URL%/}/ingestion-jobs?limit=10" \
    -H "X-Api-Key: ${TENANT_API_KEY}" \
    -H "X-Tenant-Id: ${TENANT_ID}")"
  job_status="$(printf '%s' "${job_resp}" | python3 -c 'import json,sys; job_id=sys.argv[1]; data=json.load(sys.stdin); jobs=data.get("jobs") or []; status=""; 
for job in jobs:
  if job.get("id") == job_id:
    status = job.get("status") or ""
    break
print(status)' "${JOB_ID}")"
  if [[ "${job_status}" == "Completed" || "${job_status}" == "completed" ]]; then
    echo "Job completed."
    break
  fi
  if [[ "${job_status}" == "Failed" || "${job_status}" == "failed" ]]; then
    echo "Job failed." >&2
    exit 1
  fi
  sleep 2
done

if [[ "${job_status}" != "Completed" && "${job_status}" != "completed" ]]; then
  echo "Job did not complete in time." >&2
  exit 1
fi

echo "==> Running search"
search_payload='{"query":"red","size":6}'
search_resp="$(curl -s -X POST "${SEARCH_URL%/}/search" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${TENANT_API_KEY}" \
  -H "X-Tenant-Id: ${TENANT_ID}" \
  --data "${search_payload}")"

result_count="$(printf '%s' "${search_resp}" | python3 -c 'import json,sys; data=json.load(sys.stdin); results=data.get("results") or []; print(len(results))')"

if [[ "${result_count}" -le 0 ]]; then
  echo "Search returned no results: ${search_resp}" >&2
  exit 1
fi

echo "Search OK (${result_count} hits)."

search_id="$(printf '%s' "${search_resp}" | json_get searchId)"
if [[ -n "${search_id}" ]]; then
  first_product_id="$(printf '%s' "${search_resp}" | python3 -c 'import json,sys; data=json.load(sys.stdin); results=data.get("results") or []; 
if not results:
  print("")
  sys.exit(0)
source = results[0].get("source") or {}
product_id = source.get("id") or source.get("product_id") or results[0].get("id")
print(product_id or "")')"
  if [[ -n "${first_product_id}" ]]; then
    curl -s -X POST "${SEARCH_URL%/}/search:click" \
      -H "Content-Type: application/json" \
      -H "X-Api-Key: ${TENANT_API_KEY}" \
      -H "X-Tenant-Id: ${TENANT_ID}" \
      --data "{\"searchId\":\"${search_id}\",\"productId\":\"${first_product_id}\"}" >/dev/null
    echo "Click tracked."
  fi
fi

echo "==> Fetching analytics summary"
analytics_resp="$(curl -s "${ADMIN_URL%/}/analytics/summary" \
  -H "X-Api-Key: ${TENANT_API_KEY}" \
  -H "X-Tenant-Id: ${TENANT_ID}")"

total_searches="$(printf '%s' "${analytics_resp}" | json_get total_searches)"
echo "Analytics total_searches=${total_searches}"

echo "==> E2E OK"
