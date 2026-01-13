"use client";

import { AdminSettings } from "./admin-settings";
import { normalizeBaseUrl } from "./utils";

export type SearchRequestOptions = RequestInit & {
  tenantId?: string | null;
};

export type SearchRequestOptions = RequestInit & {
  tenantId?: string | null;
  apiKey?: string | null; // Optional API key override
};

export async function searchRequest<T>(
  settings: AdminSettings,
  path: string,
  options: SearchRequestOptions = {},
  contentType?: string
) {
  const baseUrl = normalizeBaseUrl(settings.searchApiBaseUrl || "http://localhost:5222");
  const url = `${baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  const headers = new Headers(options.headers);

  // API key priority: options.apiKey > settings.searchApiKey
  // This allows Search Preview to use tenant's API key from backend
  const resolvedKey = options.apiKey ?? settings.searchApiKey ?? "";
  if (resolvedKey && !headers.has("X-Api-Key")) {
    headers.set("X-Api-Key", resolvedKey);
  }

  const resolvedTenant = options.tenantId ?? settings.tenantId;
  if (resolvedTenant && !headers.has("X-Tenant-Id")) {
    headers.set("X-Tenant-Id", resolvedTenant);
  }

  if (contentType && !headers.has("Content-Type")) {
    headers.set("Content-Type", contentType);
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  const responseType = response.headers.get("content-type") ?? "";
  const payload = responseType.includes("application/json")
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    const message =
      (payload && (payload.error || payload.message || payload.code)) ||
      response.statusText ||
      "İstek başarısız.";
    throw new Error(message);
  }

  return payload as T;
}
