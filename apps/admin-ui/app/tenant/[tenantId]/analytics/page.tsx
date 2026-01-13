"use client";

import { useEffect, useMemo, useState } from "react";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { formatNumber, formatPercent } from "@/lib/utils";

type SummaryResponse = {
  total_searches: number;
  no_result_count: number;
  no_result_rate: number;
  click_count: number;
  click_through_rate: number;
};

type TopQuery = {
  query: string;
  count: number;
  noResultCount: number;
};

type TopQueryResponse = {
  queries: TopQuery[];
};

export default function AnalyticsPage() {
  const params = useParams();
  const routeTenantId = params.tenantId as string;
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const ranges = [
    { label: t("Son 24 saat"), value: "1" },
    { label: t("Son 7 gün"), value: "7" },
    { label: t("Son 30 gün"), value: "30" },
  ];
  const [range, setRange] = useState("7");
  const [summary, setSummary] = useState<SummaryResponse | null>(null);
  const [topQueries, setTopQueries] = useState<TopQuery[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Use route tenantId as fallback if settings.tenantId is not set
  const tenantId = settings.tenantId || routeTenantId;
  const canLoad = Boolean(settings.authToken && tenantId);

  const rangeParams = useMemo(() => {
    const days = Number(range);
    const to = new Date();
    const from = new Date(to.getTime() - days * 24 * 60 * 60 * 1000);
    return { from: from.toISOString(), to: to.toISOString() };
  }, [range]);

  const loadAnalytics = async () => {
    if (!canLoad) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const [summaryResponse, topQueriesResponse] = await Promise.all([
        adminRequest<SummaryResponse>(
          settings,
          `/analytics/summary?from=${encodeURIComponent(rangeParams.from)}&to=${encodeURIComponent(rangeParams.to)}`,
          { tenantId }
        ),
        adminRequest<TopQueryResponse>(
          settings,
          `/analytics/top-queries?from=${encodeURIComponent(rangeParams.from)}&to=${encodeURIComponent(rangeParams.to)}&limit=8`,
          { tenantId }
        ),
      ]);
      setSummary(summaryResponse);
      setTopQueries(topQueriesResponse.queries ?? []);
    } catch (err) {
      const errorMessage = (err as Error).message;
      // User-friendly error messages
      if (errorMessage.includes("invalid_token") || errorMessage.includes("401")) {
        setError("Oturum süresi doldu. Lütfen tekrar giriş yapın.");
      } else if (errorMessage.includes("internal_error")) {
        setError("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
      } else {
        setError(errorMessage);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!isReady || !canLoad || !tenantId) {
      return;
    }

    loadAnalytics();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isReady, canLoad, tenantId, range]);

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("Analitik")}
        subtitle={t("Sorgu talebi, sonuçsuz aramalar ve tıklama oranını izleyin.")}
        action={
          <div className="flex flex-wrap items-center gap-3">
            <select
              className="input"
              value={range}
              onChange={(event) => setRange(event.target.value)}
            >
              {ranges.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
            <button className="btn-outline" onClick={loadAnalytics} disabled={!canLoad}>
              {t("Yenile")}
            </button>
          </div>
        }
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("Analitik verileri yüklemek için API anahtarını ekleyin.")}
        />
      )}

      {settings.authToken && !tenantId && (
        <EmptyState
          title={t("Kiracı bağlamı eksik")}
          description={t("Analitiği görüntülemek için kiracı id belirleyin.")}
        />
      )}

      {settings.authToken && tenantId && (
        <>
          {error && (
            <div className="card-soft border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
              {error}
            </div>
          )}

          <div className="grid gap-4 md:grid-cols-4">
            <div className="card p-6">
              <p className="label">{t("Sorgular")}</p>
              <p className="font-display text-3xl">{formatNumber(summary?.total_searches)}</p>
              <p className="text-sm text-[var(--muted)]">{t("Toplam arama")}</p>
            </div>
            <div className="card p-6">
              <p className="label">{t("Sonuçsuz")}</p>
              <p className="font-display text-3xl">{formatPercent(summary?.no_result_rate)}</p>
              <p className="text-sm text-[var(--muted)]">
                {formatNumber(summary?.no_result_count)} {t("sonuçsuz arama")}
              </p>
            </div>
            <div className="card p-6">
              <p className="label">{t("Tıklamalar")}</p>
              <p className="font-display text-3xl">{formatNumber(summary?.click_count)}</p>
              <p className="text-sm text-[var(--muted)]">{t("Ürün tıklamaları")}</p>
            </div>
            <div className="card p-6">
              <p className="label">CTR</p>
              <p className="font-display text-3xl">{formatPercent(summary?.click_through_rate)}</p>
              <p className="text-sm text-[var(--muted)]">{t("Tıklama içeren aramalar")}</p>
            </div>
          </div>

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Öne çıkan sorgular")}</p>
                <h2 className="font-display text-2xl">{t("Arama talebi")}</h2>
              </div>
              {loading && <span className="text-xs text-[var(--muted)]">{t("Yenileniyor...")}</span>}
            </div>

            <div className="mt-6 space-y-3">
              {topQueries.length === 0 && (
                <div className="text-sm text-[var(--muted)]">{t("Henüz sorgu verisi yok.")}</div>
              )}
              {topQueries.map((item) => (
                <div key={item.query} className="card-soft flex flex-wrap items-center justify-between gap-4 px-4 py-3">
                  <div>
                    <p className="font-medium text-[var(--ink)]">{item.query}</p>
                    <p className="text-xs text-[var(--muted)]">
                      {formatNumber(item.noResultCount)} {t("sonuçsuz arama")}
                    </p>
                  </div>
                  <div className="pill">{formatNumber(item.count)} {t("arama")}</div>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
