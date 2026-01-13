"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { canManageSecurity, isReadOnly } from "@/lib/roles";

export default function AllowedOriginsPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [origins, setOrigins] = useState<string[]>([]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken && settings.tenantId);
  const canManage = canManageSecurity(settings.role);

  useEffect(() => {
    if (!isReady || !canLoad) {
      return;
    }

    setLoading(true);
    setError(null);

    adminRequest<{ origins: string[] }>(settings, "/allowed-origins")
      .then((response) => setOrigins(response.origins ?? []))
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, [isReady, canLoad, settings.authToken, settings.tenantId]);

  const handleAdd = () => {
    const trimmed = input.trim();
    if (!trimmed) {
      return;
    }

    setOrigins((prev) => {
      if (prev.includes(trimmed)) {
        return prev;
      }
      return [...prev, trimmed];
    });
    setInput("");
  };

  const handleRemove = (origin: string) => {
    setOrigins((prev) => prev.filter((item) => item !== origin));
  };

  const handleSave = async () => {
    setLoading(true);
    setNotice(null);
    setError(null);
    try {
      await adminRequest(
        settings,
        "/allowed-origins",
        {
          method: "PUT",
          body: JSON.stringify({ origins }),
        },
        "application/json"
      );
      setNotice(t("Origin listesi güncellendi."));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("İzinli Originler")}
        subtitle={t("Arama widget'ını çağırabilecek domainleri yönetin.")}
        action={
          <button className="btn-primary" onClick={handleSave} disabled={!canLoad || !canManage}>
            {t("Değişiklikleri kaydet")}
          </button>
        }
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("İzinli originleri yönetmek için API anahtarını ekleyin.")}
        />
      )}

      {settings.authToken && !settings.tenantId && (
        <EmptyState
          title={t("Kiracı bağlamı eksik")}
          description={t("İzinli originleri güncellemek için kiracı id belirleyin.")}
        />
      )}

      {settings.authToken && settings.tenantId && (
        <div className="card p-6">
          <div>
            <p className="label">{t("CORS")}</p>
            <h2 className="font-display text-2xl">{t("Domain izin listesi")}</h2>
          </div>

          {isReadOnly(settings.role) && (
            <div className="mt-4 rounded-2xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-700">
              {t("Görüntüleyici rol: origin güncellemesi kapalı.")}
            </div>
          )}

          <div className="mt-6 flex flex-col gap-3 md:flex-row">
            <input
              className="input flex-1"
              placeholder="https://shop.example.com"
              value={input}
              onChange={(event) => setInput(event.target.value)}
              disabled={!canManage}
            />
            <button className="btn-outline" onClick={handleAdd} disabled={!canManage}>
              {t("Origin ekle")}
            </button>
          </div>

          {notice && (
            <div className="mt-4 rounded-2xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700">
              {notice}
            </div>
          )}
          {error && (
            <div className="mt-4 rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
              {error}
            </div>
          )}

          <div className="mt-6 space-y-3">
            {loading && <div className="text-xs text-[var(--muted)]">{t("Yükleniyor...")}</div>}
            {origins.length === 0 && (
              <div className="text-sm text-[var(--muted)]">{t("Henüz origin yok.")}</div>
            )}
            {origins.map((origin) => (
              <div key={origin} className="card-soft flex items-center justify-between px-4 py-3">
                <span className="text-sm text-[var(--ink)]">{origin}</span>
                <button className="btn-ghost" onClick={() => handleRemove(origin)} disabled={!canManage}>
                  {t("Kaldır")}
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
