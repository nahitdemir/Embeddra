"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { UserMenu } from "@/components/UserMenu";

type TopbarProps = {
  panel: "tenant" | "platform";
};

type TenantSwitcherProps = {
  currentTenantId: string;
  onTenantChange: (tenantId: string) => void;
};

function TenantSwitcher({ currentTenantId, onTenantChange }: TenantSwitcherProps) {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [tenants, setTenants] = useState<Array<{ tenant_id: string; tenant_name: string }>>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!isReady || !settings.authToken || tenants.length > 0) return;

    // Fetch tenant list from /me/tenants endpoint
    const fetchTenants = async () => {
      try {
        setLoading(true);
        const apiBaseUrl = process.env.NEXT_PUBLIC_ADMIN_API_BASE_URL || "http://localhost:5114";
        const response = await fetch(`${apiBaseUrl}/auth/me/tenants`, {
          headers: {
            "Authorization": `Bearer ${settings.authToken}`,
          },
        });

        if (response.ok) {
          const data = await response.json();
          setTenants(data.tenants || []);
        }
      } catch (error) {
        console.error("Failed to fetch tenants:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchTenants();
  }, [isReady, settings.authToken, tenants.length]);

  // Use fresh tenants from API, fallback to tenantPresets
  const availableTenants = tenants.length > 0 
    ? tenants 
    : settings.tenantPresets.map(tp => ({ tenant_id: tp.tenantId, tenant_name: tp.name }));

  // Don't show switcher if only 1 tenant
  if (availableTenants.length <= 1) {
    return null;
  }

  return (
    <div className="relative">
      <select
        className="input h-9 appearance-none pr-8 text-xs font-medium text-[var(--ink)] hover:bg-[var(--accent-hover)] transition-colors cursor-pointer"
        value={currentTenantId}
        onChange={(e) => onTenantChange(e.target.value)}
        disabled={loading}
      >
        {availableTenants.map((tenant) => (
          <option key={tenant.tenant_id} value={tenant.tenant_id}>
            {tenant.tenant_name || tenant.tenant_id}
          </option>
        ))}
      </select>
      <span className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 text-[var(--muted)]">
        <svg width="12" height="12" viewBox="0 0 20 20" fill="currentColor">
          <path d="M5.4 7.2a1 1 0 0 1 1.4 0L10 10.4l3.2-3.2a1 1 0 1 1 1.4 1.4l-3.9 4a1 1 0 0 1-1.4 0l-3.9-4a1 1 0 0 1 0-1.4z" />
        </svg>
      </span>
    </div>
  );
}

function TenantSwitcherWrapper() {
  const { settings, updateSettings } = useAdminSettings();
  const router = useRouter();

  return (
    <TenantSwitcher 
      currentTenantId={settings.tenantId}
      onTenantChange={(tenantId) => {
        updateSettings({ tenantId });
        router.push(`/tenant/${tenantId}`);
      }}
    />
  );
}

export function Topbar({ panel }: TopbarProps) {
  const { t } = useI18n();

  return (
    <header className="sticky top-0 z-20 flex items-center justify-between border-b border-[var(--border)] bg-[color:var(--surface)]/90 px-6 py-4 backdrop-blur">
      {/* Left: Tenant Switcher (tenant panel only) */}
      <div className="flex items-center gap-4">
        {/* Tenant Switcher - Left side, only for tenant panel */}
        {panel === "tenant" && <TenantSwitcherWrapper />}
      </div>

      {/* Right: User Menu (Avatar Dropdown) */}
      <div className="flex items-center gap-3">
        <UserMenu panel={panel} />
      </div>
    </header>
  );
}
