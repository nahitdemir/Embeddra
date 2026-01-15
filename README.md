# Embeddra

Modern, ölçeklenebilir bir .NET 8 monorepo projesi. Clean Architecture prensipleriyle tasarlanmış Admin API, Search API ve background worker servislerinden oluşur. Elastic Stack entegrasyonu ile tam observability desteği sunar.

## 🚀 Özellikler

### Backend
- **Clean Architecture**: Her servis Domain/Application/Infrastructure/WebApi katmanlarına ayrılmış
- **Observability**: Elastic APM, Serilog + ECS logging, distributed tracing
- **Cross-cutting Concerns**: Merkezi logging, correlation, audit, exception handling
- **Modern Stack**: .NET 8, Elasticsearch, Kibana, RabbitMQ, PostgreSQL, Redis
- **Developer Experience**: Tek komutla tüm servisleri başlatma, Makefile ve shell script desteği
- **Security**: Elasticsearch authentication, sensitive data masking, API key management

### Frontend (Admin UI)
- **Next.js 14**: App Router, Server Components, React 18
- **Premium UI/UX**: Modern, responsive, accessible arayüz
- **Integration Hub**: Widget entegrasyonu için adım adım rehber (Setup, Origins, Embed, Test)
- **Search Preview**: Canlı arama testi ve sonuç önizleme
- **Role-Based Access**: Platform Owner ve Tenant Owner rolleri
- **Multi-Tenant Support**: Tenant switcher ve otomatik yönlendirme
- **Internationalization**: TR/EN dil desteği

## 📋 Teknoloji Stack

### Runtime & Framework
- .NET 8
- ASP.NET Core 8
- Entity Framework Core 8

### Infrastructure
- **Elasticsearch 8.12.2** - Arama ve log depolama
- **Kibana 8.12.2** - Log ve APM görselleştirme
- **Elastic APM Server 8.12.2** - Application Performance Monitoring
- **PostgreSQL 16** - İlişkisel veritabanı
- **RabbitMQ 3.13** - Mesaj kuyruğu
- **Redis 7** - Cache ve session yönetimi

### Libraries & Tools
- **Serilog** - Structured logging
- **Elastic Common Schema (ECS)** - Log format standardı
- **Elastic APM** - Distributed tracing
- **Swashbuckle** - Swagger/OpenAPI

## 📁 Proje Yapısı

```
Embeddra/
├── apps/
│   ├── Admin/                    # Admin API servisi
│   │   ├── Embeddra.Admin.Domain
│   │   ├── Embeddra.Admin.Application
│   │   ├── Embeddra.Admin.Infrastructure
│   │   └── Embeddra.Admin.WebApi
│   ├── Search/                   # Search API servisi
│   │   ├── Embeddra.Search.Domain
│   │   ├── Embeddra.Search.Application
│   │   ├── Embeddra.Search.Infrastructure
│   │   └── Embeddra.Search.WebApi
│   ├── Worker/                   # Background worker servisi
│   │   ├── Embeddra.Worker.Application
│   │   ├── Embeddra.Worker.Infrastructure
│   │   └── Embeddra.Worker.Host
│   └── admin-ui/                 # Next.js Admin UI (Frontend)
│       ├── app/                  # Next.js App Router
│       ├── components/           # React bileşenleri
│       ├── lib/                  # Utilities ve helpers
│       └── docs/                 # Frontend dokümantasyonu
├── shared/
│   └── BuildingBlocks/          # Cross-cutting concerns
│       ├── Audit/               # Audit logging
│       ├── Correlation/         # Request correlation
│       ├── Exceptions/           # Exception handling
│       ├── Logging/              # Serilog setup & middleware
│       ├── Messaging/            # RabbitMQ integration
│       ├── Observability/        # Elastic APM
│       ├── Results/              # Result pattern
│       └── Tenancy/              # Multi-tenancy support
├── infra/                        # Infrastructure as Code
│   ├── docker-compose.yml       # Tüm servislerin tanımları
│   ├── apm-server.yml           # APM Server yapılandırması
│   ├── kibana.yml               # Kibana yapılandırması
│   ├── setup-ilm.sh             # Index Lifecycle Management
│   └── setup-fleet.sh           # Fleet & APM package setup
├── scripts/                      # Utility scripts
│   └── start-all.sh             # Tek komutla tüm servisleri başlatma
├── docs/                         # Dokümantasyon
│   ├── architecture.md          # Mimari dokümantasyonu
│   ├── mvp.md                   # MVP notları
│   └── observability.md         # Observability detayları
├── Directory.Build.props        # Merkezi paket versiyonları
├── Makefile                     # Geliştirme komutları
└── dev.sh                       # Tek komutla başlatma script'i
```

