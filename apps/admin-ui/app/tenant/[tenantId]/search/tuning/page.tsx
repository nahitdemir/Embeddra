"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { canManageSearchTuning, isReadOnly } from "@/lib/roles";

const boostFields = ["brand", "category"];

type SynonymItem = { term: string; synonyms: string };

type BoostItem = { field: string; value: string; weight: string };

type PinItem = { query: string; productIds: string };

export default function SearchTuningPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [synonyms, setSynonyms] = useState<SynonymItem[]>([]);
  const [boosts, setBoosts] = useState<BoostItem[]>([]);
  const [pins, setPins] = useState<PinItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken && settings.tenantId);
  const canManage = canManageSearchTuning(settings.role);

  const loadTuning = async () => {
    if (!canLoad) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const [synonymResponse, boostResponse, pinResponse] = await Promise.all([
        adminRequest<{ items: { term: string; synonyms: string[] }[] }>(settings, "/search-tuning/synonyms"),
        adminRequest<{ items: { field: string; value: string; weight: number }[] }>(settings, "/search-tuning/boosts"),
        adminRequest<{ items: { query: string; productIds: string[] }[] }>(settings, "/search-tuning/pins"),
      ]);

      setSynonyms(
        (synonymResponse.items ?? []).map((item) => ({
          term: item.term,
          synonyms: (item.synonyms ?? []).join(", "),
        }))
      );
      setBoosts(
        (boostResponse.items ?? []).map((item) => ({
          field: item.field,
          value: item.value,
          weight: String(item.weight ?? 1),
        }))
      );
      setPins(
        (pinResponse.items ?? []).map((item) => ({
          query: item.query,
          productIds: (item.productIds ?? []).join(", "),
        }))
      );
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

    loadTuning();
  }, [isReady, canLoad]);

  const saveSynonyms = async () => {
    setNotice(null);
    setError(null);

    const payload = synonyms
      .map((item) => ({
        term: item.term.trim(),
        synonyms: item.synonyms
          .split(",")
          .map((value) => value.trim())
          .filter(Boolean),
      }))
      .filter((item) => item.term && item.synonyms.length > 0);

    try {
      await adminRequest(
        settings,
        "/search-tuning/synonyms",
        {
          method: "PUT",
          body: JSON.stringify({ items: payload }),
        },
        "application/json"
      );
      setNotice(t("Eşanlamlılar güncellendi."));
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const saveBoosts = async () => {
    setNotice(null);
    setError(null);

    const payload = boosts
      .map((item) => ({
        field: item.field.trim(),
        value: item.value.trim(),
        weight: Number(item.weight) || 1,
      }))
      .filter((item) => item.field && item.value);

    try {
      await adminRequest(
        settings,
        "/search-tuning/boosts",
        {
          method: "PUT",
          body: JSON.stringify({ items: payload }),
        },
        "application/json"
      );
      setNotice(t("Boost kuralları güncellendi."));
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const savePins = async () => {
    setNotice(null);
    setError(null);

    const payload = pins
      .map((item) => ({
        query: item.query.trim(),
        productIds: item.productIds
          .split(",")
          .map((value) => value.trim())
          .filter(Boolean),
      }))
      .filter((item) => item.query && item.productIds.length > 0);

    try {
      await adminRequest(
        settings,
        "/search-tuning/pins",
        {
          method: "PUT",
          body: JSON.stringify({ items: payload }),
        },
        "application/json"
      );
      setNotice(t("Pinli sonuçlar güncellendi."));
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const addSynonym = () => setSynonyms((prev) => [...prev, { term: "", synonyms: "" }]);
  const addBoost = () => setBoosts((prev) => [...prev, { field: "brand", value: "", weight: "1" }]);
  const addPin = () => setPins((prev) => [...prev, { query: "", productIds: "" }]);

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("Arama Ayarları")}
        subtitle={t("Kiracı araması için eşanlamlı, boost ve pinli sonuçları yönetin.")}
        action={
          <button className="btn-outline" onClick={loadTuning} disabled={!canLoad}>
            {t("Yenile")}
          </button>
        }
      />

      {!settings.authToken && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("Arama ayarlarını yönetmek için API anahtarını ekleyin.")}
        />
      )}

      {settings.authToken && !settings.tenantId && (
        <EmptyState
          title={t("Kiracı bağlamı eksik")}
          description={t("Arama ayarlarını yönetmek için kiracı id belirleyin.")}
        />
      )}

      {settings.authToken && settings.tenantId && (
        <>
          {isReadOnly(settings.role) && (
            <div className="card-soft border border-amber-200 bg-amber-50 p-4 text-sm text-amber-700">
              {t("Görüntüleyici rol: ayar güncellemeleri kapalı.")}
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

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Eşanlamlılar")}</p>
                <h2 className="font-display text-2xl">{t("Terim genişletme")}</h2>
              </div>
              <div className="flex gap-2">
                <button className="btn-outline" onClick={addSynonym} disabled={!canManage}>
                  {t("Satır ekle")}
                </button>
                <button className="btn-primary" onClick={saveSynonyms} disabled={!canManage}>
                  {t("Eşanlamlıları kaydet")}
                </button>
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {synonyms.length === 0 && (
                <div className="text-sm text-[var(--muted)]">{t("Henüz eşanlamlı tanımı yok.")}</div>
              )}
              {synonyms.map((item, index) => (
                <div key={`${item.term}-${index}`} className="grid gap-3 md:grid-cols-[minmax(0,0.3fr),minmax(0,0.6fr),auto]">
                  <input
                    className="input"
                    placeholder={t("ayakkabı")}
                    value={item.term}
                    onChange={(event) =>
                      setSynonyms((prev) =>
                        prev.map((row, i) => (i === index ? { ...row, term: event.target.value } : row))
                      )
                    }
                  />
                  <input
                    className="input"
                    placeholder={t("sneaker, spor ayakkabı")}
                    value={item.synonyms}
                    onChange={(event) =>
                      setSynonyms((prev) =>
                        prev.map((row, i) => (i === index ? { ...row, synonyms: event.target.value } : row))
                      )
                    }
                  />
                  <button
                    className="btn-ghost"
                    onClick={() => setSynonyms((prev) => prev.filter((_, i) => i !== index))}
                    disabled={!canManage}
                  >
                    {t("Kaldır")}
                  </button>
                </div>
              ))}
            </div>
          </div>

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Boostlar")}</p>
                <h2 className="font-display text-2xl">{t("Öne çıkarma kuralları")}</h2>
              </div>
              <div className="flex gap-2">
                <button className="btn-outline" onClick={addBoost} disabled={!canManage}>
                  {t("Satır ekle")}
                </button>
                <button className="btn-primary" onClick={saveBoosts} disabled={!canManage}>
                  {t("Boostları kaydet")}
                </button>
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {boosts.length === 0 && (
                <div className="text-sm text-[var(--muted)]">{t("Henüz boost tanımı yok.")}</div>
              )}
              {boosts.map((item, index) => (
                <div key={`${item.field}-${item.value}-${index}`} className="grid gap-3 md:grid-cols-[minmax(0,0.2fr),minmax(0,0.5fr),minmax(0,0.2fr),auto]">
                  <select
                    className="input"
                    value={item.field}
                    onChange={(event) =>
                      setBoosts((prev) =>
                        prev.map((row, i) => (i === index ? { ...row, field: event.target.value } : row))
                      )
                    }
                  >
                    {boostFields.map((field) => (
                      <option key={field} value={field}>
                        {field}
                      </option>
                    ))}
                  </select>
                  <input
                    className="input"
                    placeholder={t("Acme")}
                    value={item.value}
                    onChange={(event) =>
                      setBoosts((prev) =>
                        prev.map((row, i) => (i === index ? { ...row, value: event.target.value } : row))
                      )
                    }
                  />
                  <input
                    className="input"
                    placeholder="1.4"
                    value={item.weight}
                    onChange={(event) =>
                      setBoosts((prev) =>
                        prev.map((row, i) => (i === index ? { ...row, weight: event.target.value } : row))
                      )
                    }
                  />
                  <button
                    className="btn-ghost"
                    onClick={() => setBoosts((prev) => prev.filter((_, i) => i !== index))}
                    disabled={!canManage}
                  >
                    {t("Kaldır")}
                  </button>
                </div>
              ))}
            </div>
          </div>

          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="label">{t("Pinli sonuçlar")}</p>
                <h2 className="font-display text-2xl">{t("Üst sonuçları garanti edin")}</h2>
              </div>
              <div className="flex gap-2">
                <button className="btn-outline" onClick={addPin} disabled={!canManage}>
                  {t("Satır ekle")}
                </button>
                <button className="btn-primary" onClick={savePins} disabled={!canManage}>
                  {t("Pinleri kaydet")}
                </button>
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {pins.length === 0 && (
                <div className="text-sm text-[var(--muted)]">{t("Henüz pinli sonuç yok.")}</div>
              )}
              {pins.map((item, index) => (
                <div key={`${item.query}-${index}`} className="grid gap-3 md:grid-cols-[minmax(0,0.3fr),minmax(0,0.6fr),auto]">
                  <input
                    className="input"
                    placeholder={t("yaz elbisesi")}
                    value={item.query}
                    onChange={(event) =>
                      setPins((prev) =>
                        prev.map((row, i) => (i === index ? { ...row, query: event.target.value } : row))
                      )
                    }
                  />
                  <input
                    className="input"
                    placeholder={t("urun-123, urun-456")}
                    value={item.productIds}
                    onChange={(event) =>
                      setPins((prev) =>
                        prev.map((row, i) => (i === index ? { ...row, productIds: event.target.value } : row))
                      )
                    }
                  />
                  <button
                    className="btn-ghost"
                    onClick={() => setPins((prev) => prev.filter((_, i) => i !== index))}
                    disabled={!canManage}
                  >
                    {t("Kaldır")}
                  </button>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
