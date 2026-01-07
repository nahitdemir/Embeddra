# MVP Notes

## Local run

Single command (infra + apps):

```bash
./dev.sh
```

Preferred (infra + apps via Docker):

```bash
./scripts/start-all.sh
./scripts/start-all.sh --fresh
./scripts/start-all.sh --stop
```

Start dependencies:

```bash
docker compose -f infra/docker-compose.yml up -d
```

Or use Make:

```bash
make deps-up
```

Services:

- Elasticsearch: http://localhost:9200
- Kibana: http://localhost:5601
- APM Server: http://localhost:8200
- RabbitMQ management: http://localhost:15672 (user: embeddra, pass: embeddra)
- Postgres: localhost:5433 (db: embeddra, user: embeddra, pass: embeddra)
- Redis: localhost:6379
- Kibana login: elastic / embeddra

Quick checks:

```bash
curl -u elastic:embeddra http://localhost:9200
curl http://localhost:5601
curl http://localhost:8200
curl http://localhost:15672
psql "host=localhost port=5433 dbname=embeddra user=embeddra password=embeddra"
redis-cli ping
```

Start all .NET services together:

```bash
make dev
```
