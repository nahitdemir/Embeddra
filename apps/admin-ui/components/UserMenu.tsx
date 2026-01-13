"use client";

import { useState, useRef, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { roleLabels } from "@/lib/roles";
import { cn } from "@/lib/utils";

type UserMenuProps = {
  panel: "tenant" | "platform";
};

export function UserMenu({ panel }: UserMenuProps) {
  const { settings, updateSettings } = useAdminSettings();
  const { t, locale, setLocale, theme, setTheme } = useI18n();
  const router = useRouter();
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Close menu when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
      return () => document.removeEventListener("mousedown", handleClickOutside);
    }
  }, [isOpen]);

  const handleLogout = async () => {
    try {
      await fetch("/api/auth/logout", { method: "POST" });
    } catch (error) {
      console.error("Logout API call failed:", error);
    }

    updateSettings({
      authToken: "",
      authExpiresAt: "",
      userEmail: "",
      userName: "",
      tenantId: "",
      role: "owner",
      tenantPresets: [],
      mode: "tenant",
    });

    router.push("/login");
    window.location.href = "/login";
  };

  const userName = settings.userName || settings.userEmail || t("Kullanıcı");
  const userEmail = settings.userEmail || "";
  const roleLabel = t(roleLabels[settings.role]);

  return (
    <div className="relative" ref={menuRef}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 rounded-xl px-3 py-2 text-sm transition-colors hover:bg-[var(--accent-hover)] focus:outline-none focus:ring-2 focus:ring-[var(--accent)] focus:ring-offset-2"
        aria-label={t("Kullanıcı menüsü")}
        aria-expanded={isOpen}
      >
        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--accent)] text-xs font-semibold text-white">
          {userName.charAt(0).toUpperCase()}
        </div>
        <div className="hidden text-left sm:block">
          <div className="text-xs font-medium text-[var(--ink)]">{userName}</div>
          {userEmail && (
            <div className="text-[10px] text-[var(--muted)]">{userEmail}</div>
          )}
        </div>
        <svg
          className={cn(
            "h-4 w-4 text-[var(--muted)] transition-transform",
            isOpen && "rotate-180"
          )}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M19 9l-7 7-7-7"
          />
        </svg>
      </button>

      {isOpen && (
        <div className="absolute right-0 top-full z-50 mt-2 w-64 rounded-2xl border border-[var(--border)] bg-[var(--surface)] shadow-2xl">
          {/* Header */}
          <div className="border-b border-[var(--border)] p-4">
            <div className="font-medium text-[var(--ink)]">{userName}</div>
            {userEmail && (
              <div className="mt-1 text-xs text-[var(--muted)]">{userEmail}</div>
            )}
            <div className="mt-2 text-[10px] uppercase tracking-wider text-[var(--muted)]">
              {roleLabel}
            </div>
          </div>

          {/* Language */}
          <div className="border-b border-[var(--border)] p-2">
            <label className="label mb-2 text-xs">{t("Dil")}</label>
            <select
              className="input w-full text-xs"
              value={locale}
              onChange={(e) => setLocale(e.target.value as typeof locale)}
            >
              <option value="tr">{t("Türkçe")}</option>
              <option value="en">{t("English")}</option>
            </select>
          </div>

          {/* Theme */}
          <div className="border-b border-[var(--border)] p-2">
            <label className="label mb-2 text-xs">{t("Tema")}</label>
            <div className="flex gap-2">
              <button
                className={cn(
                  "btn-outline flex-1 text-xs",
                  theme === "light" && "border-[var(--accent)] text-[var(--ink)]"
                )}
                onClick={() => setTheme("light")}
              >
                {t("Açık")}
              </button>
              <button
                className={cn(
                  "btn-outline flex-1 text-xs",
                  theme === "dark" && "border-[var(--accent)] text-[var(--ink)]"
                )}
                onClick={() => setTheme("dark")}
              >
                {t("Koyu")}
              </button>
            </div>
          </div>

          {/* Logout */}
          <div className="p-2">
            <button
              className="btn-ghost w-full text-xs text-rose-500 hover:text-rose-600 hover:bg-rose-50"
              onClick={handleLogout}
            >
              {t("Çıkış")}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
