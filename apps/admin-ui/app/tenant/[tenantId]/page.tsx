"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { StatusBadge } from "@/components/StatusBadge";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatDuration, formatNumber } from "@/lib/utils";

type JobSummary = {
  id: string;
  sourceType: string;
  status: string;
  totalCount: number;
  processedCount: number;
  failedCount: number;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  error?: string | null;
};

type ApiKeySummary = {
  id: string;
  name: string;
  status: string;
};

export default function DashboardPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [jobs, setJobs] = useState<JobSummary[]>([]);
  const [apiKeys, setApiKeys] = useState<ApiKeySummary[]>([]);
  const [origins, setOrigins] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken && settings.tenantId);

  useEffect(() => {
    if (!isReady || !canLoad) {
      return;
    }

    let isMounted = true;
    setLoading(true);
    setError(null);

    Promise.all([
      adminRequest<{ jobs: JobSummary[] }>(settings, "/ingestion-jobs?limit=6"),
      adminRequest<{ apiKeys: ApiKeySummary[] }>(settings, "/api-keys"),
      adminRequest<{ origins: string[] }>(settings, "/allowed-origins"),
    ])
      .then(([jobsResponse, keysResponse, originsResponse]) => {
        if (!isMounted) {
          return;
        }
        setJobs(jobsResponse.jobs ?? []);
        setApiKeys(keysResponse.apiKeys ?? []);
        setOrigins(originsResponse.origins ?? []);
      })
      .catch((err: Error) => {
        if (!isMounted) {
          return;
        }
        setError(err.message);
      })
      .finally(() => {
        if (isMounted) {
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [isReady, canLoad, settings]);

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("Gösterge Paneli")}
        subtitle={t("Sağlık sinyalleri ve son aktarım hareketleri.")}
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("Kiracı verilerini yüklemek için Ayarlar'da Admin API URL ve anahtarını girin.")}
        />
      )}

      {settings.authToken && !settings.tenantId && (
        <EmptyState
          title={t("Kiracı bağlamı eksik")}
          description={t("İşleri, originleri ve API anahtarlarını yüklemek için kiracı id belirleyin.")}
        />
      )}

      {settings.authToken && settings.tenantId && (
        <>
          {error && (
            <div className="card-soft border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
              {error}
            </div>
          )}

          <div className="grid gap-4 md:grid-cols-3">
            <div className="card p-6 animate-rise" style={{ animationDelay: "0ms" }}>
              <p className="label">{t("Aktarım")}</p>
              <p className="font-display text-3xl">{formatNumber(jobs.length)}</p>
              <p className="text-sm text-[var(--muted)]">{t("Son işler")}</p>
            </div>
            <div className="card p-6 animate-rise" style={{ animationDelay: "80ms" }}>
              <p className="label">{t("API Anahtarları")}</p>
              <p className="font-display text-3xl">
                {formatNumber(apiKeys.filter((key) => key.status === "active").length)}
              </p>
              <p className="text-sm text-[var(--muted)]">{t("Aktif anahtarlar")}</p>
            </div>
            <div className="card p-6 animate-rise" style={{ animationDelay: "160ms" }}>
              <p className="label">{t("İzinli Originler")}</p>
              <p className="font-display text-3xl">{formatNumber(origins.length)}</p>
              <p className="text-sm text-[var(--muted)]">{t("CORS origin sayısı")}</p>
            </div>
          </div>

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Akış")}</p>
                <h2 className="font-display text-2xl">{t("Son aktarım işleri")}</h2>
              </div>
              {loading && <span className="text-xs text-[var(--muted)]">{t("Yenileniyor...")}</span>}
            </div>

            {jobs.length === 0 ? (
              <div className="mt-6 text-sm text-[var(--muted)]">
                {t("Henüz aktarım işi yok. İndekslemek için CSV veya JSON yükleyin.")}
              </div>
            ) : (
              <div className="mt-6 space-y-3">
                {jobs.map((job) => (
                  <div
                    key={job.id}
                    className="card-soft flex flex-wrap items-center justify-between gap-4 px-4 py-3"
                  >
                    <div>
                      <p className="font-medium text-[var(--ink)]">{job.sourceType}</p>
                      <p className="text-xs text-[var(--muted)]">
                        {formatDate(job.createdAt)} · {formatDuration(job.startedAt, job.completedAt)}
                      </p>
                    </div>
                    <div className="flex items-center gap-4">
                      <div className="text-right text-xs text-[var(--muted)]">
                        <div>{t("İşlenen")} {formatNumber(job.processedCount)} / {formatNumber(job.totalCount)}</div>
                        {job.failedCount > 0 && (
                          <div>{t("Hatalı")} {formatNumber(job.failedCount)}</div>
                        )}
                      </div>
                      <StatusBadge status={job.status} />
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
