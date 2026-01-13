"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useI18n } from "@/lib/i18n";
import { ROUTES } from "@/lib/constants";
import { EmptyState } from "@/components/EmptyState";
import { cn } from "@/lib/utils";

type OriginsManagerProps = {
  origins: string[];
  onOriginsChange: (origins: string[]) => void;
  onSave: () => Promise<void>;
  canManage: boolean;
  loading?: boolean;
  showStandaloneLink?: boolean;
};

export function OriginsManager({
  origins,
  onOriginsChange,
  onSave,
  canManage,
  loading = false,
  showStandaloneLink = true,
}: OriginsManagerProps) {
  const params = useParams();
  const tenantId = params.tenantId as string;
  const { t } = useI18n();
  const [input, setInput] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSaving, setIsSaving] = useState(false);

  const validateOrigin = (origin: string): string | null => {
    if (!origin.trim()) {
      return t("Origin boş olamaz");
    }

    // Basic URL validation
    try {
      const url = new URL(origin);
      if (!["http:", "https:"].includes(url.protocol)) {
        return t("Origin http veya https ile başlamalı");
      }
      // Allow wildcard for subdomains
      if (origin.includes("*") && !origin.match(/^https?:\/\/(\*\.)?[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*/)) {
        return t("Geçersiz wildcard formatı. Örnek: https://*.example.com");
      }
    } catch {
      // If URL parsing fails, check if it's a valid domain pattern
      if (!origin.match(/^https?:\/\/(\*\.)?[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*/)) {
        return t("Geçersiz origin formatı. Örnek: https://example.com");
      }
    }

    return null;
  };

  const handleAdd = () => {
    const trimmed = input.trim();
    if (!trimmed) {
      return;
    }

    const error = validateOrigin(trimmed);
    if (error) {
      setErrors({ input: error });
      return;
    }

    if (origins.includes(trimmed)) {
      setErrors({ input: t("Bu origin zaten eklenmiş") });
      return;
    }

    setErrors({});
    onOriginsChange([...origins, trimmed]);
    setInput("");
  };

  const handleRemove = (origin: string) => {
    onOriginsChange(origins.filter((o) => o !== origin));
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      await onSave();
    } finally {
      setIsSaving(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      e.preventDefault();
      handleAdd();
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <h3 className="font-display text-lg font-semibold text-[var(--ink)] mb-1">
          {t("İzinli Originler")}
        </h3>
        <p className="text-sm text-[var(--muted)]">
          {t("Arama widget'ını çağırabilecek domainleri ekleyin. Widget, bu origin'lerden gelen istekleri kabul eder.")}
        </p>
      </div>

      {origins.length === 0 && (
        <EmptyState
          title={t("Henüz origin eklenmemiş")}
          description={t("Widget'ın çalışması için en az bir origin eklemeniz gerekir. Origin eklemeden widget çalışmayacaktır.")}
        />
      )}

      <div className="space-y-3">
        {/* Add Origin Input */}
        <div className="flex flex-col gap-2 md:flex-row">
          <div className="flex-1">
            <input
              className={cn(
                "input w-full",
                errors.input && "border-rose-300 focus:border-rose-500 focus:ring-rose-200"
              )}
              placeholder="https://example.com veya https://*.example.com"
              value={input}
              onChange={(e) => {
                setInput(e.target.value);
                if (errors.input) {
                  setErrors({});
                }
              }}
              onKeyDown={handleKeyDown}
              disabled={!canManage || loading}
              aria-label={t("Origin ekle")}
              aria-invalid={!!errors.input}
              aria-describedby={errors.input ? "origin-error" : undefined}
            />
            {errors.input && (
              <p id="origin-error" className="mt-1 text-xs text-rose-600" role="alert">
                {errors.input}
              </p>
            )}
          </div>
          <button
            className="btn-outline whitespace-nowrap"
            onClick={handleAdd}
            disabled={!canManage || loading || !input.trim()}
            aria-label={t("Origin ekle")}
          >
            {t("Ekle")}
          </button>
        </div>

        {/* Origins List */}
        {origins.length > 0 && (
          <div className="space-y-2">
            {origins.map((origin, index) => (
              <div
                key={`${origin}-${index}`}
                className="card-soft flex items-center justify-between px-4 py-3"
              >
                <span className="font-mono text-sm text-[var(--ink)]">{origin}</span>
                <button
                  className="btn-ghost text-sm"
                  onClick={() => handleRemove(origin)}
                  disabled={!canManage || loading}
                  aria-label={t("{origin} origin'ini kaldır", { origin })}
                >
                  {t("Kaldır")}
                </button>
              </div>
            ))}
          </div>
        )}

        {/* Save Button */}
        {origins.length > 0 && canManage && (
          <div className="flex items-center justify-between gap-4 pt-2">
            <button
              className="btn-primary"
              onClick={handleSave}
              disabled={loading || isSaving}
              aria-label={t("Değişiklikleri kaydet")}
            >
              {isSaving ? t("Kaydediliyor...") : t("Değişiklikleri kaydet")}
            </button>
            {showStandaloneLink && (
              <Link
                href={ROUTES.TENANT_SECURITY_ORIGINS(tenantId)}
                className="btn-ghost text-sm"
              >
                {t("Detaylı yönetim →")}
              </Link>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
