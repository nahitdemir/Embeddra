"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { canManageSecurity, roleLabels } from "@/lib/roles";
import { formatDate } from "@/lib/utils";

type AdminUser = {
  id: string;
  email: string;
  name: string;
  role: string;
  status: string;
  createdAt: string;
  lastLoginAt?: string | null;
};

const roleOptions = ["owner", "admin", "viewer"] as const;
const statusOptions = ["active", "disabled"] as const;
const statusLabels: Record<(typeof statusOptions)[number], string> = {
  active: "Aktif",
  disabled: "Devre dışı",
};

export default function UsersPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [drafts, setDrafts] = useState<Record<string, { role: string; status: string; password: string }>>({});
  const [form, setForm] = useState({ email: "", name: "", password: "", role: "viewer" });
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canLoad = Boolean(settings.authToken);
  const canManage = canManageSecurity(settings.role);
  const needsTenant = !settings.authToken;

  const loadUsers = async () => {
    setLoading(true);
    setError(null);

    try {
      const query = needsTenant && settings.tenantId
        ? `?tenantId=${encodeURIComponent(settings.tenantId)}`
        : "";
      const response = await adminRequest<{ users: AdminUser[] }>(
        settings,
        `/auth/users${query}`,
        {
          authStrategy: "auto",
        }
      );

      const items = response.users ?? [];
      setUsers(items);
      setDrafts(
        items.reduce<Record<string, { role: string; status: string; password: string }>>(
          (acc, user) => {
            acc[user.id] = {
              role: user.role,
              status: user.status,
              password: "",
            };
            return acc;
          },
          {}
        )
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

    if (needsTenant && !settings.tenantId) {
      return;
    }

    loadUsers();
  }, [isReady, canLoad, needsTenant, settings.tenantId]);

  const handleCreate = async () => {
    setSaving(true);
    setError(null);
    setNotice(null);

    try {
      await adminRequest(
        settings,
        "/auth/users",
        {
          method: "POST",
          body: JSON.stringify({
            tenantId: settings.tenantId,
            email: form.email.trim(),
            name: form.name.trim(),
            role: form.role,
            password: form.password,
          }),
          authStrategy: "auto",
        },
        "application/json"
      );

      setForm({ email: "", name: "", password: "", role: "viewer" });
      setNotice(t("Kullanıcı oluşturuldu."));
      await loadUsers();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const handleUpdate = async (userId: string) => {
    const draft = drafts[userId];
    if (!draft) {
      return;
    }

    setSaving(true);
    setError(null);
    setNotice(null);

    try {
      await adminRequest(
        settings,
        `/auth/users/${userId}`,
        {
          method: "PUT",
          body: JSON.stringify({
            tenantId: settings.tenantId,
            role: draft.role,
            status: draft.status,
            password: draft.password || undefined,
          }),
          authStrategy: "auto",
        },
        "application/json"
      );

      setDrafts((prev) => ({
        ...prev,
        [userId]: { ...prev[userId], password: "" },
      }));
      setNotice(t("Kullanıcı güncellendi."));
      await loadUsers();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-8">
      <PageHeader
        title={t("Kullanıcılar")}
        subtitle={t("Kiracı ekip üyelerini ve rolleri yönetin.")}
        action={
          <button className="btn-outline" onClick={loadUsers} disabled={!canLoad || loading}>
            {t("Yenile")}
          </button>
        }
      />

      {!canLoad && (
        <EmptyState
          title={t("Admin API'yi bağlayın")}
          description={t("Kullanıcıları yüklemek için API anahtarı ekleyin veya giriş yapın.")}
        />
      )}

      {canLoad && needsTenant && !settings.tenantId && (
        <EmptyState
          title={t("Kiracı bağlamı eksik")}
          description={t("Kiracı kullanıcılarını yönetmek için kiracı id belirleyin.")}
        />
      )}

      {canLoad && (!needsTenant || settings.tenantId) && (
        <>
          <div className="card p-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="label">{t("Oluştur")}</p>
                <h2 className="font-display text-2xl">{t("Yeni kullanıcı ekle")}</h2>
              </div>
              {!canManage && (
                <span className="pill">{t("Salt okuma")}</span>
              )}
            </div>

            <div className="mt-6 grid gap-4 md:grid-cols-4">
              <input
                className="input"
                placeholder={t("E-posta")}
                value={form.email}
                onChange={(event) => setForm((prev) => ({ ...prev, email: event.target.value }))}
                disabled={!canManage}
              />
              <input
                className="input"
                placeholder={t("Ad")}
                value={form.name}
                onChange={(event) => setForm((prev) => ({ ...prev, name: event.target.value }))}
                disabled={!canManage}
              />
              <select
                className="input"
                value={form.role}
                onChange={(event) => setForm((prev) => ({ ...prev, role: event.target.value }))}
                disabled={!canManage}
              >
                {roleOptions.map((role) => (
                  <option key={role} value={role}>
                    {t(roleLabels[role])}
                  </option>
                ))}
              </select>
              <input
                className="input"
                type="password"
                placeholder={t("Geçici şifre")}
                value={form.password}
                onChange={(event) => setForm((prev) => ({ ...prev, password: event.target.value }))}
                disabled={!canManage}
              />
            </div>

            <div className="mt-4">
              <button className="btn-primary" onClick={handleCreate} disabled={!canManage || saving}>
                {saving ? t("Kaydediliyor...") : t("Kullanıcı oluştur")}
              </button>
            </div>
          </div>

          <div className="card p-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="label">{t("Ekip")}</p>
                <h2 className="font-display text-2xl">{t("Mevcut kullanıcılar")}</h2>
              </div>
              <div className="text-xs text-[var(--muted)]">
                {loading ? t("Yükleniyor...") : t("{count} kullanıcı", { count: users.length })}
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {users.length === 0 && (
                <div className="text-sm text-[var(--muted)]">{t("Kullanıcı bulunamadı.")}</div>
              )}

              {users.map((user) => {
                const draft = drafts[user.id] ?? { role: user.role, status: user.status, password: "" };
                return (
                  <div key={user.id} className="card-soft space-y-3 p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <div className="font-medium text-[var(--ink)]">{user.name}</div>
                        <div className="text-xs text-[var(--muted)]">{user.email}</div>
                        <div className="text-xs text-[var(--muted)]">
                          {t("Son giriş: {value}", {
                            value: user.lastLoginAt ? formatDate(user.lastLoginAt) : t("Hiç"),
                          })}
                        </div>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <select
                          className="input h-9 w-32 text-xs"
                          value={draft.role}
                          onChange={(event) =>
                            setDrafts((prev) => ({
                              ...prev,
                              [user.id]: { ...draft, role: event.target.value },
                            }))
                          }
                          disabled={!canManage}
                        >
                          {roleOptions.map((role) => (
                            <option key={role} value={role}>
                              {t(roleLabels[role])}
                            </option>
                          ))}
                        </select>
                        <select
                          className="input h-9 w-32 text-xs"
                          value={draft.status}
                          onChange={(event) =>
                            setDrafts((prev) => ({
                              ...prev,
                              [user.id]: { ...draft, status: event.target.value },
                            }))
                          }
                          disabled={!canManage}
                        >
                          {statusOptions.map((status) => (
                            <option key={status} value={status}>
                              {t(statusLabels[status])}
                            </option>
                          ))}
                        </select>
                        <input
                          className="input h-9 w-36 text-xs"
                          type="password"
                          placeholder={t("Yeni şifre")}
                          value={draft.password}
                          onChange={(event) =>
                            setDrafts((prev) => ({
                              ...prev,
                              [user.id]: { ...draft, password: event.target.value },
                            }))
                          }
                          disabled={!canManage}
                        />
                        <button
                          className="btn-outline h-9 px-4 text-xs"
                          onClick={() => handleUpdate(user.id)}
                          disabled={!canManage || saving}
                        >
                          {t("Kaydet")}
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {notice && (
            <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700">
              {notice}
            </div>
          )}
          {error && (
            <div className="rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
              {error}
            </div>
          )}
        </>
      )}
    </div>
  );
}
