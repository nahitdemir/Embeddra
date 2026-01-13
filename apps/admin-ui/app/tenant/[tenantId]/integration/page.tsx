"use client";

import { useEffect, useMemo, useState, Suspense, useCallback } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { Stepper } from "@/components/Stepper";
import { IntegrationContextBar } from "@/components/IntegrationContextBar";
import { OriginsManager } from "@/components/OriginsManager";
import { EmbedSnippetPanel } from "@/components/EmbedSnippetPanel";
import { SearchPreviewPanel } from "@/components/SearchPreviewPanel";
import { ToastContainer, useToast } from "@/components/Toast";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { adminRequest } from "@/lib/admin-api";
import { canManageSecurity } from "@/lib/roles";
import { INTEGRATION_STEPS, ROUTES, API_ENDPOINTS } from "@/lib/constants";

type ApiKeySummary = {
  id: string;
  name: string;
  description?: string | null;
  keyPrefix: string;
  keyType?: string;
  type?: string;
  status: string;
  createdAt: string;
  revokedAt?: string | null;
};

type SearchHit = {
  id: string;
  score: number;
  source: Record<string, unknown>;
};

type SearchResponse = {
  searchId?: string | null;
  total?: number | null;
  results?: SearchHit[];
  facets?: {
    brands?: Array<{ key: string; count: number }>;
    categories?: Array<{ key: string; count: number }>;
    priceRanges?: Array<{ key: string; from?: number | null; to?: number | null; count: number }>;
    inStock?: Array<{ key: string; count: number }>;
  };
};

