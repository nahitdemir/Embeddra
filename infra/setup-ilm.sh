#!/usr/bin/env sh
set -e

ES_URL="${ES_URL:-http://elasticsearch:9200}"

until curl -s "$ES_URL" >/dev/null; do
  echo "Waiting for Elasticsearch..."
  sleep 2
done

curl -s -X PUT "$ES_URL/_ilm/policy/logs-embeddra-7d-delete" \
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

curl -s -X PUT "$ES_URL/_index_template/logs-embeddra-template" \
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
