"use client";

import { Suspense, useEffect, useState, useRef } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";

function LoginForm() {
  const { settings, updateSettings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const router = useRouter();
  const searchParams = useSearchParams();

  // Get tenantId from query params (if coming from tenant select)
  const tenantIdFromQuery = searchParams.get("tenantId");
  const emailFromSettings = settings.userEmail || "";

  const [email, setEmail] = useState(emailFromSettings);
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Clear expired tokens on mount, but don't auto-redirect
  // Auto-redirect causes infinite loops - let middleware handle it
  useEffect(() => {
    if (!isReady) return;

    // Check if token exists and is expired
    const isTokenExpired = settings.authExpiresAt 
      ? new Date(settings.authExpiresAt) <= new Date()
      : false;
    const hasTokenInStorage = Boolean(settings.authToken);

    // If token exists but expired, clear it immediately
    if (hasTokenInStorage && isTokenExpired) {
      updateSettings({
        authToken: "",
        authExpiresAt: "",
      });
      // Clear cookie
      fetch("/api/auth/logout", { method: "POST" }).catch(() => {});
    }
  }, [isReady, settings.authExpiresAt, settings.authToken, updateSettings]);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password.trim()) {
      setError(t("E-posta ve şifre gerekli."));
      return;
    }

    setLoading(true);
    setError(null);

    try {
      // Only include tenantId if coming from tenant select (query param)
      // Otherwise, backend will search across all tenants/platform
      const tenantId = tenantIdFromQuery || null;
      
      // Login request - apiBaseUrl is handled server-side (env variable)
      const loginResponse = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          // Only send tenantId if explicitly provided (from tenant select)
          ...(tenantId && { tenantId }),
          email: email.trim(),
          password,
        }),
      });

      const loginBody = await loginResponse.json().catch(() => ({}));
      if (!loginResponse.ok) {
        throw new Error(loginBody.error || loginBody.message || t("Giriş başarısız."));
      }

      // Handle login response based on backend response structure
      // Backend returns either:
      // 1. { token, user, redirect_hint: "platform" | "tenant" } - Single user login
      // 2. { tenants: [...], redirect_hint: "tenant_select" } - Multi-tenant scenario

      // Case 1: Multi-tenant scenario - show tenant selection
      if (loginBody.tenants && Array.isArray(loginBody.tenants) && loginBody.tenants.length > 1) {
        updateSettings({
          authToken: "", // No token yet, will get after tenant selection
          userEmail: email.trim(), // Store email for tenant select flow
          tenantPresets: loginBody.tenants.map((t: any) => ({
            tenantId: t.tenant_id,
            name: t.tenant_name || t.tenant_id
          }))
        });
        router.push("/tenant/select");
        return;
      }

      // Case 2: Single tenant found in multi-tenant response - auto-select
      if (loginBody.tenants && Array.isArray(loginBody.tenants) && loginBody.tenants.length === 1) {
        const tid = loginBody.tenants[0].tenant_id;
        // Re-login with selected tenantId
        updateSettings({ tenantId: tid, userEmail: email.trim() });
        router.push(`/login?tenantId=${encodeURIComponent(tid)}`);
        return;
      }

      // Case 3: Single user login - token returned
      if (loginBody.token && loginBody.user) {
        const user = loginBody.user;
        const tenantId = user.tenant_id || null;
        const role = user.role || "";
        const redirectHint = loginBody.redirect_hint || (tenantId ? "tenant" : "platform");
        
        updateSettings({
          authToken: loginBody.token,
          authExpiresAt: loginBody.expires_at || "",
          userEmail: user.email || email.trim(),
          userName: user.name || "",
          tenantId: tenantId || "",
          role: role === "PlatformOwner" || role === "platform_owner" ? "platform_owner" : "owner",
          mode: tenantId ? "tenant" : "platform",
          tenantPresets: tenantId ? [{ tenantId, name: tenantId }] : [],
        });

        setTimeout(() => {
          if (redirectHint === "platform" || !tenantId || role === "PlatformOwner" || role === "platform_owner") {
            window.location.href = "/platform";
          } else if (redirectHint === "tenant" && tenantId) {
            window.location.href = `/tenant/${tenantId}`;
          } else {
            setError(t("Yönlendirme bilgisi alınamadı."));
          }
        }, 300);
        return;
      }

      // Case 4: Unexpected response
      throw new Error(t("Giriş başarısız. Lütfen bilgilerinizi kontrol edin."));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center px-6 py-12">
      <div className="mx-auto w-full max-w-md">
        <div className="mb-8 text-center">
          <div className="mb-4 flex justify-center">
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--accent)] text-white shadow-[0_14px_30px_rgba(180,91,60,0.28)]">
              <span className="font-display text-2xl">E</span>
            </div>
          </div>
          <h1 className="font-display text-3xl font-bold tracking-tight text-[var(--ink)]">
            {t("Giriş Yap")}
          </h1>
          <p className="mt-2 text-[var(--muted)]">
            {t("Embeddra yönetim paneline hoş geldiniz.")}
          </p>
        </div>

        <div className="card overflow-hidden border border-[var(--border)] bg-[var(--surface)] p-8 shadow-xl">
          <form onSubmit={handleLogin} className="space-y-6">
            <div className="space-y-2">
              <label className="text-sm font-medium text-[var(--ink)]" htmlFor="email">
                {t("E-posta")}
              </label>
              <input
                id="email"
                type="email"
                className="input w-full"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="ornek@embeddra.com"
                required
                autoComplete="email"
                autoFocus={!tenantIdFromQuery}
                disabled={!!tenantIdFromQuery && !!emailFromSettings}
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-[var(--ink)]" htmlFor="password">
                {t("Şifre")}
              </label>
              <input
                id="password"
                type="password"
                className="input w-full"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                required
                autoComplete="current-password"
              />
            </div>

            {error && (
              <div className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700 animate-in fade-in slide-in-from-top-1">
                {error}
              </div>
            )}

            <button
              type="submit"
              className="btn-primary w-full py-3 text-base font-semibold transition-all hover:scale-[1.02] active:scale-[0.98]"
              disabled={loading}
            >
              {loading ? t("Giriş yapılıyor...") : t("Giriş Yap")}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}

export default function LoginPage() {
  return (
    <Suspense fallback={
      <div className="flex min-h-screen items-center justify-center">
        <div className="text-sm text-[var(--muted)]">Yükleniyor...</div>
      </div>
    }>
      <LoginForm />
    </Suspense>
  );
}
