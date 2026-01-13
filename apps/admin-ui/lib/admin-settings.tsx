"use client";

import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import { normalizeBaseUrl, normalizeOptionalBaseUrl } from "./utils";
import type { AdminRole } from "./roles";

const STORAGE_KEY = "embeddra_admin_settings";
const UI_STORAGE_KEY = "embeddra_admin_ui_settings";

type SettingsScope = "tenant" | "platform" | "shared";

export type AdminMode = "tenant" | "platform";
export type Locale = "tr" | "en";
export type ThemeMode = "light" | "dark";

export type TenantPreset = {
  name: string;
  tenantId: string;
};

export type AdminSettings = {
  apiBaseUrl: string;
  apiKey: string;
  authToken: string;
  authExpiresAt: string;
  userEmail: string;
  userName: string;
  searchApiBaseUrl: string;
  searchApiKey: string;
  tenantId: string;
  actor: string;
  mode: AdminMode;
  role: AdminRole;
  observabilityUrl: string;
  tenantPresets: TenantPreset[];
  locale: Locale;
  theme: ThemeMode;
};

const defaultSettings: AdminSettings = {
  apiBaseUrl: process.env.NEXT_PUBLIC_ADMIN_API_BASE_URL || "http://localhost:5114",
  apiKey: "",
  authToken: "",
  authExpiresAt: "",
  userEmail: "",
  userName: "",
  searchApiBaseUrl: process.env.NEXT_PUBLIC_SEARCH_API_BASE_URL || "http://localhost:5222",
  searchApiKey: "",
  tenantId: "",
  actor: "admin-ui",
  mode: "tenant",
  role: "owner",
  observabilityUrl: "http://localhost:5601",
  tenantPresets: [],
  locale: "tr",
  theme: "light",
};

type AdminSettingsContextValue = {
  settings: AdminSettings;
  isReady: boolean;
  updateSettings: (next: Partial<AdminSettings>) => void;
};

const AdminSettingsContext = createContext<AdminSettingsContextValue | null>(null);

type AdminSettingsProviderProps = {
  children: React.ReactNode;
  scope?: SettingsScope;
};

