#!/usr/bin/env bash
set -euo pipefail

ADMIN_URL="${ADMIN_URL:-http://localhost:5114}"
TENANT_ID="${TENANT_ID:-demo}"
API_KEY="${API_KEY:-}"
COUNT="${COUNT:-50}"

usage() {
  cat <<'USAGE'
Usage:
  API_KEY=... ./scripts/seed-data.sh [--count N] [--tenant TENANT] [--admin-url URL]

Environment:
  ADMIN_URL   Admin API base URL (default: http://localhost:5114)
  TENANT_ID   Tenant id (default: demo)
  API_KEY     Tenant API key (required)
  COUNT       Number of products to seed (default: 50)
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --count)
      COUNT="$2"
      shift 2
      ;;
    --tenant)
      TENANT_ID="$2"
      shift 2
      ;;
    --admin-url)
      ADMIN_URL="$2"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "${API_KEY}" ]]; then
  echo "API_KEY is required." >&2
  usage >&2
  exit 1
fi

payload="$(
  python3 - "$COUNT" "$TENANT_ID" <<'PY'
import json
import random
import sys

count = int(sys.argv[1])
tenant = sys.argv[2]

random.seed(42)

brands = ["Acme", "Nova", "Vertex", "Pilot", "Aurora", "Nimbus", "Flux", "Atlas"]
categories = ["Shoes", "Apparel", "Accessories", "Home", "Fitness", "Outdoor"]
adjectives = ["Red", "Blue", "Green", "Black", "White", "Amber", "Slate", "Sand"]
items = ["Runner", "Sneaker", "Hoodie", "Backpack", "Bottle", "Jacket", "Shirt", "Boot"]
materials = ["Canvas", "Leather", "Nylon", "Wool", "Cotton", "Mesh"]
sizes = ["XS", "S", "M", "L", "XL"]

products = []
for i in range(count):
  brand = random.choice(brands)
  category = random.choice(categories)
  adjective = random.choice(adjectives)
  item = random.choice(items)
  name = f"{adjective} {item}"
  description = f"{name} by {brand} in {category.lower()} collection."
  product_id = f"{tenant}-{i+1:05d}"
  price = round(random.uniform(9.99, 249.99), 2)
  in_stock = random.random() > 0.15
  attributes = {
      "color": adjective.lower(),
      "size": random.choice(sizes),
      "material": random.choice(materials)
  }
  products.append({
      "id": product_id,
      "name": name,
      "description": description,
      "brand": brand,
      "category": category,
      "price": price,
      "in_stock": in_stock,
      "attributes": attributes
  })

print(json.dumps(products))
PY
)"

echo "Seeding ${COUNT} products into ${TENANT_ID}..." >&2

response="$(
  curl -s -X POST "${ADMIN_URL%/}/products:bulk" \
    -H "Content-Type: application/json" \
    -H "X-Api-Key: ${API_KEY}" \
    -H "X-Tenant-Id: ${TENANT_ID}" \
    --data "${payload}"
)"

echo "${response}"
