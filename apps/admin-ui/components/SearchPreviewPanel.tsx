"use client";

import { useState, useMemo, useEffect } from "react";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/utils";
import { EmptyState } from "@/components/EmptyState";

type SearchHit = {
  id: string;
  score: number;
  source: Record<string, unknown>;
};

type FacetBucket = {
  key: string;
  count: number;
};

type RangeFacetBucket = {
  key: string;
  from?: number | null;
  to?: number | null;
  count: number;
};

type SearchResponse = {
  searchId?: string | null;
  total?: number | null;
  results?: SearchHit[];
  facets?: {
    brands?: FacetBucket[];
    categories?: FacetBucket[];
    priceRanges?: RangeFacetBucket[];
    inStock?: FacetBucket[];
  };
};

type SearchPreviewPanelProps = {
  query: string;
  onQueryChange: (query: string) => void;
  results: SearchHit[];
  facets: SearchResponse["facets"];
  loading: boolean;
  error: string | null;
  onSearch: () => void;
  onFiltersChange?: (filters: {
    brands?: string[];
    categories?: string[];
    priceMin?: number;
    priceMax?: number;
    inStock?: boolean;
  }) => void;
  onResultClick?: (hit: SearchHit) => void;
  tenantId: string;
  searchApiBaseUrl: string;
  hasApiKey: boolean;
  hasOrigins: boolean;
};

