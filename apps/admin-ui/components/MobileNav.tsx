"use client";

import Link from "next/link";
import { usePathname, useParams } from "next/navigation";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import type { NavItem } from "@/lib/nav-items";
import { tenantNavItems } from "@/lib/nav-items";

type MobileNavProps = {
  items: NavItem[];
  panel?: "tenant" | "platform";
};

export function MobileNav({ items: initialItems, panel }: MobileNavProps) {
  const pathname = usePathname();
  const params = useParams();
  const { settings } = useAdminSettings();
  const { t } = useI18n();

  // For tenant panel, get tenantId from route params and rebuild items
  const items = panel === "tenant" && params?.tenantId
    ? tenantNavItems.map((item) => ({
        ...item,
        href: item.href.startsWith("/tenant")
          ? item.href.replace("/tenant", `/tenant/${params.tenantId}`)
          : item.href,
      }))
    : initialItems;

  const visibleItems = items.filter((item) => !item.roles || item.roles.includes(settings.role));

  return (
    <nav className="fixed bottom-4 left-1/2 z-30 flex w-[92%] -translate-x-1/2 gap-2 rounded-full border border-[var(--border)] bg-[var(--surface)]/95 px-3 py-2 shadow-lg backdrop-blur lg:hidden">
      {visibleItems.map((item) => {
        const isRoot = item.href === "/tenant" || item.href === "/platform" || (item.href.startsWith("/tenant/") && item.href.split("/").length === 3);
        const isActive = isRoot ? pathname === item.href : pathname.startsWith(item.href);
        return (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "flex-1 rounded-full px-3 py-2 text-center text-xs font-medium",
              isActive ? "bg-[var(--bg-strong)] text-[var(--ink)]" : "text-[var(--muted)]"
            )}
          >
            {t(item.shortLabel ?? item.label)}
          </Link>
        );
      })}
    </nav>
  );
}
