/**
 * Centralized constants for routes, asset paths, and configuration
 */

export const ROUTES = {
  // Tenant routes
  TENANT_DASHBOARD: (tenantId: string) => `/tenant/${tenantId}`,
  TENANT_INTEGRATION: (tenantId: string) => `/tenant/${tenantId}/integration`,
  TENANT_GETTING_STARTED: (tenantId: string) => `/tenant/${tenantId}/getting-started`,
  TENANT_SECURITY_API_KEYS: (tenantId: string) => `/tenant/${tenantId}/security/api-keys`,
  TENANT_SECURITY_ORIGINS: (tenantId: string) => `/tenant/${tenantId}/security/allowed-origins`,
  TENANT_ANALYTICS: (tenantId: string) => `/tenant/${tenantId}/analytics`,
  TENANT_CATALOG_IMPORTS: (tenantId: string) => `/tenant/${tenantId}/catalog/imports`,

  // Platform routes
  PLATFORM_DASHBOARD: "/platform",
  PLATFORM_TENANTS: "/platform/tenants",
  PLATFORM_AUDIT: "/platform/audit",

  // Auth routes
  LOGIN: "/login",
  TENANT_SELECT: "/tenant/select",
} as const;

export const API_ENDPOINTS = {
  API_KEYS: "/api-keys",
  ALLOWED_ORIGINS: "/allowed-origins",
  SEARCH_PREVIEW: "/search/preview",
  SEARCH_PREVIEW_CLICK: "/search/preview:click",
} as const;

export const INTEGRATION_STEPS = {
  SETUP: 1,
  ORIGINS: 2,
  EMBED: 3,
  TEST: 4,
} as const;
