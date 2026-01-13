export function cn(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}

export function normalizeBaseUrl(url: string) {
  if (!url) {
    return "http://localhost:5114";
  }

  const trimmed = url.trim();
  if (!trimmed) {
    return "http://localhost:5114";
  }

  return trimmed.endsWith("/") ? trimmed.slice(0, -1) : trimmed;
}

export function normalizeOptionalBaseUrl(url?: string | null) {
  const trimmed = (url ?? "").trim();
  if (!trimmed) {
    return "";
  }

  return trimmed.endsWith("/") ? trimmed.slice(0, -1) : trimmed;
}

export function formatDate(value?: string | Date | null) {
  if (!value) {
    return "--";
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "--";
  }

  return new Intl.DateTimeFormat(resolveLocale(), {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

export function formatNumber(value?: number | null) {
  if (value === null || value === undefined) {
    return "--";
  }

  return new Intl.NumberFormat(resolveLocale()).format(value);
}

export function formatPercent(value?: number | null) {
  if (value === null || value === undefined) {
    return "--";
  }

  if (Number.isNaN(value)) {
    return "--";
  }

  return `${(value * 100).toFixed(1)}%`;
}

export function formatDuration(start?: string | Date | null, end?: string | Date | null) {
  if (!start || !end) {
    return "--";
  }

  const startDate = start instanceof Date ? start : new Date(start);
  const endDate = end instanceof Date ? end : new Date(end);
  if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) {
    return "--";
  }

  const ms = endDate.getTime() - startDate.getTime();
  if (ms <= 0) {
    return "--";
  }

  const locale = resolveLocale();
  const isTurkish = locale.startsWith("tr");

  if (ms < 1000) {
    return `${ms}ms`;
  }

  const seconds = Math.round(ms / 100) / 10;
  if (seconds < 60) {
    return `${seconds}${isTurkish ? " sn" : "s"}`;
  }

  const minutes = Math.floor(seconds / 60);
  const remaining = Math.round((seconds % 60) * 10) / 10;
  if (isTurkish) {
    return `${minutes} dk ${remaining} sn`;
  }
  return `${minutes}m ${remaining}s`;
}

function resolveLocale() {
  if (typeof document === "undefined") {
    return "tr-TR";
  }

  const raw = document.documentElement.lang || "";
  if (!raw) {
    return "tr-TR";
  }

  if (raw === "tr") {
    return "tr-TR";
  }

  if (raw === "en") {
    return "en-US";
  }

  return raw;
}
