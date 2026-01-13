"use client";

import type { ChangeEvent } from "react";
import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { StatusBadge } from "@/components/StatusBadge";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { canManageImports, isReadOnly } from "@/lib/roles";
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

type IngestionResponse = {
  job_id: string;
  status: string;
  count: number;
};

export default function ImportsPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [jsonPayload, setJsonPayload] = useState("[]");
  const [csvPayload, setCsvPayload] = useState("");
  const [jobs, setJobs] = useState<JobSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken && settings.tenantId);
  const canManage = canManageImports(settings.role);

  const loadJobs = async () => {
    setLoading(true);
    try {
      const response = await adminRequest<{ jobs: JobSummary[] }>(settings, "/ingestion-jobs?limit=8");
      setJobs(response.jobs ?? []);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!isReady || !canLoad) {
      return;
    }

    loadJobs();
  }, [isReady, canLoad, settings.authToken, settings.tenantId]);

  const handleJsonImport = async () => {
    setNotice(null);
    setError(null);

    let parsed: unknown;
    try {
      parsed = JSON.parse(jsonPayload);
    } catch {
      setError(t("Geçersiz JSON içeriği."));
      return;
    }

    try {
      const response = await adminRequest<IngestionResponse>(
        settings,
        "/products:bulk",
        {
          method: "POST",
          body: JSON.stringify(parsed),
        },
        "application/json"
      );
      setNotice(
        t("İş kuyruğa alındı: {jobId} ({count} doküman)", {
          jobId: response.job_id,
          count: response.count,
        })
      );
      await loadJobs();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleCsvImport = async () => {
    if (!csvPayload.trim()) {
      setError(t("CSV içeriği boş."));
      return;
    }

    setNotice(null);
    setError(null);
    try {
      const response = await adminRequest<IngestionResponse>(
        settings,
        "/products:importCsv",
        {
          method: "POST",
          body: csvPayload,
        },
        "text/plain"
      );
      setNotice(
        t("İş kuyruğa alındı: {jobId} ({count} satır)", {
          jobId: response.job_id,
          count: response.count,
        })
      );
      await loadJobs();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleCsvFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    const text = await file.text();
    setCsvPayload(text);
  };

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("Aktarımlar")}
        subtitle={t("Toplu JSON veya CSV payloadlarını aktarım akışına gönderin.")}
        action={
          <button className="btn-outline" onClick={loadJobs} disabled={!canLoad}>
            {t("İşleri yenile")}
          </button>
        }
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("Aktarım göndermek için API anahtarını ekleyin.")}
        />
      )}

      {settings.authToken && !settings.tenantId && (
        <EmptyState
          title={t("Kiracı bağlamı eksik")}
          description={t("Aktarım işleri için kiracı id belirleyin.")}
        />
      )}

      {settings.authToken && settings.tenantId && (
        <>
          {isReadOnly(settings.role) && (
            <div className="card-soft border border-amber-200 bg-amber-50 p-4 text-sm text-amber-700">
              {t("Görüntüleyici rol: aktarım kapalı.")}
            </div>
          )}
          {notice && (
            <div className="card-soft border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">
              {notice}
            </div>
          )}
          {error && (
            <div className="card-soft border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
              {error}
            </div>
          )}

          <div className="grid gap-6 lg:grid-cols-2">
            <div className="card p-6">
              <div>
                <p className="label">{t("JSON")}</p>
                <h2 className="font-display text-2xl">{t("Toplu yükleme")}</h2>
              </div>
              <textarea
                className="textarea mt-4 h-56"
                value={jsonPayload}
                onChange={(event) => setJsonPayload(event.target.value)}
              />
              <button className="btn-primary mt-4" onClick={handleJsonImport} disabled={!canManage}>
                {t("JSON aktarımı kuyruğa al")}
              </button>
            </div>

            <div className="card p-6">
              <div>
                <p className="label">{t("CSV")}</p>
                <h2 className="font-display text-2xl">{t("CSV aktarımı")}</h2>
              </div>
              <div className="mt-4 flex flex-wrap items-center gap-3">
                <input
                  className="text-sm"
                  type="file"
                  accept=".csv,text/csv"
                  onChange={handleCsvFile}
                  disabled={!canManage}
                />
                <span className="text-xs text-[var(--muted)]">{t("CSV yapıştırın veya yükleyin")}</span>
              </div>
              <textarea
                className="textarea mt-4 h-48"
                value={csvPayload}
                onChange={(event) => setCsvPayload(event.target.value)}
                placeholder="id,name,brand,category,price,in_stock"
              />
              <button className="btn-primary mt-4" onClick={handleCsvImport} disabled={!canManage}>
                {t("CSV aktarımı kuyruğa al")}
              </button>
            </div>
          </div>

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("İşler")}</p>
                <h2 className="font-display text-2xl">{t("Son aktarım işleri")}</h2>
              </div>
              {loading && <span className="text-xs text-[var(--muted)]">{t("Yenileniyor...")}</span>}
            </div>

            <div className="mt-6 space-y-3">
              {jobs.length === 0 && (
                <div className="text-sm text-[var(--muted)]">{t("Henüz aktarım işi yok.")}</div>
              )}
              {jobs.map((job) => (
                <div key={job.id} className="card-soft flex flex-wrap items-center justify-between gap-4 px-4 py-3">
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
          </div>
        </>
      )}
    </div>
  );
}
