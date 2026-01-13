# Revizyon Planı

## Faz 1: Domain ve Auth Refactoring
1. **User Rolleri**:
   - `Embeddra.Admin.Domain` altında `UserRole` enum'ı oluştur (PlatformOwner, TenantOwner).
   - `AdminUser` sınıfını güncelle: `Role` property'sini bu enum ile (veya constant string ile) sınırla.
   - Platform kullanıcıları için `TenantId` null olabilir veya "platform" olabilir.

2. **Panel Login Ayrımı**:
   - `AuthController.Login` metodunu incele.
   - Platform login: `TenantId` boş gönderildiğinde sadece PlatformOwner user bulmalı.
   - Tenant login: `TenantId` zorunlu, TenantOwner user bulmalı.
   - JWT payload'ına `role` ve `tenant_id` claim'lerini doğru bas.

3. **Search API Security**:
   - `ApiKey` entity'si zaten Origin listesi tutuyor mu? `ApiKey` entity'sine `AllowedOrigins` (string array veya comma-separated) property'si ekle/kontrol et.
   - `SearchAccessMiddleware` içinde Origin kontrolünü API Key validasyonundan hemen sonra yap.

## Faz 2: Observability ve Logging
1. **Logging Policy**:
   - `SearchRequestResponseLoggingPolicy` ve `AdminRequestResponseLoggingPolicy` içinde `SensitiveDataMasker` kullanımını doğrula.
   - Bulk endpointleri (`/imports`) için log body'sini truncate et veya sadece metadata logla.
   - `logs-embeddra-*` ILM policy'sinin 7 gün retention ile çalıştığını doğrula (`setup-ilm.sh`).

2. **Elastic APM**:
   - Transaction ve Span'lerin doğru aktığını test et. Background job (`Worker`) transaction başlatıyor mu?

## 3. Worker ve Data Pipeline
1. **Mock Embedding**:
   - `Embeddra.Worker.Infrastructure` içinde embedding servisini bul ve MVP için deterministic olduğundan emin ol.
2. **Job Pipeline**:
   - `IngestionJob` statülerini güncelleme (Processing -> Completed/Failed) ve hata loglama akışını kontrol et.

## 4. Runbook ve E2E
1. `docs/runbook.md` oluştur.
2. Shell script veya `curl` komutlarıyla bir "Smoke Test" senaryosu yaz.

# Adım Adım Uygulama Listesi

- [ ] `UserRole` enum ve `AdminUser` güncellemesi.
- [ ] `ApiKey` entity güncellemesi (AllowedOrigins).
- [ ] `AuthController` login mantığı revizyonu.
- [ ] `SearchAccessMiddleware` origin kontrolü.
- [ ] `SearchRequestResponseLoggingPolicy` optimizations.
- [ ] Worker mock embedding verification.
- [ ] Runbook oluşturma.
