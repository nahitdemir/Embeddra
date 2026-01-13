"use client";

import { AdminSettingsProvider } from "@/lib/admin-settings";

type ProvidersProps = {
  children: React.ReactNode;
  scope?: "tenant" | "platform" | "shared";
};

export function Providers({ children, scope = "shared" }: ProvidersProps) {
  return <AdminSettingsProvider scope={scope}>{children}</AdminSettingsProvider>;
}
