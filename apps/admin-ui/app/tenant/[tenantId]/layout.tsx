"use client";

import { useEffect } from "react";
import { Providers } from "@/app/providers";
import { MobileNav } from "@/components/MobileNav";
import { RequireAuth } from "@/components/RequireAuth";
import { Sidebar } from "@/components/Sidebar";
import { Topbar } from "@/components/Topbar";
import { tenantNavItems } from "@/lib/nav-items";
import { useAdminSettings } from "@/lib/admin-settings";

export default function TenantPanelLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: { tenantId: string };
}) {
  const { tenantId } = params;
  const { updateSettings, isReady } = useAdminSettings();

  // Sync route tenantId to settings
  useEffect(() => {
    if (isReady && tenantId) {
      updateSettings({ tenantId, mode: "tenant" });
    }
  }, [isReady, tenantId, updateSettings]);

  // Sidebar will handle tenantId from useParams, so we can pass base items
  return (
    <Providers scope="tenant">
      <RequireAuth panel="tenant">
        <div className="app-shell">
          <Sidebar panel="tenant" items={tenantNavItems} />
          <div className="flex min-h-screen flex-1 flex-col">
            <Topbar panel="tenant" />
            <main className="flex-1 px-6 pb-14 pt-6 md:px-10">
              {children}
            </main>
          </div>
        </div>
        <MobileNav items={tenantNavItems} panel="tenant" />
      </RequireAuth>
    </Providers>
  );
}
