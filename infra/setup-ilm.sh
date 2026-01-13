#!/usr/bin/env sh
set -e

ES_URL="${ES_URL:-http://elasticsearch:9200}"
ES_USERNAME="${ES_USERNAME:-}"
ES_PASSWORD="${ES_PASSWORD:-}"

AUTH=""
if [ -n "$ES_USERNAME" ] || [ -n "$ES_PASSWORD" ]; then
  AUTH="-u ${ES_USERNAME:-elastic}:${ES_PASSWORD}"
fi

until curl -s $AUTH "$ES_URL" >/dev/null; do
  echo "Waiting for Elasticsearch..."
  sleep 2
done

echo "Setting passwords..."
curl -s $AUTH -X POST "$ES_URL/_security/user/kibana_system/_password" \
  -H "Content-Type: application/json" \
  -d "{\"password\": \"${ES_PASSWORD}\"}" >/dev/null

curl -s $AUTH -X PUT "$ES_URL/_ilm/policy/logs-embeddra-7d-delete" \
  -H "Content-Type: application/json" \
  -d '{
    "policy": {
      "phases": {
        "hot": {
          "actions": {}
        },
        "delete": {
          "min_age": "7d",
          "actions": {
            "delete": {}
          }
        }
      }
    }
  }' >/dev/null

curl -s $AUTH -X PUT "$ES_URL/_index_template/logs-embeddra-template" \
  -H "Content-Type: application/json" \
  -d '{
    "index_patterns": ["logs-embeddra-*"],
    "template": {
      "settings": {
        "index.lifecycle.name": "logs-embeddra-7d-delete"
      }
    },
    "priority": 500
  }' >/dev/null

echo "ILM policy and template applied."
