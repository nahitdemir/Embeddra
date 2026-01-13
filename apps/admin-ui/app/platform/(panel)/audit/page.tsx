"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/utils";

type AuditLog = {
  id: string;
  tenantId?: string | null;
  action: string;
  actor?: string | null;
  correlationId?: string | null;
  payloadJson: string;
  createdAt: string;
};

type Filters = {
  tenantId: string;
  action: string;
  actor: string;
  correlationId: string;
  from: string;
  to: string;
  limit: number;
};

export default function AuditLogsPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [logs, setLogs] = useState<AuditLog[]>([]);
  const [filters, setFilters] = useState<Filters>({
    tenantId: "",
    action: "",
    actor: "",
    correlationId: "",
    from: "",
    to: "",
    limit: 50,
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken);

  const loadLogs = async () => {
    setLoading(true);
    setError(null);

    const params = new URLSearchParams();
    if (filters.action.trim()) {
      params.set("action", filters.action.trim());
    }
    if (filters.actor.trim()) {
      params.set("actor", filters.actor.trim());
    }
    if (filters.correlationId.trim()) {
      params.set("correlationId", filters.correlationId.trim());
    }
    if (filters.tenantId.trim()) {
      params.set("tenantId", filters.tenantId.trim());
    }
    if (filters.from) {
      params.set("from", new Date(filters.from).toISOString());
    }
    if (filters.to) {
      params.set("to", new Date(filters.to).toISOString());
    }
    params.set("limit", String(filters.limit));

    try {
      const response = await adminRequest<{ logs: AuditLog[] }>(
        settings,
        `/audit-logs?${params.toString()}`,
        { skipTenantHeader: true }
      );
      setLogs(response.logs ?? []);
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

    loadLogs();
  }, [isReady, canLoad, settings.authToken]);

  const handleFilterChange = (field: keyof Filters, value: string | number) => {
    setFilters((prev) => ({
      ...prev,
      [field]: value,
    }));
  };

  const handleClear = () => {
    setFilters({
      tenantId: "",
      action: "",
      actor: "",
      correlationId: "",
      from: "",
      to: "",
      limit: 50,
    });
  };

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("Denetim Kayıtları")}
        subtitle={t("Kim neyi değiştirdi filtreleyin ve correlation id ile servisler arası izleyin.")}
        action={
          <div className="flex gap-3">
            <button className="btn-outline" onClick={handleClear}>
              {t("Filtreleri temizle")}
            </button>
            <button className="btn-primary" onClick={loadLogs} disabled={!canLoad}>
              {t("Filtreleri uygula")}
            </button>
          </div>
        }
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("Denetim kayıtlarını yüklemek için platform girişi yapın.")}
        />
      )}

      {settings.authToken && (
        <>
          <div className="card p-6">
            <div>
              <p className="label">{t("Filtreler")}</p>
              <h2 className="font-display text-2xl">{t("Denetim sorgusu")}</h2>
            </div>
            <div className="mt-6 grid gap-4 md:grid-cols-3">
              <div>
                <label className="label">{t("Kiracı id")}</label>
                <input
                  className="input mt-2"
                  value={filters.tenantId}
                  onChange={(event) => handleFilterChange("tenantId", event.target.value)}
                  placeholder="demo"
                />
              </div>
              <div>
                <label className="label">{t("Aksiyon")}</label>
                <input
                  className="input mt-2"
                  value={filters.action}
                  onChange={(event) => handleFilterChange("action", event.target.value)}
                  placeholder="api_key_created"
                />
              </div>
              <div>
                <label className="label">{t("Aktör")}</label>
                <input
                  className="input mt-2"
                  value={filters.actor}
                  onChange={(event) => handleFilterChange("actor", event.target.value)}
                  placeholder="admin-ui"
                />
              </div>
              <div>
                <label className="label">{t("Korelasyon id")}</label>
                <input
                  className="input mt-2"
                  value={filters.correlationId}
                  onChange={(event) => handleFilterChange("correlationId", event.target.value)}
                  placeholder="c6c88196..."
                />
              </div>
              <div>
                <label className="label">{t("Başlangıç")}</label>
                <input
                  className="input mt-2"
                  type="datetime-local"
                  value={filters.from}
                  onChange={(event) => handleFilterChange("from", event.target.value)}
                />
              </div>
              <div>
                <label className="label">{t("Bitiş")}</label>
                <input
                  className="input mt-2"
                  type="datetime-local"
                  value={filters.to}
                  onChange={(event) => handleFilterChange("to", event.target.value)}
                />
              </div>
              <div>
                <label className="label">{t("Limit")}</label>
                <select
                  className="input mt-2"
                  value={filters.limit}
                  onChange={(event) => handleFilterChange("limit", Number(event.target.value))}
                >
                  {[25, 50, 100, 200].map((value) => (
                    <option key={value} value={value}>
                      {value} {t("kayıt")}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          {error && (
            <div className="card-soft border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
              {error}
            </div>
          )}

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Kayıtlar")}</p>
                <h2 className="font-display text-2xl">{t("Son hareketler")}</h2>
              </div>
              {loading && <span className="text-xs text-[var(--muted)]">{t("Yenileniyor...")}</span>}
            </div>

            <div className="mt-6 space-y-3">
              {logs.length === 0 && !loading && (
                <div className="text-sm text-[var(--muted)]">{t("Denetim kaydı bulunamadı.")}</div>
              )}
              {logs.map((log) => (
                <div key={log.id} className="card-soft px-4 py-3">
                  <div className="flex flex-wrap items-center justify-between gap-4">
                    <div>
                      <p className="font-medium text-[var(--ink)]">{log.action}</p>
                      <p className="text-xs text-[var(--muted)]">
                        {log.actor ?? t("sistem")} · {formatDate(log.createdAt)}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      {log.tenantId && <span className="pill">{t("Kiracı")} {log.tenantId}</span>}
                      {log.correlationId && (
                        <CorrelationActions correlationId={log.correlationId} />
                      )}
                    </div>
                  </div>
                  <details className="mt-3 text-xs text-[var(--muted)]">
                    <summary className="cursor-pointer">{t("İçerik")}</summary>
                    <pre className="mt-2 whitespace-pre-wrap rounded-2xl bg-[color:var(--surface-glass-strong)] p-3 text-[11px] text-[var(--ink)]">
                      {log.payloadJson}
                    </pre>
                  </details>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function CorrelationActions({ correlationId }: { correlationId: string }) {
  const { settings } = useAdminSettings();
  const { t } = useI18n();
  const baseUrl = settings.observabilityUrl?.trim();
  const link = baseUrl
    ? `${baseUrl}/app/discover#/?_a=(query:(language:kuery,query:'correlationId:\"${correlationId}\"'))`
    : null;

  const handleCopy = async () => {
    await navigator.clipboard.writeText(correlationId);
  };

  return (
    <div className="flex items-center gap-2">
      <button className="btn-ghost" onClick={handleCopy}>
        {t("ID kopyala")}
      </button>
      {link && (
        <a className="btn-outline" href={link} target="_blank" rel="noreferrer">
          {t("Logları aç")}
        </a>
      )}
    </div>
  );
}
