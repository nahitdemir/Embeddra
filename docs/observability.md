# Observability

Bu dokuman lokal Elastic Stack ile log/trace gozlemi ve maliyet kontrolu kurallarini aciklar.

## Loglama (Serilog + ECS + Elasticsearch)

- Loglar ECS uyumlu JSON olarak Elasticsearch'e gonderilir.
- Index isimleri:
  - `logs-embeddra-admin`
  - `logs-embeddra-search`
  - `logs-embeddra-worker`
- Enrichment (her log event'inde):
  - `service.name`, `service.version`, `service.environment`
  - `correlation_id`, `tenant_id`
  - `trace.id`, `transaction.id`, `span.id` (Elastic APM)

## Correlation

- HTTP header: `X-Correlation-Id`
- Request basinda yoksa uretilir, response header'a eklenir.
- Loglarda hem `correlation_id` hem `trace.id` bulunur.

## Request/Response Logging Policy

- Metadata: `method`, `path`, `status`, `duration_ms`, `origin`, `user_agent`, `remote_ip`
- Body sadece JSON ise loglanir.
- Maksimum body boyutu: **4KB** (truncate + `truncated: true`)
- Masking (JSON + headers): `apiKey`, `token`, `authorization`, `password`, `secret`

### Ozel endpointler

- `/products:bulk` ve `/products:importCsv`:
  - body yerine ozet loglanir
  - `document_count`, `csv_row_count`, `sample_product_ids` (max 5), `payload_bytes`

### Search maliyet kontrolu

- `query` metni max 200 karakter loglanir
- Basarili search isteklerinde body loglama **%10**
- Hata durumunda body loglama **%100**

## Elastic APM

- Admin/Search/Worker icin Elastic APM agent aktif.
- Server URL env'den okunur: `ELASTIC_APM_SERVER_URL`
- ServiceName:
  - `embeddra-admin`
  - `embeddra-search`
  - `embeddra-worker`

### Worker transaction/spans

- Transaction: `IngestionJob` (type: `background`)
- Spans:
  - `DB.FetchProductsRaw`
  - `Embedding.Generate`
  - `ES.BulkIndex`
  - `DB.UpdateJobStatus`
  - `Rabbit.Ack`

## ILM / Retention

- `logs-embeddra-*` icin ILM policy:
  - **7 gun** hot
  - sonra **delete**
- docker-compose baslarken otomatik setup yapilir (idempotent).

## Kibana kontrol adimlari

1. Logs:
   - Discover'da `logs-embeddra-*` pattern ile arama yap.
2. APM:
   - Observability > APM altinda servisleri gor:
     - `embeddra-admin`, `embeddra-search`, `embeddra-worker`
3. Trace <-> Log:
   - Bir request at.
   - Loglardan `trace.id` degerini kopyala.
   - APM'de ayni `trace.id` ile filtrele.