function IntegrationPageContent() {
  const params = useParams();
  const searchParams = useSearchParams();
  const tenantId = params.tenantId as string;
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const toast = useToast();

  // Get initial step from query parameter
  const stepFromQuery = searchParams.get("step");
  const initialStep = stepFromQuery
    ? Math.max(1, Math.min(4, parseInt(stepFromQuery) || 1))
    : INTEGRATION_STEPS.SETUP;

  const [currentStep, setCurrentStep] = useState(initialStep);

  // Shared state
  const [apiKeys, setApiKeys] = useState<ApiKeySummary[]>([]);
  const [origins, setOrigins] = useState<string[]>([]);
  const [selectedApiKeyId, setSelectedApiKeyId] = useState<string | null>(null);
  const [apiKeysLoading, setApiKeysLoading] = useState(false);
  const [originsLoading, setOriginsLoading] = useState(false);
  const [originsSaving, setOriginsSaving] = useState(false);

  // Search Preview state
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchHit[]>([]);
  const [facets, setFacets] = useState<SearchResponse["facets"]>({});
  const [searchId, setSearchId] = useState<string | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken && settings.tenantId);
  const canManage = canManageSecurity(settings.role);
  const searchApiBaseUrl = process.env.NEXT_PUBLIC_SEARCH_API_BASE_URL || "http://localhost:5222";

  // Fetch API keys and origins on mount
  useEffect(() => {
    if (!isReady || !canLoad) {
      return;
    }

    let isMounted = true;

    setApiKeysLoading(true);
    Promise.all([
      adminRequest<{ apiKeys: ApiKeySummary[] }>(settings, API_ENDPOINTS.API_KEYS).catch((err) => {
        console.error("Failed to fetch API keys:", err);
        return { apiKeys: [] }; // Return empty array on error
      }),
      adminRequest<{ origins: string[] }>(settings, API_ENDPOINTS.ALLOWED_ORIGINS).catch((err) => {
        console.error("Failed to fetch origins:", err);
        return { origins: [] }; // Return empty array on error
      }),
    ])
      .then(([keysResponse, originsResponse]) => {
        if (!isMounted) return;

        const keys = keysResponse.apiKeys ?? [];
        setApiKeys(keys);
        setOrigins(originsResponse.origins ?? []);

        // Auto-select: Find first active search_public key
        const searchPublicKey = keys.find(
          (k) =>
            (k.keyType === "search_public" || k.type === "search_public") &&
            k.status === "active"
        );

        if (searchPublicKey) {
          setSelectedApiKeyId(searchPublicKey.id);
        }
      })
      .catch((err: Error) => {
        if (isMounted) {
          console.error("Integration page fetch error:", err);
          // Only show toast for actual network errors, not for individual request failures
          if (err.message.includes("Backend API'ye bağlanılamıyor")) {
            toast.error(err.message);
          }
        }
      })
      .finally(() => {
        if (isMounted) {
          setApiKeysLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [isReady, canLoad, settings.authToken, settings.tenantId]);

  // Get selected API key
  const selectedApiKey = apiKeys.find((k) => k.id === selectedApiKeyId);
  const hasApiKey = Boolean(selectedApiKey && selectedApiKey.status === "active");
  const activeSearchKeys = apiKeys.filter(
    (k) =>
      (k.keyType === "search_public" || k.type === "search_public") &&
      k.status === "active"
  );

  // Save origins
  const handleSaveOrigins = async () => {
    setOriginsSaving(true);
    try {
      await adminRequest(settings, API_ENDPOINTS.ALLOWED_ORIGINS, {
        method: "PUT",
        body: JSON.stringify({ origins }),
      }, "application/json");
      toast.success(t("Origin listesi güncellendi."));
    } catch (err) {
      toast.error((err as Error).message);
    } finally {
      setOriginsSaving(false);
    }
  };

  // Search filters state (managed by SearchPreviewPanel, but we need to track them)
  const [searchFilters, setSearchFilters] = useState<{
    brands?: string[];
    categories?: string[];
    priceMin?: number;
    priceMax?: number;
    inStock?: boolean;
  }>({});

  // Run search - use searchFilters from state
  const runSearch = useCallback(async () => {
    if (!hasApiKey || !query.trim() || !selectedApiKey) {
      return;
    }

    setSearchLoading(true);
    setSearchError(null);

    try {
      const response = await adminRequest<SearchResponse>(
        settings,
        API_ENDPOINTS.SEARCH_PREVIEW,
        {
          method: "POST",
          body: JSON.stringify({
            query: query.trim(),
            size: 12,
            brands: searchFilters.brands?.length ? searchFilters.brands : undefined,
            categories: searchFilters.categories?.length ? searchFilters.categories : undefined,
            inStock: searchFilters.inStock,
            priceMin: searchFilters.priceMin,
            priceMax: searchFilters.priceMax,
            apiKeyId: selectedApiKey.id,
          }),
        },
        "application/json"
      );

      setResults(response.results ?? []);
      setFacets(response.facets ?? {});
      setSearchId(response.searchId ?? null);
    } catch (err) {
      setSearchError((err as Error).message);
      toast.error((err as Error).message);
    } finally {
      setSearchLoading(false);
    }
  }, [hasApiKey, query, selectedApiKey, searchFilters, settings, toast]);

  // Auto-search when query changes (debounced) - but NOT when filters change automatically
  // Filters should only trigger search when user explicitly clicks "Search" button
  useEffect(() => {
    if (currentStep !== INTEGRATION_STEPS.TEST || !query.trim() || query.length < 2 || !hasApiKey || !selectedApiKey) {
      return;
    }

    const timer = setTimeout(() => {
      // Inline search logic to avoid dependency on runSearch
      setSearchLoading(true);
      setSearchError(null);

      adminRequest<SearchResponse>(
        settings,
        API_ENDPOINTS.SEARCH_PREVIEW,
        {
          method: "POST",
          body: JSON.stringify({
            query: query.trim(),
            size: 12,
            brands: searchFilters.brands?.length ? searchFilters.brands : undefined,
            categories: searchFilters.categories?.length ? searchFilters.categories : undefined,
            inStock: searchFilters.inStock,
            priceMin: searchFilters.priceMin,
            priceMax: searchFilters.priceMax,
            apiKeyId: selectedApiKey.id,
          }),
        },
        "application/json"
      )
        .then((response) => {
          setResults(response.results ?? []);
          setFacets(response.facets ?? {});
          setSearchId(response.searchId ?? null);
        })
        .catch((err: Error) => {
          setSearchError(err.message);
          toast.error(err.message);
        })
        .finally(() => {
          setSearchLoading(false);
        });
    }, 800); // Increased debounce to 800ms to reduce requests

    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, currentStep]); // Only trigger on query change, not on filter changes

  const handleResultClick = async (hit: SearchHit) => {
    if (!searchId) {
      return;
    }

    const productId = String(hit.source?.["id"] ?? hit.source?.["product_id"] ?? hit.id ?? "");
    if (!productId) {
      return;
    }

    try {
      await adminRequest(settings, API_ENDPOINTS.SEARCH_PREVIEW_CLICK, {
        method: "POST",
        body: JSON.stringify({ searchId, productId }),
      }, "application/json");
    } catch {
      // Ignore click failures
    }
  };

  // Step navigation
  const handleNext = () => {
    if (currentStep < 4) {
      setCurrentStep(currentStep + 1);
    }
  };

  const handlePrevious = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
    }
  };

  // Primary action based on step
  const getPrimaryAction = () => {
    switch (currentStep) {
      case INTEGRATION_STEPS.SETUP:
        return {
          label: t("Devam Et"),
          onClick: handleNext,
          disabled: false,
        };
      case INTEGRATION_STEPS.ORIGINS:
        return {
          label: t("Kaydet ve Devam Et"),
          onClick: async () => {
            await handleSaveOrigins();
            handleNext();
          },
          disabled: originsSaving,
          loading: originsSaving,
        };
      case INTEGRATION_STEPS.EMBED:
        return {
          label: t("Test Et"),
          onClick: () => {
            setCurrentStep(INTEGRATION_STEPS.TEST);
          },
          disabled: !hasApiKey,
        };
      case INTEGRATION_STEPS.TEST:
        return {
          label: t("Aramayı Çalıştır"),
          onClick: runSearch,
          disabled: !query.trim() || searchLoading,
          loading: searchLoading,
        };
      default:
        return undefined;
    }
  };

  const steps = [
    { label: t("Kurulum"), description: t("API bilgileri") },
    { label: t("Originler"), description: t("CORS ayarları") },
    { label: t("Entegrasyon"), description: t("Snippet kodu") },
    { label: t("Test"), description: t("Canlı önizleme") },
  ];

  if (!isReady || !canLoad) {
    return (
      <div className="flex min-h-screen items-center justify-center p-8 text-sm text-[var(--muted)]">
        {t("Yükleniyor...")}
      </div>
    );
  }

  return (
    <div className="min-h-screen">
      {/* Sticky Context Bar */}
      <IntegrationContextBar
        apiKeys={apiKeys}
        selectedApiKeyId={selectedApiKeyId}
        onApiKeyChange={setSelectedApiKeyId}
        primaryAction={getPrimaryAction()}
        showManageLink={true}
      />

      <div className="space-y-6 px-6 py-6">
        <PageHeader
          title={t("Entegrasyon Merkezi")}
          subtitle={t("Arama widget'ını kurun, yapılandırın ve test edin.")}
        />

        {/* Stepper */}
        <div className="card p-6">
          <Stepper steps={steps} currentStep={currentStep} />
        </div>

        {/* Step Content */}
        <div className="card p-6">
          {/* Step 1: Setup */}
          {currentStep === INTEGRATION_STEPS.SETUP && (
            <div className="space-y-6">
              <div>
                <h2 className="font-display text-xl font-semibold text-[var(--ink)] mb-2">
                  {t("API Kurulumu")}
                </h2>
                <p className="text-sm text-[var(--muted)]">
                  {t("Arama API'nizi kullanmak için gerekli bilgileri görüntüleyin.")}
                </p>
              </div>

              <div className="grid gap-4 md:grid-cols-3">
                <div className="card-soft p-4">
                  <label className="label text-xs mb-1">{t("Arama API URL")}</label>
                  <div className="mt-1 flex items-center gap-2">
                    <div className="font-mono text-sm text-[var(--ink)] bg-[var(--background)] px-3 py-2 rounded-lg border border-[var(--border)] flex-1">
                      {searchApiBaseUrl}
                    </div>
                    <button
                      className="btn-ghost text-xs"
                      onClick={() => {
                        navigator.clipboard.writeText(searchApiBaseUrl);
                        toast.success(t("URL kopyalandı"));
                      }}
                      aria-label={t("URL'yi kopyala")}
                    >
                      📋
                    </button>
                  </div>
                </div>

                <div className="card-soft p-4">
                  <label className="label text-xs mb-1">{t("Kiracı ID")}</label>
                  <div className="mt-1 flex items-center gap-2">
                    <div className="font-mono text-sm text-[var(--ink)] bg-[var(--background)] px-3 py-2 rounded-lg border border-[var(--border)] flex-1">
                      {tenantId}
                    </div>
                    <button
                      className="btn-ghost text-xs"
                      onClick={() => {
                        navigator.clipboard.writeText(tenantId);
                        toast.success(t("Kiracı ID kopyalandı"));
                      }}
                      aria-label={t("Kiracı ID'yi kopyala")}
                    >
                      📋
                    </button>
                  </div>
                </div>

                {selectedApiKey ? (
                  <div className="card-soft p-4">
                    <label className="label text-xs mb-1">{t("API Anahtarı (Önek)")}</label>
                    <div className="mt-1 flex items-center gap-2">
                      <div className="font-mono text-sm text-[var(--ink)] bg-[var(--background)] px-3 py-2 rounded-lg border border-[var(--border)] flex-1">
                        {selectedApiKey.keyPrefix}...
                      </div>
                      <button
                        className="btn-ghost text-xs"
                        onClick={() => {
                          navigator.clipboard.writeText(selectedApiKey.keyPrefix);
                          toast.success(t("Önek kopyalandı"));
                        }}
                        aria-label={t("Öneki kopyala")}
                      >
                        📋
                      </button>
                    </div>
                    <p className="mt-2 text-xs text-[var(--muted)]">
                      {t("Tam anahtarı Güvenlik > API Anahtarları sayfasından alın.")}
                    </p>
                  </div>
                ) : (
                  <div className="card-soft p-4 border border-amber-200 bg-amber-50">
                    <p className="text-sm text-amber-700 mb-2">
                      {t("API anahtarı bulunamadı")}
                    </p>
                    {canManage && (
                      <a
                        href={ROUTES.TENANT_SECURITY_API_KEYS(tenantId)}
                        className="btn-outline text-xs"
                      >
                        {t("Oluştur")} →
                      </a>
                    )}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Step 2: Origins */}
          {currentStep === INTEGRATION_STEPS.ORIGINS && (
            <OriginsManager
              origins={origins}
              onOriginsChange={setOrigins}
              onSave={handleSaveOrigins}
              canManage={canManage}
              loading={originsLoading}
              showStandaloneLink={true}
            />
          )}

          {/* Step 3: Embed */}
          {currentStep === INTEGRATION_STEPS.EMBED && (
            <EmbedSnippetPanel
              snippet=""
              tenantId={tenantId}
              searchApiBaseUrl={searchApiBaseUrl}
              selectedApiKeyPrefix={selectedApiKey?.keyPrefix}
            />
          )}

          {/* Step 4: Test */}
          {currentStep === INTEGRATION_STEPS.TEST && (
            <SearchPreviewPanel
              query={query}
              onQueryChange={setQuery}
              results={results}
              facets={facets}
              loading={searchLoading}
              error={searchError}
              onSearch={() => runSearch()}
              onFiltersChange={setSearchFilters}
              onResultClick={handleResultClick}
              tenantId={tenantId}
              searchApiBaseUrl={searchApiBaseUrl}
              hasApiKey={hasApiKey}
              hasOrigins={origins.length > 0}
            />
          )}
        </div>

        {/* Navigation Buttons */}
        <div className="flex items-center justify-between">
          <button
            className="btn-outline"
            onClick={handlePrevious}
            disabled={currentStep === 1}
            aria-label={t("Önceki adım")}
          >
            {t("← Önceki")}
          </button>
          <button
            className="btn-outline"
            onClick={handleNext}
            disabled={currentStep === 4}
            aria-label={t("Sonraki adım")}
          >
            {t("Sonraki →")}
          </button>
        </div>
      </div>

      {/* Toast Container */}
      <ToastContainer toasts={toast.toasts} onRemove={toast.removeToast} />
    </div>
  );
}

export default function IntegrationPage() {
  return (
    <Suspense fallback={
      <div className="flex min-h-screen items-center justify-center p-8 text-sm text-[var(--muted)]">
        Yükleniyor...
      </div>
    }>
      <IntegrationPageContent />
    </Suspense>
  );
}
