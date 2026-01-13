"use client";

import { useEffect, useRef } from "react";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";

type RequireAuthProps = {
  panel: "tenant" | "platform";
  children: React.ReactNode;
};

export function RequireAuth({ panel, children }: RequireAuthProps) {
  const { settings, updateSettings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const hasRedirectedRef = useRef(false);

  // MVP: Only use authToken for authentication (no API key support)
  // Also check token expiry
  const isTokenExpired = settings.authExpiresAt 
    ? new Date(settings.authExpiresAt) <= new Date()
    : false;
  const isAuthed = Boolean(settings.authToken) && !isTokenExpired;

  useEffect(() => {
    if (!isReady || hasRedirectedRef.current) return;
    
    if (!isAuthed) {
      hasRedirectedRef.current = true;
      
      if (isTokenExpired && settings.authToken) {
        updateSettings({
          authToken: "",
          authExpiresAt: "",
        });
        fetch("/api/auth/logout", { method: "POST" }).catch(() => {});
      }
      
      window.location.href = "/login";
    }
  }, [isReady, isAuthed, isTokenExpired, settings.authToken, settings.authExpiresAt, updateSettings]);

  if (!isReady) {
    return (
      <div className="flex min-h-screen items-center justify-center p-8 text-sm text-[var(--muted)]">
        {t("Yükleniyor...")}
      </div>
    );
  }

  if (!isAuthed) {
    return null;
  }

  return <>{children}</>;
}
