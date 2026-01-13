"use client";

import Link from "next/link";
import { usePathname, useParams } from "next/navigation";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { roleLabels } from "@/lib/roles";
import { cn } from "@/lib/utils";
import type { NavItem } from "@/lib/nav-items";
import { tenantNavItems } from "@/lib/nav-items";

type SidebarProps = {
  panel: "tenant" | "platform";
  items: NavItem[];
};

export function Sidebar({ panel, items: initialItems }: SidebarProps) {
  const pathname = usePathname();
  const params = useParams();
  const { settings } = useAdminSettings();
  const { t } = useI18n();
  const panelLabel = panel === "platform" ? t("Platform paneli") : t("Tenant paneli");
  const roleLabel = t(roleLabels[settings.role]);

  // For tenant panel, get tenantId from route params and rebuild items
  const items = panel === "tenant" && params?.tenantId
    ? tenantNavItems.map((item) => ({
        ...item,
        href: item.href.startsWith("/tenant")
          ? item.href.replace("/tenant", `/tenant/${params.tenantId}`)
          : item.href,
      }))
    : initialItems;

  return (
    <aside className="hidden w-64 flex-col gap-8 border-r border-[var(--border)] bg-[var(--surface)] px-6 py-8 lg:flex">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--accent)] text-white shadow-lg shadow-[color:var(--accent)]/30">
          <span className="font-display text-lg">E</span>
        </div>
        <div>
          <p className="font-display text-lg">Embeddra</p>
          <p className="text-xs text-[var(--muted)]">{panelLabel}</p>
        </div>
      </div>

      <div className="space-y-3">
        <p className="label">{t("Menü")}</p>
        <nav className="space-y-1">
          {items.map((item) => {
            const isRoot = item.href === "/platform" || (item.href.startsWith("/tenant/") && item.href.split("/").length === 3);
            const isActive = isRoot ? pathname === item.href : pathname.startsWith(item.href);
            const roleAllowed = !item.roles || item.roles.includes(settings.role);
            const isRestricted = !roleAllowed;

            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "flex items-center gap-3 rounded-2xl px-4 py-3 text-sm transition",
                  isActive
                    ? "bg-[var(--bg-strong)] text-[var(--ink)] shadow-inner"
                    : "text-[var(--muted)] hover:bg-[color:var(--surface-glass-strong)] hover:text-[var(--ink)]",
                  isRestricted && "pointer-events-none opacity-60"
                )}
              >
                <span className={cn(
                  "flex h-8 w-8 items-center justify-center rounded-xl",
                  isActive ? "bg-[color:var(--surface-glass-strong)]" : "bg-[color:var(--surface-glass)]"
                )}>
                  {renderIcon(item.icon ?? "menu")}
                </span>
                <span className="flex-1">{t(item.label)}</span>
                {isRestricted && (
                  <span className="pill border-none bg-[var(--bg-strong)] text-[10px] uppercase tracking-[0.2em]">
                    {t("Kısıtlı")}
                  </span>
                )}
              </Link>
            );
          })}
        </nav>
      </div>

      {/* User info removed - now shown in topbar avatar dropdown */}
    </aside>
  );
}

function renderIcon(name: string) {
  switch (name) {
    case "grid":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <rect x="3" y="3" width="7" height="7" rx="2" />
          <rect x="14" y="3" width="7" height="7" rx="2" />
          <rect x="14" y="14" width="7" height="7" rx="2" />
          <rect x="3" y="14" width="7" height="7" rx="2" />
        </svg>
      );
    case "upload":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M12 3v12" />
          <path d="M7 8l5-5 5 5" />
          <path d="M4 21h16" />
        </svg>
      );
    case "chart":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M4 20h16" />
          <path d="M7 16v-6" />
          <path d="M12 16V8" />
          <path d="M17 16v-9" />
        </svg>
      );
    case "tune":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M4 7h9" />
          <path d="M4 17h13" />
          <circle cx="17" cy="7" r="2" />
          <circle cx="9" cy="17" r="2" />
        </svg>
      );
    case "search":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <circle cx="11" cy="11" r="7" />
          <path d="M20 20l-3.5-3.5" />
        </svg>
      );
    case "key":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <circle cx="8" cy="15" r="4" />
          <path d="M11 15h10v4" />
          <path d="M18 15v-4" />
        </svg>
      );
    case "users":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <circle cx="9" cy="8" r="3" />
          <path d="M4 19c0-3 2.5-5 5-5" />
          <circle cx="17" cy="10" r="2.5" />
          <path d="M14 19c.2-2.2 1.8-3.8 4-4" />
        </svg>
      );
    case "shield":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M12 3l8 3v6c0 5-3.5 8.5-8 9-4.5-.5-8-4-8-9V6l8-3z" />
        </svg>
      );
    case "layers":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M12 3l9 5-9 5-9-5 9-5z" />
          <path d="M3 12l9 5 9-5" />
          <path d="M3 17l9 5 9-5" />
        </svg>
      );
    case "activity":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M4 13h4l2 5 4-12 2 7h4" />
        </svg>
      );
    case "code":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <polyline points="16 18 22 12 16 6" />
          <polyline points="8 6 2 12 8 18" />
        </svg>
      );
    case "slider":
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M4 7h16" />
          <path d="M4 17h16" />
          <circle cx="9" cy="7" r="2" />
          <circle cx="15" cy="17" r="2" />
        </svg>
      );
    default:
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
          <path d="M4 10h16" />
          <path d="M4 6h16" />
          <path d="M4 14h10" />
          <path d="M4 18h6" />
        </svg>
      );
  }
}
