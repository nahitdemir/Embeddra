# Runbook: Embeddra MVP Setup & Operations

## 1. Ön Hazırlık
- **Docker**: Docker Desktop veya Engine çalışıyor olmalı.
- **.NET 8 SDK**: Yüklü olmalı.
- **Portlar**: 5433 (Postgres), 5601 (Kibana), 9200 (Elastic), 8200 (APM), 5672 (RabbitMQ), 6379 (Redis) boş olmalı.

## 2. Sistemi Başlatma (Fresh Start)
Tüm sistemi sıfırdan başlatmak (verileri silerek) için:

```bash
chmod +x scripts/*.sh
./scripts/start-all.sh --fresh
```

Bu işlem yaklaşık 1-2 dakika sürebilir (Elasticsearch açılışı).

## 3. smoke-test.sh ile Test
Sistemin çalıştığını doğrulamak için hazırlanan E2E test scriptini çalıştırın:

```bash
./scripts/smoke-test.sh
```

Bu script sırasıyla:
1. Platform Owner olarak login olur.
2. Yeni bir Tenant ve Tenant Owner oluşturur.
3. Tenant Owner olarak login olur.
4. "Search Public Key" oluşturur (Allowed Origins ile).
5. Search API'yi test eder (Origin kontrolü ve yetkisiz erişim testi).
6. Örnek bir CSV import job'ı başlatır (Mock embedding ile).

Script çıktısında "SUCCESS" görüyorsanız temel akış çalışıyor demektir.

## 4. Observability Kontrolü

### Loglar (Kibana)
1. Tarayıcıda http://localhost:5601 adresine gidin. (User: `elastic`, Pass: `embeddra`)
2. **Discover** menüsüne gidin.
3. `logs-embeddra-*` data view'ını seçin.
4. Logların `service.name`, `trace.id`, `tenant_id` alanlarını içerdiğini doğrulayın.

### APM (Kibana)
1. **Observability > APM** menüsüne gidin.
2. `embeddra-admin`, `embeddra-search`, `embeddra-worker` servislerini görmelisiniz.
3. `embeddra-worker` servisinde ingestion job transaction'larını inceleyebilirsiniz.

### Data (Elasticsearch)
1. Indexleri listeleme:
   ```bash
   curl -u elastic:embeddra http://localhost:9200/_cat/indices?v
   ```
2. `embeddra-products-v1` index'inin oluştuğunu ve doküman içerdiğini doğrulayın.

## 5. Sorun Giderme
- **Auth Hatası**: DB seed gerçekleşmemiş olabilir. `docker-compose logs admin-api` kontrol edin.
- **Search Origin Hatası**: `smoke-test.sh` içinde kullanılan Origin ile Key'in Allowed Origins listesi eşleşmiyor olabilir.
- **Worker/Job Hatası**: RabbitMQ bağlantısını kontrol edin.

## 6. Geliştirme Notları
- **Platform Login**: `TenantId` alanı boş bırakılmalı.
- **Tenant Login**: `TenantId` alanı dolu olmalı.
- **Search Key**: Artık user-bazlı değil, Key'e özel `AllowedOrigins` kontrolü var.
