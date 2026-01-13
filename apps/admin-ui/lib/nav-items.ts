import type { AdminRole } from "@/lib/roles";

export type NavItem = {
  label: string;
  href: string;
  icon?: string;
  roles?: AdminRole[];
  shortLabel?: string;
};

export const tenantNavItems: NavItem[] = [
  { label: "Gösterge Paneli", href: "/tenant", icon: "grid", shortLabel: "Panel" },
  { label: "Entegrasyon Merkezi", href: "/tenant/integration", icon: "code", shortLabel: "Entegrasyon" },
  { label: "Katalog / Aktarımlar", href: "/tenant/catalog/imports", icon: "upload", roles: ["owner", "admin"], shortLabel: "Aktarım" },
  { label: "Analitik", href: "/tenant/analytics", icon: "chart" },
  { label: "Güvenlik", href: "/tenant/security/allowed-origins", icon: "shield", roles: ["owner", "admin"], shortLabel: "Güvenlik" },
  // Users removed in MVP - first owner created from platform panel
];

export const platformNavItems: NavItem[] = [
  { label: "Dashboard", href: "/platform", icon: "grid", shortLabel: "Panel" },
  { label: "Kiracılar", href: "/platform/tenants", icon: "layers" },
  { label: "Denetim Kayıtları", href: "/platform/audit", icon: "activity", shortLabel: "Denetim" },
  // Settings removed in MVP - redirects to dashboard
];