export function SearchPreviewPanel({
  query,
  onQueryChange,
  results,
  facets,
  loading,
  error,
  onSearch,
  onFiltersChange,
  onResultClick,
  tenantId,
  searchApiBaseUrl,
  hasApiKey,
  hasOrigins,
}: SearchPreviewPanelProps) {
  const { t } = useI18n();
  const [selectedBrands, setSelectedBrands] = useState<string[]>([]);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [selectedPrice, setSelectedPrice] = useState<RangeFacetBucket | null>(null);
  const [inStockOnly, setInStockOnly] = useState(false);

  const toggleValue = (list: string[], value: string) =>
    list.includes(value) ? list.filter((item) => item !== value) : [...list, value];

  const activeFilters = useMemo(
    () => ({
      brands: selectedBrands,
      categories: selectedCategories,
      inStock: inStockOnly ? true : undefined,
      priceMin: selectedPrice?.from ?? undefined,
      priceMax: selectedPrice?.to ?? undefined,
    }),
    [selectedBrands, selectedCategories, selectedPrice, inStockOnly]
  );

  // Notify parent when filters change (but don't trigger auto-search)
  // Filters are only applied when user clicks "Search" button
  useEffect(() => {
    onFiltersChange?.(activeFilters);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeFilters]); // Removed onFiltersChange from deps to prevent infinite loop

  if (!hasApiKey) {
    return (
      <EmptyState
        title={t("API Anahtarı Gerekli")}
        description={t("Arama testi için önce bir API anahtarı oluşturmanız gerekir.")}
      />
    );
  }

  if (!hasOrigins) {
    return (
      <EmptyState
        title={t("Origin Gerekli")}
        description={t("Arama testi için önce en az bir origin eklemeniz gerekir. Origin eklemeden widget çalışmayacaktır.")}
      />
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h3 className="font-display text-lg font-semibold text-[var(--ink)] mb-1">
          {t("Canlı Arama Testi")}
        </h3>
        <p className="text-sm text-[var(--muted)]">
          {t("Gerçek kiracı verileriyle alaka, facet ve arama davranışını doğrulayın.")}
        </p>
      </div>

      {/* Search Input */}
      <div className="card-soft p-4">
        <div className="flex flex-wrap items-center justify-between gap-4 mb-4">
          <div>
            <p className="label">{t("Sorgu")}</p>
            <h4 className="font-display text-xl">{t("Canlı arama")}</h4>
          </div>
          <div className="pill text-xs">
            {t("Kiracı: {tenant} | {url}", {
              tenant: tenantId,
              url: searchApiBaseUrl,
            })}
          </div>
        </div>

        <div className="flex flex-wrap gap-3">
          <input
            className="input flex-1"
            placeholder={t("Örn: koşu ayakkabısı, kırmızı elbise, pamuklu tişört...")}
            value={query}
            onChange={(e) => onQueryChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                onSearch();
              }
            }}
            aria-label={t("Arama sorgusu")}
          />
          <button
            className="btn-primary"
            onClick={onSearch}
            disabled={!query.trim() || loading}
            aria-label={t("Ara")}
          >
            {loading ? t("Aranıyor...") : t("Ara")}
          </button>
        </div>

        {error && (
          <div className="mt-4 rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
            {error}
          </div>
        )}
      </div>

      {/* Results and Filters */}
      <div className="grid gap-6 lg:grid-cols-[minmax(0,0.32fr),minmax(0,0.68fr)]">
        {/* Filters */}
        <div className="card-soft p-4 space-y-4">
          <div>
            <p className="label">{t("Filtreler")}</p>
            <h4 className="font-display text-lg">{t("Fasetler")}</h4>
          </div>

          {/* Brands */}
          <div>
            <p className="text-xs font-medium uppercase tracking-[0.2em] text-[var(--muted)]">
              {t("Marka")}
            </p>
            <div className="mt-2 space-y-2">
              {(facets?.brands ?? []).length === 0 && (
                <div className="text-xs text-[var(--muted)]">{t("Marka yok")}</div>
              )}
              {(facets?.brands ?? []).map((bucket) => (
                <label
                  key={bucket.key}
                  className="flex items-center justify-between text-sm"
                >
                  <span className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={selectedBrands.includes(bucket.key)}
                      onChange={() =>
                        setSelectedBrands((prev) => toggleValue(prev, bucket.key))
                      }
                      aria-label={t("{brand} markası", { brand: bucket.key })}
                    />
                    {bucket.key}
                  </span>
                  <span className="text-xs text-[var(--muted)]">
                    {formatNumber(bucket.count)}
                  </span>
                </label>
              ))}
            </div>
          </div>

          {/* Categories */}
          <div>
            <p className="text-xs font-medium uppercase tracking-[0.2em] text-[var(--muted)]">
              {t("Kategori")}
            </p>
            <div className="mt-2 space-y-2">
              {(facets?.categories ?? []).length === 0 && (
                <div className="text-xs text-[var(--muted)]">{t("Kategori yok")}</div>
              )}
              {(facets?.categories ?? []).map((bucket) => (
                <label
                  key={bucket.key}
                  className="flex items-center justify-between text-sm"
                >
                  <span className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={selectedCategories.includes(bucket.key)}
                      onChange={() =>
                        setSelectedCategories((prev) => toggleValue(prev, bucket.key))
                      }
                      aria-label={t("{category} kategorisi", { category: bucket.key })}
                    />
                    {bucket.key}
                  </span>
                  <span className="text-xs text-[var(--muted)]">
                    {formatNumber(bucket.count)}
                  </span>
                </label>
              ))}
            </div>
          </div>

          {/* Price Ranges */}
          <div>
            <p className="text-xs font-medium uppercase tracking-[0.2em] text-[var(--muted)]">
              {t("Fiyat aralığı")}
            </p>
            <div className="mt-2 space-y-2">
              {(facets?.priceRanges ?? []).length === 0 && (
                <div className="text-xs text-[var(--muted)]">
                  {t("Fiyat aralığı yok")}
                </div>
              )}
              {(facets?.priceRanges ?? []).map((bucket) => (
                <button
                  key={bucket.key}
                  className={`flex w-full items-center justify-between rounded-xl border px-3 py-2 text-sm transition ${
                    selectedPrice?.key === bucket.key
                      ? "border-[var(--accent)] bg-[color:var(--surface-glass-strong)]"
                      : "border-[var(--border)] bg-[color:var(--surface-glass)] hover:bg-[color:var(--surface-glass-strong)]"
                  }`}
                  onClick={() =>
                    setSelectedPrice((prev) => (prev?.key === bucket.key ? null : bucket))
                  }
                  aria-label={t("{range} fiyat aralığı", { range: bucket.key })}
                >
                  <span>{bucket.key}</span>
                  <span className="text-xs text-[var(--muted)]">
                    {formatNumber(bucket.count)}
                  </span>
                </button>
              ))}
            </div>
          </div>

          {/* In Stock */}
          <div className="flex items-center justify-between text-sm">
            <span>{t("Sadece stokta")}</span>
            <input
              type="checkbox"
              checked={inStockOnly}
              onChange={() => setInStockOnly((prev) => !prev)}
              aria-label={t("Sadece stokta olan ürünleri göster")}
            />
          </div>

          {/* Clear Filters */}
          <button
            className="btn-outline w-full text-sm"
            onClick={() => {
              setSelectedBrands([]);
              setSelectedCategories([]);
              setSelectedPrice(null);
              setInStockOnly(false);
            }}
            aria-label={t("Filtreleri temizle")}
          >
            {t("Filtreleri temizle")}
          </button>
        </div>

        {/* Results */}
        <div className="card-soft p-4">
          <div className="flex items-center justify-between mb-4">
            <div>
              <p className="label">{t("Sonuçlar")}</p>
              <h4 className="font-display text-lg">{t("Eşleşen ürünler")}</h4>
            </div>
            {loading && (
              <span className="text-xs text-[var(--muted)]">{t("Aranıyor...")}</span>
            )}
          </div>

          {results.length === 0 && !loading && (
            <div className="mt-6 text-sm text-[var(--muted)]">
              {t("Henüz sonuç yok. Daha uzun bir sorgu deneyin veya filtreleri değiştirin.")}
            </div>
          )}

          <div className="mt-4 space-y-2">
            {results.map((hit) => {
              const source = hit.source ?? {};
              const name = String(source["name"] ?? source["title"] ?? hit.id);
              const brand = source["brand"] ? String(source["brand"]) : "";
              const category = source["category"] ? String(source["category"]) : "";
              const price = source["price"] ? String(source["price"]) : "";
              const inStock = source["in_stock"] ?? source["inStock"];
              return (
                <button
                  key={hit.id}
                  type="button"
                  className="card-soft w-full text-left transition hover:bg-[color:var(--surface-glass-strong)] px-4 py-3"
                  onClick={() => onResultClick?.(hit)}
                  aria-label={t("{name} ürününü görüntüle", { name })}
                >
                  <div className="flex flex-wrap items-center justify-between gap-4">
                    <div>
                      <div className="font-medium text-[var(--ink)]">{name}</div>
                      <div className="text-xs text-[var(--muted)]">
                        {[brand, category].filter(Boolean).join(" | ") || t("Kategorisiz")}
                      </div>
                    </div>
                    <div className="text-right text-xs text-[var(--muted)]">
                      {price && <div>${price}</div>}
                      {typeof inStock !== "undefined" && (
                        <div>{inStock ? t("Stokta") : t("Stokta yok")}</div>
                      )}
                    </div>
                  </div>
                </button>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
