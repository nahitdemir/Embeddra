# MVP Notes

## Local run

Start dependencies:

```bash
docker compose -f infra/docker-compose.yml up -d
```

Services:

- Elasticsearch: http://localhost:9200
- Kibana: http://localhost:5601
- RabbitMQ management: http://localhost:15672 (user: embeddra, pass: embeddra)
- Postgres: localhost:5432 (db: embeddra, user: embeddra, pass: embeddra)
- Redis: localhost:6379

Quick checks:

```bash
curl http://localhost:9200
curl http://localhost:5601
curl http://localhost:15672
psql "host=localhost port=5432 dbname=embeddra user=embeddra password=embeddra"
redis-cli ping
```