export function AdminSettingsProvider({ children, scope = "shared" }: AdminSettingsProviderProps) {
  const [settings, setSettings] = useState<AdminSettings>(defaultSettings);
  const [isReady, setIsReady] = useState(false);
  const storageKey = resolveStorageKey(scope);

  useEffect(() => {
    const raw = window.localStorage.getItem(storageKey);
    const fallbackRaw = storageKey === STORAGE_KEY ? null : window.localStorage.getItem(STORAGE_KEY);
    const sourceRaw = raw ?? fallbackRaw;
    const sourceKey = raw ? storageKey : fallbackRaw ? STORAGE_KEY : null;
    const uiRaw = window.localStorage.getItem(UI_STORAGE_KEY);
    const uiPrefs = parseUiPrefs(uiRaw);
    if (sourceRaw) {
      try {
        const parsed = JSON.parse(sourceRaw) as Partial<AdminSettings>;
        setSettings((current) => ({
          ...current,
          ...parsed,
          // Always use env variable for apiBaseUrl (security: don't trust localStorage)
          apiBaseUrl: normalizeBaseUrl(
            process.env.NEXT_PUBLIC_ADMIN_API_BASE_URL || 
            parsed.apiBaseUrl || 
            current.apiBaseUrl
          ),
          searchApiBaseUrl: normalizeBaseUrl(
            process.env.NEXT_PUBLIC_SEARCH_API_BASE_URL || 
            (parsed.searchApiBaseUrl ?? current.searchApiBaseUrl)
          ),
          observabilityUrl: normalizeOptionalBaseUrl(parsed.observabilityUrl ?? current.observabilityUrl),
          tenantPresets: normalizeTenantPresets(parsed.tenantPresets, current.tenantPresets),
          locale: normalizeLocale(uiPrefs.locale ?? parsed.locale ?? current.locale),
          theme: normalizeTheme(uiPrefs.theme ?? parsed.theme ?? current.theme),
          mode: normalizeMode(scope, parsed.mode ?? current.mode),
        }));
      } catch {
        if (sourceKey) {
          window.localStorage.removeItem(sourceKey);
        }
      }
    } else {
      setSettings((current) => ({
        ...current,
        locale: normalizeLocale(uiPrefs.locale ?? current.locale),
        theme: normalizeTheme(uiPrefs.theme ?? current.theme),
        mode: normalizeMode(scope, current.mode),
      }));
    }

    setIsReady(true);
  }, [scope, storageKey]);

  useEffect(() => {
    if (!isReady || typeof document === "undefined") {
      return;
    }

    const root = document.documentElement;
    root.lang = settings.locale;
    root.dataset.theme = settings.theme;
    if (scope === "shared") {
      delete root.dataset.panel;
    } else {
      root.dataset.panel = scope;
    }
  }, [isReady, scope, settings.locale, settings.theme]);

  const updateSettings = (next: Partial<AdminSettings>) => {
    setSettings((current) => {
      const merged = {
        ...current,
        ...next,
      };
      const normalized = {
        ...merged,
        // Always use env variable for apiBaseUrl (security: don't trust client input)
        apiBaseUrl: normalizeBaseUrl(
          process.env.NEXT_PUBLIC_ADMIN_API_BASE_URL || 
          merged.apiBaseUrl || 
          defaultSettings.apiBaseUrl
        ),
        searchApiBaseUrl: normalizeBaseUrl(
          process.env.NEXT_PUBLIC_SEARCH_API_BASE_URL || 
          (merged.searchApiBaseUrl || defaultSettings.searchApiBaseUrl)
        ),
        observabilityUrl: normalizeOptionalBaseUrl(merged.observabilityUrl),
        tenantPresets: normalizeTenantPresets(merged.tenantPresets, []),
        locale: normalizeLocale(merged.locale),
        theme: normalizeTheme(merged.theme),
      };
      const enforced = {
        ...normalized,
        mode: normalizeMode(scope, normalized.mode),
      };
      window.localStorage.setItem(storageKey, JSON.stringify(enforced));
      
      // If authToken or authExpiresAt is being updated, also save to shared storage AND all scope-specific storages
      // This ensures token is available across all scopes (platform, tenant, shared)
      if (next.authToken !== undefined || next.authExpiresAt !== undefined || next.userEmail !== undefined || next.userName !== undefined || next.role !== undefined) {
        const authData = {
          authToken: enforced.authToken,
          authExpiresAt: enforced.authExpiresAt,
          userEmail: enforced.userEmail,
          userName: enforced.userName,
          role: enforced.role,
          tenantId: enforced.tenantId,
          mode: enforced.mode,
        };
        
        // Save to shared storage
        const sharedRaw = window.localStorage.getItem(STORAGE_KEY);
        const sharedData = sharedRaw ? { ...JSON.parse(sharedRaw), ...authData } : authData;
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(sharedData));
        
        // Also save to platform and tenant storages if they exist
        const platformKey = `${STORAGE_KEY}_platform`;
        const tenantKey = `${STORAGE_KEY}_tenant`;
        [platformKey, tenantKey].forEach((key) => {
          const existingRaw = window.localStorage.getItem(key);
          if (existingRaw) {
            try {
              const existing = JSON.parse(existingRaw);
              window.localStorage.setItem(key, JSON.stringify({ ...existing, ...authData }));
            } catch {
              // Ignore parse errors
            }
          }
        });
      }
      
      window.localStorage.setItem(
        UI_STORAGE_KEY,
        JSON.stringify({ locale: enforced.locale, theme: enforced.theme })
      );
      return enforced;
    });
  };

  const value = useMemo(
    () => ({
      settings,
      isReady,
      updateSettings,
    }),
    [settings, isReady]
  );

  return (
    <AdminSettingsContext.Provider value={value}>
      {children}
    </AdminSettingsContext.Provider>
  );
}

export function useAdminSettings() {
  const context = useContext(AdminSettingsContext);
  if (!context) {
    throw new Error("useAdminSettings must be used within AdminSettingsProvider");
  }

  return context;
}

function normalizeLocale(value: unknown): Locale {
  return value === "en" ? "en" : "tr";
}

function normalizeTheme(value: unknown): ThemeMode {
  return value === "dark" ? "dark" : "light";
}

function normalizeMode(scope: SettingsScope, value: unknown): AdminMode {
  if (scope === "platform") {
    return "platform";
  }
  if (scope === "tenant") {
    return "tenant";
  }
  return value === "platform" ? "platform" : "tenant";
}

function resolveStorageKey(scope: SettingsScope) {
  if (scope === "platform") {
    return `${STORAGE_KEY}_platform`;
  }
  if (scope === "tenant") {
    return `${STORAGE_KEY}_tenant`;
  }
  return STORAGE_KEY;
}

function parseUiPrefs(raw: string | null) {
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as Partial<Pick<AdminSettings, "locale" | "theme">>;
    return {
      locale: parsed.locale,
      theme: parsed.theme,
    };
  } catch {
    return {};
  }
}

function normalizeTenantPresets(
  value: unknown,
  fallback: TenantPreset[]
): TenantPreset[] {
  if (!Array.isArray(value)) {
    return fallback;
  }

  const presets = value
    .map((item) => {
      if (!item || typeof item !== "object") {
        return null;
      }

      const record = item as { name?: unknown; tenantId?: unknown };
      const tenantId = typeof record.tenantId === "string" ? record.tenantId.trim() : "";
      if (!tenantId) {
        return null;
      }

      const name = typeof record.name === "string" ? record.name.trim() : "";
      return { tenantId, name: name || tenantId };
    })
    .filter((item): item is TenantPreset => item !== null);

  return presets;
}
