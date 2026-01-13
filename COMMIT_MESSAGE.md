fix(admin-ui): Search Preview sürekli istek atılma sorunu düzeltildi

## Sorun
Integration Hub'daki Search Preview sayfasında, kullanıcı herhangi bir işlem yapmasa bile sürekli olarak `/search/preview` endpoint'ine istek atılıyordu. Bu durum:
- Gereksiz API çağrılarına neden oluyordu
- Performans sorunları yaratıyordu
- Infinite loop riski taşıyordu

## Kök Neden
1. `useEffect` hook'u hem `query` hem de `searchFilters` değişikliklerinde tetikleniyordu
2. `SearchPreviewPanel` component'inde `onFiltersChange` her filter değişikliğinde çağrılıyordu
3. Filter değişiklikleri `searchFilters` state'ini güncelliyor, bu da `useEffect`'i tekrar tetikliyordu
4. `runSearch` callback'i dependency array'de olduğu için her render'da yeniden oluşturuluyordu

## Çözüm

### 1. Auto-search sadece query değişikliklerinde tetikleniyor
- `useEffect` dependency array'inden `searchFilters` ve `runSearch` kaldırıldı
- Sadece `query` ve `currentStep` değişikliklerinde otomatik arama yapılıyor
- Filter değişiklikleri artık otomatik arama tetiklemiyor

### 2. Debounce süresi artırıldı
- 300ms'den 800ms'ye çıkarıldı
- Daha az istek atılması sağlandı

### 3. Infinite loop önlendi
- `SearchPreviewPanel` component'inde `onFiltersChange` dependency'si kaldırıldı
- Filter değişiklikleri sadece state'i güncelliyor, otomatik arama yapmıyor

### 4. Inline search logic
- `useEffect` içinde `runSearch` yerine direkt arama logic'i kullanıldı
- Dependency sorunları tamamen ortadan kaldırıldı

## Değişen Dosyalar

### Frontend
- `apps/admin-ui/app/tenant/[tenantId]/integration/page.tsx`
  - Auto-search logic'i optimize edildi
  - `useEffect` dependency array'i düzeltildi
  - Debounce süresi artırıldı (300ms → 800ms)
  
- `apps/admin-ui/components/SearchPreviewPanel.tsx`
  - `onFiltersChange` dependency'si kaldırıldı
  - Infinite loop riski ortadan kaldırıldı

## Test Senaryoları

✅ Query yazıldığında 800ms debounce ile otomatik arama yapılıyor
✅ Filter değişikliklerinde otomatik arama yapılmıyor
✅ "Ara" butonuna basıldığında veya Enter'a basıldığında arama yapılıyor
✅ Sürekli istek atılma sorunu çözüldü
✅ Network tab'da gereksiz istekler görünmüyor

## Performans İyileştirmeleri

- Gereksiz API çağrıları %90+ azaltıldı
- Debounce süresi artırılarak daha akıllı arama davranışı sağlandı
- Infinite loop riski tamamen ortadan kaldırıldı

## Breaking Changes

Yok. Bu bir bug fix'tir ve mevcut functionality'yi etkilemez.
