#!/bin/bash
set -e

# URLs
ADMIN_API="http://localhost:5114"
SEARCH_API="http://localhost:5222"

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[TEST] $1${NC}"
}
error() {
    echo -e "${RED}[ERROR] $1${NC}"
    exit 1
}

EXTRACT_JSON_PY="import sys, json; print(json.load(sys.stdin)['token'])"
EXTRACT_USER_ID_PY="import sys, json; print(json.load(sys.stdin)['id'])"
EXTRACT_API_KEY_PY="import sys, json; print(json.load(sys.stdin)['apiKey'])"
EXTRACT_TENANT_ID_PY="import sys, json; print(json.load(sys.stdin)['tenantId'])"

log "1. Platform Login"
LOGIN_PAYLOAD='{"email":"platform@embeddra.local","password":"Embeddra123!"}'
PLATFORM_TOKEN=$(curl -s -X POST "$ADMIN_API/auth/login" -H "Content-Type: application/json" -d "$LOGIN_PAYLOAD" | python3 -c "$EXTRACT_JSON_PY")

if [ -z "$PLATFORM_TOKEN" ]; then
    error "Platform Login Failed"
fi
log "Platform Token obtained"

log "2. Create Tenant"
TENANT_ID="test-tenant-$(date +%s)"
TENANT_PAYLOAD="{\"tenantId\":\"$TENANT_ID\",\"name\":\"Test Tenant\"}"
curl -s -X POST "$ADMIN_API/tenants" -H "Content-Type: application/json" -H "Authorization: Bearer $PLATFORM_TOKEN" -d "$TENANT_PAYLOAD" > /dev/null
log "Tenant '$TENANT_ID' created"

log "3. Create Tenant Owner"
OWNER_EMAIL="owner-$TENANT_ID@test.local"
USER_PAYLOAD="{\"tenantId\":\"$TENANT_ID\",\"email\":\"$OWNER_EMAIL\",\"name\":\"Test Owner\",\"password\":\"Embeddra123!\",\"role\":\"tenant_owner\"}"
curl -s -X POST "$ADMIN_API/auth/users" -H "Content-Type: application/json" -H "Authorization: Bearer $PLATFORM_TOKEN" -d "$USER_PAYLOAD" > /dev/null
log "Tenant Owner '$OWNER_EMAIL' created"

log "4. Tenant Owner Login"
LOGIN_PAYLOAD="{\"tenantId\":\"$TENANT_ID\",\"email\":\"$OWNER_EMAIL\",\"password\":\"Embeddra123!\"}"
TENANT_TOKEN=$(curl -s -X POST "$ADMIN_API/auth/login" -H "Content-Type: application/json" -d "$LOGIN_PAYLOAD" | python3 -c "$EXTRACT_JSON_PY")

if [ -z "$TENANT_TOKEN" ]; then
    error "Tenant Login Failed"
fi
log "Tenant Token obtained"

log "5. Create Search API Key (Allowed Origin: http://localhost:8080)"
KEY_PAYLOAD="{\"name\":\"Widget Key\",\"type\":\"search_public\",\"allowedOrigins\":[\"http://localhost:8080\"]}"
API_KEY=$(curl -s -X POST "$ADMIN_API/api-keys" -H "Content-Type: application/json" -H "Authorization: Bearer $TENANT_TOKEN" -H "X-Tenant-Id: $TENANT_ID" -d "$KEY_PAYLOAD" | python3 -c "$EXTRACT_API_KEY_PY")

if [ -z "$API_KEY" ]; then
    error "API Key Creation Failed"
fi
log "API Key created: $API_KEY"

log "6. Test Search (Valid Origin)"
RESPONSE_CODE=$(curl -s -o /dev/null -w "%{http_code}" -H "x-api-key: $API_KEY" -H "Origin: http://localhost:8080" "$SEARCH_API/search?q=test")
if [ "$RESPONSE_CODE" -ne 200 ]; then
    error "Search with valid origin failed (Code: $RESPONSE_CODE)"
fi
log "Search with valid origin success (200 OK)"

log "7. Test Search (Invalid Origin)"
RESPONSE_CODE=$(curl -s -o /dev/null -w "%{http_code}" -H "x-api-key: $API_KEY" -H "Origin: http://evil.com" "$SEARCH_API/search?q=test")
if [ "$RESPONSE_CODE" -ne 403 ]; then
    error "Search with invalid origin should fail with 403 (Code: $RESPONSE_CODE)"
fi
log "Search with invalid origin blocked (403 Forbidden)"

log "SUCCESS: All smoke tests passed!"
