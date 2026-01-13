"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { canManageSecurity, isReadOnly } from "@/lib/roles";
import { formatDate } from "@/lib/utils";

type ApiKeySummary = {
  id: string;
  name: string;
  description?: string | null;
  keyPrefix: string;
  status: string;
  createdAt: string;
  revokedAt?: string | null;
};

type ApiKeyCreatedResponse = {
  apiKeyId: string;
  apiKey: string;
  apiKeyPrefix: string;
};

export default function ApiKeysPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [apiKeys, setApiKeys] = useState<ApiKeySummary[]>([]);
  const [form, setForm] = useState({ name: "", description: "" });
  const [createdKey, setCreatedKey] = useState<ApiKeyCreatedResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken && settings.tenantId);
  const canManage = canManageSecurity(settings.role);

  const loadKeys = async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await adminRequest<{ apiKeys: ApiKeySummary[] }>(settings, "/api-keys");
      setApiKeys(response.apiKeys ?? []);
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

    loadKeys();
  }, [isReady, canLoad, settings.authToken, settings.tenantId]);

  const handleCreate = async () => {
    if (!form.name.trim()) {
      setError(t("Ad gerekli."));
      return;
    }

    setError(null);
    setCreatedKey(null);

    try {
      const response = await adminRequest<ApiKeyCreatedResponse>(
        settings,
        "/api-keys",
        {
          method: "POST",
          body: JSON.stringify({
            name: form.name.trim(),
            description: form.description.trim() || null,
          }),
        },
        "application/json"
      );
      setCreatedKey(response);
      setForm({ name: "", description: "" });
      await loadKeys();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleRevoke = async (apiKeyId: string) => {
    setError(null);
    try {
      await adminRequest(settings, `/api-keys/${apiKeyId}`, { method: "DELETE" });
      await loadKeys();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleCopy = async () => {
    if (!createdKey) {
      return;
    }
    await navigator.clipboard.writeText(createdKey.apiKey);
  };

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("API Anahtarları")}
        subtitle={t("Kiracı entegrasyonları için API anahtarları oluşturun veya iptal edin.")}
        action={
          <button className="btn-outline" onClick={loadKeys} disabled={!canLoad}>
            {t("Yenile")}
          </button>
        }
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("Kiracı kimlik bilgilerini yönetmek için API anahtarı ekleyin.")}
        />
      )}

      {settings.authToken && !settings.tenantId && (
        <EmptyState
          title={t("Kiracı bağlamı eksik")}
          description={t("API anahtarlarını listelemek veya oluşturmak için kiracı id belirleyin.")}
        />
      )}

      {settings.authToken && settings.tenantId && (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.3fr),minmax(0,0.7fr)]">
          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Anahtarlar")}</p>
                <h2 className="font-display text-2xl">{t("Oluşturulan anahtarlar")}</h2>
              </div>
              {loading && <span className="text-xs text-[var(--muted)]">{t("Yenileniyor...")}</span>}
            </div>

            {isReadOnly(settings.role) && (
              <div className="mt-4 rounded-2xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-700">
                {t("Görüntüleyici rol: anahtar yönetimi kapalı.")}
              </div>
            )}
            {error && (
              <div className="mt-4 rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
                {error}
              </div>
            )}

            <div className="mt-6 space-y-3">
              {apiKeys.length === 0 && (
                <div className="text-sm text-[var(--muted)]">{t("Henüz API anahtarı yok.")}</div>
              )}
              {apiKeys.map((key) => {
                const statusKey = key.status?.toLowerCase() ?? "";
                const statusLabel =
                  t(statusKey === "active" ? "Aktif" : statusKey === "revoked" ? "İptal edildi" : "Bilinmiyor");
                const isRevoked = statusKey === "revoked";
                return (
                  <div key={key.id} className="card-soft flex flex-wrap items-center justify-between gap-4 px-4 py-3">
                    <div>
                      <p className="font-medium text-[var(--ink)]">{key.name}</p>
                      <p className="text-xs text-[var(--muted)]">
                        {key.keyPrefix} · {formatDate(key.createdAt)}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="pill">{statusLabel}</span>
                      {!isRevoked && (
                        <button className="btn-ghost" onClick={() => handleRevoke(key.id)} disabled={!canManage}>
                          {t("İptal et")}
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="card p-6">
            <div>
              <p className="label">{t("Oluştur")}</p>
              <h2 className="font-display text-2xl">{t("Yeni API anahtarı")}</h2>
              <p className="mt-2 text-sm text-[var(--muted)]">
                {t("Anahtarlar yalnızca oluşturulduğunda bir kez gösterilir.")}
              </p>
            </div>

            <div className="mt-6 space-y-4">
              <div>
                <label className="label">{t("Ad")}</label>
                <input
                  className="input mt-2"
                  placeholder={t("Katalog senkron")}
                  value={form.name}
                  onChange={(event) => setForm((prev) => ({ ...prev, name: event.target.value }))}
                  disabled={!canManage}
                />
              </div>
              <div>
                <label className="label">{t("Açıklama")}</label>
                <input
                  className="input mt-2"
                  placeholder={t("Aktarım işleri tarafından kullanılır")}
                  value={form.description}
                  onChange={(event) => setForm((prev) => ({ ...prev, description: event.target.value }))}
                  disabled={!canManage}
                />
              </div>
              <button className="btn-primary w-full" onClick={handleCreate} disabled={!canManage}>
                {t("Anahtar oluştur")}
              </button>

              {createdKey && (
                <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">
                  <p className="font-medium">{t("Anahtar oluşturuldu")}</p>
                  <p className="mt-2 break-all font-mono text-xs">{createdKey.apiKey}</p>
                  <button className="btn-ghost mt-3" onClick={handleCopy}>
                    {t("Panoya kopyala")}
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
