"use client";

import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

export function StatusBadge({ status }: { status?: string | null }) {
  const { t } = useI18n();
  const normalized = (status || "unknown").toLowerCase();
  const color =
    normalized === "completed"
      ? "bg-emerald-100 text-emerald-700"
      : normalized === "failed"
        ? "bg-rose-100 text-rose-700"
        : normalized === "processing" || normalized === "running"
          ? "bg-amber-100 text-amber-700"
          : normalized === "queued"
            ? "bg-slate-100 text-slate-600"
            : "bg-slate-100 text-slate-600";

  const labelMap: Record<string, string> = {
    completed: t("Tamamlandı"),
    failed: t("Hata"),
    processing: t("İşleniyor"),
    running: t("Çalışıyor"),
    queued: t("Kuyrukta"),
    unknown: t("Bilinmiyor"),
  };

  const label = labelMap[normalized] ?? status ?? t("Bilinmiyor");

  return (
    <span
      className={cn(
        "rounded-full px-3 py-1 text-xs font-medium uppercase tracking-[0.12em]",
        color
      )}
    >
      {label}
    </span>
  );
}
