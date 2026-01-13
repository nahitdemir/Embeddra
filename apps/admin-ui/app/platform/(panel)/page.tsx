"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { EmptyState } from "@/components/EmptyState";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatNumber } from "@/lib/utils";

type TenantSummary = {
  id: string;
  name: string;
  status: string;
  createdAt: string;
};

export default function PlatformDashboardPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [tenants, setTenants] = useState<TenantSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isReady || !settings.authToken) {
      return;
    }

    let isMounted = true;
    setLoading(true);
    setError(null);

    adminRequest<{ tenants: TenantSummary[] }>(settings, "/tenants", {
      skipTenantHeader: true,
    })
      .then((response) => {
        if (isMounted) {
          setTenants(response.tenants ?? []);
        }
      })
      .catch((err: Error) => {
        if (isMounted) {
          setError(err.message);
        }
      })
      .finally(() => {
        if (isMounted) {
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [isReady, settings.authToken]);

  const activeCount = tenants.filter((tenant) => tenant.status?.toLowerCase() === "active").length;

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("Platform özeti")}
        subtitle={t("Kiracıları, durumlarını ve sistem genelindeki hareketleri izleyin.")}
        action={
          <Link className="btn-outline" href="/platform/tenants">
            {t("Kiracıları yönet")}
          </Link>
        }
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Platform girişi gerekli")}
          description={t("Platform paneline erişmek için giriş yapın.")}
        />
      )}

      {settings.authToken && (
        <>
          {error && (
            <div className="card-soft border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
              {error}
            </div>
          )}

          <div className="grid gap-4 md:grid-cols-3">
            <div className="card p-6 animate-rise" style={{ animationDelay: "0ms" }}>
              <p className="label">{t("Kiracılar")}</p>
              <p className="font-display text-3xl">{formatNumber(tenants.length)}</p>
              <p className="text-sm text-[var(--muted)]">{t("Toplam tenant")}</p>
            </div>
            <div className="card p-6 animate-rise" style={{ animationDelay: "80ms" }}>
              <p className="label">{t("Durum")}</p>
              <p className="font-display text-3xl">{formatNumber(activeCount)}</p>
              <p className="text-sm text-[var(--muted)]">{t("Aktif tenant")}</p>
            </div>
            <div className="card p-6 animate-rise" style={{ animationDelay: "160ms" }}>
              <p className="label">{t("Denetim")}</p>
              <p className="font-display text-3xl">{t("Canlı")}</p>
              <p className="text-sm text-[var(--muted)]">{t("Denetim kayıtları hazır")}</p>
            </div>
          </div>

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Kiracılar")}</p>
                <h2 className="font-display text-2xl">{t("Son eklenenler")}</h2>
              </div>
              {loading && <span className="text-xs text-[var(--muted)]">{t("Yenileniyor...")}</span>}
            </div>

            {tenants.length === 0 ? (
              <div className="mt-6 text-sm text-[var(--muted)]">
                {t("Henüz kiracı yok.")}
              </div>
            ) : (
              <div className="mt-6 space-y-3">
                {tenants.slice(0, 5).map((tenant) => (
                  <div key={tenant.id} className="card-soft flex items-center justify-between px-4 py-3">
                    <div>
                      <p className="font-medium text-[var(--ink)]">{tenant.name}</p>
                      <p className="text-xs text-[var(--muted)]">
                        {tenant.id} · {formatDate(tenant.createdAt)}
                      </p>
                    </div>
                    <StatusBadge status={tenant.status} />
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
