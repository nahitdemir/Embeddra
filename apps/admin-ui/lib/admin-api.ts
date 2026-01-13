import { AdminSettings } from "./admin-settings";
import { normalizeBaseUrl } from "./utils";

export type AdminRequestOptions = RequestInit & {
  tenantId?: string | null;
  skipTenantHeader?: boolean;
  authStrategy?: "bearer";
};

export async function adminRequest<T>(
  settings: AdminSettings,
  path: string,
  options: AdminRequestOptions = {},
  contentType?: string
) {
  // Get API base URL from settings or fallback to env variable
  const apiBaseUrl = settings.apiBaseUrl || process.env.NEXT_PUBLIC_ADMIN_API_BASE_URL || "http://localhost:5114";
  const baseUrl = normalizeBaseUrl(apiBaseUrl);
  const url = `${baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  const headers = new Headers(options.headers);

  const strategy = options.authStrategy ?? "bearer";
  if (strategy === "bearer" && !headers.has("Authorization")) {
    if (!settings.authToken) {
      throw new Error("Oturum belirteci eksik.");
    }
    
    // Check token expiry
    if (settings.authExpiresAt) {
      const expiresAt = new Date(settings.authExpiresAt);
      const now = new Date();
      if (now >= expiresAt) {
        throw new Error("Oturum süresi doldu. Lütfen tekrar giriş yapın.");
      }
    }
    
    headers.set("Authorization", `Bearer ${settings.authToken}`);
  }

  // X-Actor header removed - not needed for production
  // if (settings.actor) {
  //   headers.set("X-Actor", settings.actor);
  // }

  if (!options.skipTenantHeader) {
    const resolvedTenant = options.tenantId ?? settings.tenantId;
    if (resolvedTenant) {
      headers.set("X-Tenant-Id", resolvedTenant);
    }
  }

  if (contentType && !headers.has("Content-Type")) {
    headers.set("Content-Type", contentType);
  }

  let response: Response;
  try {
    response = await fetch(url, {
      ...options,
      headers,
    });
  } catch (fetchError) {
    // Network error (connection refused, timeout, etc.)
    const errorMessage = fetchError instanceof Error 
      ? fetchError.message 
      : "Bağlantı hatası";
    
    // Provide helpful error message
    if (errorMessage.includes("Failed to fetch") || errorMessage.includes("ERR_CONNECTION_REFUSED")) {
      throw new Error(
        `Backend API'ye bağlanılamıyor. Lütfen API'nin çalıştığından emin olun. (${baseUrl})`
      );
    }
    throw new Error(`Ağ hatası: ${errorMessage}`);
  }

  const responseType = response.headers.get("content-type") ?? "";
  let payload: any;
  try {
    payload = responseType.includes("application/json")
      ? await response.json()
      : await response.text();
  } catch (parseError) {
    // If response parsing fails, still check status
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    throw new Error("Yanıt parse edilemedi.");
  }

  if (!response.ok) {
    // Handle invalid_token error - token expired or invalid
    if (response.status === 401 && payload && typeof payload === "object" && payload.error === "invalid_token") {
      // Clear token and redirect to login
      if (typeof window !== "undefined") {
        // Clear localStorage
        const storageKey = "embeddra_admin_settings";
        const storageKeyPlatform = "embeddra_admin_settings_platform";
        const storageKeyTenant = "embeddra_admin_settings_tenant";
        
        [storageKey, storageKeyPlatform, storageKeyTenant].forEach((key) => {
          const raw = localStorage.getItem(key);
          if (raw) {
            try {
              const parsed = JSON.parse(raw);
              parsed.authToken = "";
              parsed.authExpiresAt = "";
              localStorage.setItem(key, JSON.stringify(parsed));
            } catch {
              localStorage.removeItem(key);
            }
          }
        });
        
        // Clear cookie via logout endpoint
        fetch("/api/auth/logout", { method: "POST" }).catch(() => {
          // Ignore errors
        });
        
        // Redirect to login (use replace to avoid adding to history)
        // Use setTimeout to ensure localStorage is cleared before redirect
        setTimeout(() => {
          window.location.replace("/login");
        }, 100);
      }
      throw new Error("Oturum süresi doldu veya geçersiz. Lütfen tekrar giriş yapın.");
    }
    
    const message =
      (payload && (payload.error || payload.message || payload.code)) ||
      response.statusText ||
      `HTTP ${response.status}: İstek başarısız.`;
    throw new Error(message);
  }

  return payload as T;
}