## 🛠️ Gereksinimler

- **.NET 8 SDK** - [İndir](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Docker & Docker Compose** - Infrastructure servisleri için
- **Git** - Versiyon kontrolü

## 🚀 Hızlı Başlangıç

### ⚡ Tek Komutla Başlat (Önerilen)

```bash
# Tüm sistemi başlat (Docker + Backend)
./scripts/start-all.sh

# Fresh start (tüm verileri sil ve sıfırdan başla)
./scripts/start-all.sh --fresh

# Tüm servisleri durdur
./scripts/start-all.sh --stop
```

Bu komut:
- Docker altyapısını başlatır (Elasticsearch, Kibana, APM, Postgres, Redis, RabbitMQ)
- Admin, Search ve Worker servislerini başlatır
- Erişim URL'lerini konsola yazar

### Alternatif: dev.sh

```bash
./dev.sh up
```

Bu komut:
1. Docker Compose ile tüm infrastructure servislerini başlatır
2. Admin, Search ve Worker servislerini paralel olarak çalıştırır
3. Ctrl+C ile tüm servisleri durdurur

### Infrastructure Servislerini Yönetme

```bash
# Servisleri başlat
./dev.sh up

# Servisleri durdur
./dev.sh down

# Servis durumunu kontrol et
./dev.sh ps

# Logları takip et
./dev.sh logs
```

### Makefile Komutları

```bash
# Yardım menüsü
make help

# Sadece infrastructure servislerini başlat
make deps-up

# Infrastructure servislerini durdur
make deps-down

# Servis durumunu listele
make deps-ps

# Infrastructure loglarını takip et
make deps-logs

# Infrastructure + tüm .NET servislerini başlat
make dev

# Tekil servisleri çalıştır
make run-admin      # Sadece Admin API
make run-search     # Sadece Search API
make run-worker     # Sadece Worker

# Health check'leri kontrol et
make health
```

### Manuel Çalıştırma

```bash
# Projeyi build et
dotnet build

# Servisleri ayrı ayrı çalıştır
dotnet run --project apps/Admin/Embeddra.Admin.WebApi
dotnet run --project apps/Search/Embeddra.Search.WebApi
dotnet run --project apps/Worker/Embeddra.Worker.Host
```

## 🌐 Servisler ve Portlar

### .NET Servisleri

| Servis | Port | Health Check | Swagger |
|--------|------|--------------|---------|
| Admin API | 5114 | http://localhost:5114/health | http://localhost:5114/swagger |
| Search API | 5222 | http://localhost:5222/health | http://localhost:5222/swagger |
| Worker | 5310 | http://localhost:5310/health | - |

### Frontend (Admin UI)

| Servis | Port | Açıklama |
|--------|------|----------|
| Admin UI (Next.js) | 3000 | http://localhost:3000 | Platform ve Tenant yönetim arayüzü |

### Infrastructure Servisleri

| Servis | Port | Kullanıcı Adı | Şifre | Açıklama |
|--------|------|---------------|-------|----------|
| Elasticsearch | 9200 | `elastic` | `embeddra` | Arama motoru ve log depolama |
| Kibana | 5601 | `elastic` | `embeddra` | Log ve APM görselleştirme |
| APM Server | 8200 | - | - | Application Performance Monitoring |
| RabbitMQ Management | 15672 | `embeddra` | `embeddra` | Mesaj kuyruğu yönetimi |
| PostgreSQL | 5433 | `embeddra` | `embeddra` | Veritabanı (db: `embeddra`) |
| Redis | 6379 | - | - | Cache ve session |

### Hızlı Kontroller

```bash
# Elasticsearch
curl -u elastic:embeddra http://localhost:9200

# Kibana
curl http://localhost:5601

# APM Server
curl http://localhost:8200

# RabbitMQ
curl http://localhost:15672

# PostgreSQL
psql "host=localhost port=5433 dbname=embeddra user=embeddra password=embeddra"

# Redis
redis-cli ping
```

## 📊 Observability

### Logging

- **Format**: ECS (Elastic Common Schema) uyumlu JSON
- **Destination**: Elasticsearch
- **Index Pattern**: `logs-embeddra-*`
- **Retention**: 7 gün (ILM policy ile otomatik)

Her log event'inde şu bilgiler otomatik olarak eklenir:
- `service.name`, `service.version`, `service.environment`
- `correlation_id`, `tenant_id`
- `trace.id`, `transaction.id`, `span.id` (Elastic APM)

### Distributed Tracing

Elastic APM ile tüm servisler arası request tracing:
- **Admin API**: `embeddra-admin`
- **Search API**: `embeddra-search`
- **Worker**: `embeddra-worker`

Kibana'da **Observability > APM** bölümünden trace'leri görüntüleyebilirsiniz.

### Request/Response Logging

- HTTP metadata (method, path, status, duration, etc.)
- JSON body logging (4KB limit, truncation)
- Sensitive data masking (password, token, apiKey, etc.)
- Özel endpoint'ler için özet logging

Detaylar için [observability.md](docs/observability.md) dosyasına bakın.

## 🔧 Yapılandırma

### Environment Variables

Servisler aşağıdaki environment variable'ları destekler:

```bash
# Elasticsearch
ELASTICSEARCH_URL=http://localhost:9200
ELASTICSEARCH_USERNAME=elastic
ELASTICSEARCH_PASSWORD=embeddra

# Elastic APM
ELASTIC_APM_SERVER_URL=http://localhost:8200
ELASTIC_APM_SERVICE_NAME=embeddra-admin  # veya embeddra-search, embeddra-worker
ELASTIC_APM_ENVIRONMENT=Development

# Database
ConnectionStrings__DefaultConnection=Host=localhost;Port=5433;Database=embeddra;Username=embeddra;Password=embeddra

# RabbitMQ
RABBITMQ_CONNECTION_STRING=amqp://embeddra:embeddra@localhost:5672/
```

### appsettings.json

Her servisin kendi `appsettings.json` dosyası vardır. Geliştirme ortamı için `appsettings.Development.json` kullanılır.

## 📚 Dokümantasyon

- [Architecture](docs/architecture.md) - Mimari dokümantasyonu ve Clean Architecture prensipleri
- [MVP Notes](docs/mvp.md) - MVP notları ve hızlı referans
- [Observability](docs/observability.md) - Logging, tracing ve monitoring detayları

## 🏗️ Mimari

### Clean Architecture Katmanları

```
┌─────────────────────────────────────┐
│         WebApi / Host                │  ← Controllers, Program.cs
├─────────────────────────────────────┤
│         Infrastructure              │  ← DB, External Services
├─────────────────────────────────────┤
│         Application                 │  ← Use Cases, Business Logic
├─────────────────────────────────────┤
│         Domain                      │  ← Entities, Value Objects
└─────────────────────────────────────┘
```

### Dependency Direction

```
Domain → Application → Infrastructure → Host
```

Shared BuildingBlocks, Application/Infrastructure/Host katmanları tarafından referans edilebilir.

## 🧪 Geliştirme

### Kod Standartları

- `.editorconfig` - Kod formatı standartları
- `Nullable` reference types enabled
- `ImplicitUsings` enabled
- C# 12 özellikleri

### Paket Yönetimi

Tüm NuGet paket versiyonları `Directory.Build.props` dosyasında merkezi olarak yönetilir.

### Health Checks

Her servis `/health` endpoint'i üzerinden health check sağlar:
- Admin API: http://localhost:5114/health
- Search API: http://localhost:5222/health
- Worker: http://localhost:5310/health


---

**Not**: Bu proje geliştirme aşamasındadır. Production kullanımı için ek güvenlik ve performans optimizasyonları gerekebilir.
