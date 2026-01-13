"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useI18n } from "@/lib/i18n";
import { ROUTES } from "@/lib/constants";
import { cn } from "@/lib/utils";

type ApiKeySummary = {
  id: string;
  name: string;
  keyPrefix: string;
  keyType?: string;
  type?: string;
  status: string;
};

type IntegrationContextBarProps = {
  apiKeys: ApiKeySummary[];
  selectedApiKeyId: string | null;
  onApiKeyChange: (keyId: string | null) => void;
  primaryAction?: {
    label: string;
    onClick: () => void;
    disabled?: boolean;
    loading?: boolean;
  };
  showManageLink?: boolean;
};

export function IntegrationContextBar({
  apiKeys,
  selectedApiKeyId,
  onApiKeyChange,
  primaryAction,
  showManageLink = true,
}: IntegrationContextBarProps) {
  const params = useParams();
  const tenantId = params.tenantId as string;
  const { t } = useI18n();
  const [isManageOpen, setIsManageOpen] = useState(false);

  const activeSearchKeys = apiKeys.filter(
    (k) =>
      (k.keyType === "search_public" || k.type === "search_public") &&
      k.status === "active"
  );

  return (
    <div className="sticky top-0 z-30 border-b border-[var(--border)] bg-[color:var(--surface)]/95 backdrop-blur">
      <div className="flex items-center justify-between gap-4 px-6 py-4">
        {/* Left: API Key Selector */}
        <div className="flex flex-1 items-center gap-4">
          <div className="flex-1 max-w-md">
            <label className="label text-xs mb-1">{t("API Anahtarı")}</label>
            {activeSearchKeys.length === 0 ? (
              <div className="flex items-center gap-2">
                <p className="text-sm text-[var(--muted)]">
                  {t("API anahtarı gerekli")}
                </p>
                {showManageLink && (
                  <Link
                    href={ROUTES.TENANT_SECURITY_API_KEYS(tenantId)}
                    className="btn-primary text-xs"
                  >
                    {t("Oluştur")}
                  </Link>
                )}
              </div>
            ) : (
              <select
                className="input w-full text-sm"
                value={selectedApiKeyId ?? ""}
                onChange={(e) => onApiKeyChange(e.target.value || null)}
                aria-label={t("API Anahtarı seçin")}
              >
                {activeSearchKeys.map((key) => (
                  <option key={key.id} value={key.id}>
                    {key.name} ({key.keyPrefix})
                  </option>
                ))}
              </select>
            )}
          </div>

          {showManageLink && activeSearchKeys.length > 0 && (
            <div className="relative">
              <Link
                href={ROUTES.TENANT_SECURITY_API_KEYS(tenantId)}
                className="btn-ghost text-sm"
                onMouseEnter={() => setIsManageOpen(true)}
                onMouseLeave={() => setIsManageOpen(false)}
              >
                {t("Yönet")}
              </Link>
            </div>
          )}
        </div>

        {/* Right: Primary Action */}
        {primaryAction && (
          <div className="flex items-center gap-3">
            <button
              className={cn(
                "btn-primary",
                primaryAction.loading && "opacity-50 cursor-not-allowed"
              )}
              onClick={primaryAction.onClick}
              disabled={primaryAction.disabled || primaryAction.loading}
              aria-label={primaryAction.label}
            >
              {primaryAction.loading ? t("Yükleniyor...") : primaryAction.label}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
