# Mevcut Durum Değerlendirmesi

## 1. Proje Yapısı ve Mimari
- **Monorepo**: Standard `apps` (Admin, Search, Worker) ve `shared` (BuildingBlocks) yapısı.
- **Mimari**: Clean Architecture (Domain, Application, Infrastructure, WebApi) tüm servislerde uygulanmış.
- **Database**: PostgreSQL kullanılıyor, EF Core ile erişim sağlanıyor.
- **Messaging**: RabbitMQ entegrasyonu mevcut.

## 2. Authentication & Authorization
- **Admin API**: JWT tabanlı auth mevcut (`JwtBearer`). `AdminUser` entity'si var ancak rol yönetimi basit ("owner" string). Platform vs Tenant ayrımı net değil.
- **Search API**: API Key auth mevcut. `ApiKey` entity'si var ve `SearchPublic` tipi destekleniyor.
- **Eksikler**:
  - Platform Owner vs Tenant Owner rol ayrımı kodda net değil (`AdminUser`'da sadece TenantId ve Role string var).
  - API Key için "Allowed Origins" kontrolü `Program.cs`'de CORS policy içinde yapılıyor, ancak auth layer'da da kontrol edilmeli.

## 3. Observability
- **Stack**: Elasticsearch 8.12, Kibana, APM Server, Serilog.
- **Logging**: Serilog `EcsTextFormatter` ile yapılandırılmış. `logs-embeddra-*` index'ine yazıyor. 
- **Tracing**: Elastic APM entegrasyonu var (`ElasticApmLogEnricher` dahil).
- **Correlation**: `X-Correlation-Id` header desteği var.
- **Eksikler**:
  - `setup-ilm.sh` var ancak çalıştığına emin olunmalı (docker-compose'da `es-setup` servisi var).
  - Request/Response logging policy'lerin 4KB limit ve masking kurallarına tam uyup uymadığı kontrol edilmeli.

## 4. Servisler ve Endpointler
- **Admin API**: Tenant ve User yönetimi, Job yönetimi.
- **Search API**: `/search` endpoint'i ve widget desteği.
- **Worker**: Background job processing (CSV/JSON import).

## 5. Riskler
- **Security**: Platform API Key UI'da gösterilmemeli (Admin API'de bu kontrolün yapıldığından emin olunmalı).
- **Performance**: Bulk import sırasında loglama çok şişebilir, "summary logger" kullanılmalı.
- **Data Integrity**: Deterministic mock embedding üretimi MVP için kabul edilebilir ancak interface arkasında olmalı.

## 6. Önerilen Düzeltmeler
1. **Auth Refactor**:
   - `AdminUser` rollerini `PlatformOwner` ve `TenantOwner` olarak enum'a çevir veya sabitle.
   - Platform login ve Tenant login akışlarını ayır veya tenant-id header/claim üzerinden yönet.
2. **Search API Key**:
   - Allowed Origins kontrolünü strict hale getir.
   - Rate limiting uygula (kodda `SearchRateLimiter` var, aktifliğini kontrol et).
3. **Logging**:
   - `RequestResponseLoggingPolicy` maskeleme kurallarını sıkılaştır.
   - Bulk import için özel logging behavior ekle.
