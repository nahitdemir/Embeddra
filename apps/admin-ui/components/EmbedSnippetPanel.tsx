"use client";

import { useState } from "react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

type EmbedSnippetPanelProps = {
  snippet: string;
  tenantId: string;
  searchApiBaseUrl: string;
  selectedApiKeyPrefix?: string;
};

type AdvancedOptions = {
  containerId: string;
  maxResults: number;
  placeholder: string;
  locale: string;
  theme: "light" | "dark" | "auto";
};

export function EmbedSnippetPanel({
  snippet,
  tenantId,
  searchApiBaseUrl,
  selectedApiKeyPrefix,
}: EmbedSnippetPanelProps) {
  const { t } = useI18n();
  const [copied, setCopied] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [advancedOptions, setAdvancedOptions] = useState<AdvancedOptions>({
    containerId: "#embeddra-search",
    maxResults: 8,
    placeholder: "Ürün ara...",
    locale: "tr",
    theme: "auto",
  });

  const generateSnippet = (): string => {
    const options = showAdvanced ? advancedOptions : {
      containerId: "#embeddra-search",
      maxResults: 8,
      placeholder: "Ürün ara...",
      locale: "tr",
      theme: "auto",
    };

    return `<!-- Embeddra Search Widget -->
<script>
  window.EmbeddraSearchUrl = '${searchApiBaseUrl}';
</script>
<script src="${searchApiBaseUrl.replace(/\/$/, "")}/widget/embed.js"></script>
<script>
  window.addEventListener('DOMContentLoaded', function () {
    window.EmbeddraWidget.init({
      container: '${options.containerId}',
      apiBaseUrl: '${searchApiBaseUrl}',
      apiKey: 'YOUR_API_KEY', // Replace with full API key from Security > API Keys
      tenantId: '${tenantId}',
      placeholder: '${options.placeholder}',
      maxResults: ${options.maxResults}${options.locale !== "tr" ? `,\n      locale: '${options.locale}'` : ""}${options.theme !== "auto" ? `,\n      theme: '${options.theme}'` : ""}
    });
  });
</script>
<div id="${options.containerId.replace("#", "")}"></div>`;
  };

  const currentSnippet = generateSnippet();

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(currentSnippet);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      // Error handled by toast in parent
    }
  };

  const handleDownload = () => {
    const blob = new Blob([currentSnippet], { type: "text/html" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "embeddra-widget.html";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-4">
      <div>
        <h3 className="font-display text-lg font-semibold text-[var(--ink)] mb-1">
          {t("Entegrasyon Kodu")}
        </h3>
        <p className="text-sm text-[var(--muted)]">
          {t("Bu kodu sitenizin HTML'ine ekleyerek arama widget'ını entegre edin.")}
        </p>
      </div>

      {selectedApiKeyPrefix && (
        <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-700">
          <p className="font-medium mb-1">{t("Önemli:")}</p>
          <p>
            {t("Snippet'teki 'YOUR_API_KEY' yerine tam API anahtarınızı yazın. Tam anahtarı Güvenlik > API Anahtarları sayfasından alabilirsiniz. (Önek: {prefix})", {
              prefix: selectedApiKeyPrefix,
            })}
          </p>
        </div>
      )}

      {/* Code Block */}
      <div className="card-soft p-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <span className="text-xs font-medium text-[var(--muted)] uppercase tracking-wider">
              {t("HTML Snippet")}
            </span>
          </div>
          <div className="flex items-center gap-2">
            <button
              className="btn-ghost text-xs"
              onClick={handleDownload}
              aria-label={t("Snippet'i indir")}
            >
              {t("İndir")}
            </button>
            <button
              className={cn("btn-primary text-xs", copied && "bg-emerald-600")}
              onClick={handleCopy}
              aria-label={t("Snippet'i kopyala")}
            >
              {copied ? t("Kopyalandı!") : t("Kopyala")}
            </button>
          </div>
        </div>
        <pre className="overflow-x-auto rounded-lg border border-[var(--border)] bg-[var(--background)] p-4 text-xs font-mono text-[var(--ink)] whitespace-pre-wrap break-words">
          <code>{currentSnippet}</code>
        </pre>
      </div>

      {/* Advanced Options */}
      <div className="card-soft p-4">
        <button
          className="flex w-full items-center justify-between text-sm font-medium text-[var(--ink)]"
          onClick={() => setShowAdvanced(!showAdvanced)}
          aria-expanded={showAdvanced}
          aria-controls="advanced-options"
        >
          <span>{t("Gelişmiş Seçenekler")}</span>
          <svg
            className={cn(
              "h-4 w-4 transition-transform",
              showAdvanced && "rotate-180"
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

        {showAdvanced && (
          <div id="advanced-options" className="mt-4 space-y-4">
            <div>
              <label className="label text-xs mb-1">{t("Container ID")}</label>
              <input
                className="input w-full text-sm"
                value={advancedOptions.containerId}
                onChange={(e) =>
                  setAdvancedOptions((prev) => ({
                    ...prev,
                    containerId: e.target.value,
                  }))
                }
                placeholder="#embeddra-search"
              />
              <p className="mt-1 text-xs text-[var(--muted)]">
                {t("Widget'ın render edileceği DOM element ID'si")}
              </p>
            </div>

            <div>
              <label className="label text-xs mb-1">{t("Maksimum Sonuç")}</label>
              <input
                type="number"
                className="input w-full text-sm"
                value={advancedOptions.maxResults}
                onChange={(e) =>
                  setAdvancedOptions((prev) => ({
                    ...prev,
                    maxResults: parseInt(e.target.value) || 8,
                  }))
                }
                min={1}
                max={50}
              />
            </div>

            <div>
              <label className="label text-xs mb-1">{t("Placeholder Metni")}</label>
              <input
                className="input w-full text-sm"
                value={advancedOptions.placeholder}
                onChange={(e) =>
                  setAdvancedOptions((prev) => ({
                    ...prev,
                    placeholder: e.target.value,
                  }))
                }
                placeholder="Ürün ara..."
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="label text-xs mb-1">{t("Dil")}</label>
                <select
                  className="input w-full text-sm"
                  value={advancedOptions.locale}
                  onChange={(e) =>
                    setAdvancedOptions((prev) => ({
                      ...prev,
                      locale: e.target.value,
                    }))
                  }
                >
                  <option value="tr">Türkçe</option>
                  <option value="en">English</option>
                </select>
              </div>

              <div>
                <label className="label text-xs mb-1">{t("Tema")}</label>
                <select
                  className="input w-full text-sm"
                  value={advancedOptions.theme}
                  onChange={(e) =>
                    setAdvancedOptions((prev) => ({
                      ...prev,
                      theme: e.target.value as "light" | "dark" | "auto",
                    }))
                  }
                >
                  <option value="auto">{t("Otomatik")}</option>
                  <option value="light">{t("Açık")}</option>
                  <option value="dark">{t("Koyu")}</option>
                </select>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
