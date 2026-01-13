"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAdminSettings, TenantPreset } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";

export default function TenantSelectPage() {
    const { settings, updateSettings, isReady } = useAdminSettings();
    const { t } = useI18n();
    const router = useRouter();
    const [search, setSearch] = useState("");
    const [loading, setLoading] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);

    const filteredTenants = settings.tenantPresets.filter((t) =>
        t.name.toLowerCase().includes(search.toLowerCase()) ||
        t.tenantId.toLowerCase().includes(search.toLowerCase())
    );

    const handleSelect = (tenantId: string) => {
        // If we already have a token, just switch tenant
        if (settings.authToken) {
            updateSettings({ tenantId, mode: "tenant" });
            router.push(`/tenant/${tenantId}`);
            return;
        }

        // No token - redirect to login with tenantId as query param
        // Login page will pre-fill tenantId and only ask for password
        updateSettings({ tenantId });
        router.push(`/login?tenantId=${encodeURIComponent(tenantId)}`);
    };

    if (!isReady) {
        return (
            <div className="flex min-h-screen items-center justify-center p-8 text-sm text-[var(--muted)]">
                {t("Yükleniyor...")}
            </div>
        );
    }

    // If no tenant presets, redirect to login
    if (settings.tenantPresets.length === 0) {
        router.push("/login");
        return null;
    }

    return (
        <div className="mx-auto max-w-2xl space-y-8 py-12 px-4">
            <div className="text-center">
                <h1 className="font-display text-4xl font-bold tracking-tight text-[var(--ink)]">
                    {t("Kiracı Seçimi")}
                </h1>
                <p className="mt-3 text-lg text-[var(--muted)]">
                    {t("Devam etmek için erişiminiz olan bir kiracı seçin.")}
                </p>
            </div>

            <div className="card overflow-hidden border border-[var(--border)] bg-[var(--surface)] shadow-2xl">
                <div className="border-b border-[var(--border)] p-6">
                    <div className="relative">
                        <input
                            type="text"
                            className="input w-full pl-10"
                            placeholder={t("Kiracı ara...")}
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                        <div className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--muted)]">
                            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="11" cy="11" r="8" /><path d="m21 21-4.3-4.3" /></svg>
                        </div>
                    </div>
                </div>

                <div className="max-h-[400px] overflow-y-auto p-2">
                    {filteredTenants.length > 0 ? (
                        <div className="grid gap-2">
                            {filteredTenants.map((tenant) => (
                                <button
                                    key={tenant.tenantId}
                                    onClick={() => handleSelect(tenant.tenantId)}
                                    disabled={loading === tenant.tenantId}
                                    className="flex items-center justify-between rounded-xl p-4 text-left transition-all hover:bg-[var(--accent-hover)] hover:translate-x-1 group disabled:opacity-50 disabled:cursor-not-allowed"
                                >
                                    <div>
                                        <div className="font-semibold text-[var(--ink)] group-hover:text-[var(--accent)]">
                                            {tenant.name}
                                        </div>
                                        <div className="text-xs text-[var(--muted)]">
                                            ID: {tenant.tenantId}
                                        </div>
                                    </div>
                                    <div className="text-[var(--muted)] opacity-0 group-hover:opacity-100 transition-opacity">
                                        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="m9 18 6-6-6-6" /></svg>
                                    </div>
                                </button>
                            ))}
                        </div>
                    ) : (
                        <div className="py-12 text-center text-[var(--muted)]">
                            {t("Erişilebilir kiracı bulunamadı.")}
                        </div>
                    )}
                </div>

                {error && (
                    <div className="border-t border-[var(--border)] bg-rose-50 p-4">
                        <div className="text-sm text-rose-700">{error}</div>
                    </div>
                )}

                <div className="border-t border-[var(--border)] bg-[var(--background)] p-4 text-center">
                    <Link
                        href="/login"
                        className="text-sm font-medium text-[var(--muted)] hover:text-[var(--ink)] transition-colors"
                    >
                        ← {t("Farklı bir hesapla giriş yap")}
                    </Link>
                </div>
            </div>
        </div>
    );
}
