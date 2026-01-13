/**
 * Validation helpers for tenant onboarding
 */

export function validateTenantId(tenantId: string): { valid: boolean; error?: string } {
  if (!tenantId.trim()) {
    return { valid: false, error: "Tenant ID gereklidir." };
  }

  if (tenantId.length > 32) {
    return { valid: false, error: "Tenant ID en fazla 32 karakter olabilir." };
  }

  if (!/^[a-z0-9-]+$/.test(tenantId)) {
    return {
      valid: false,
      error: "Tenant ID yalnızca küçük harf, rakam ve tire içerebilir.",
    };
  }

  if (tenantId.startsWith("-") || tenantId.endsWith("-")) {
    return { valid: false, error: "Tenant ID tire ile başlayıp bitemez." };
  }

  return { valid: true };
}

export function validateEmail(email: string): { valid: boolean; error?: string } {
  if (!email.trim()) {
    return { valid: false, error: "E-posta gereklidir." };
  }

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(email)) {
    return { valid: false, error: "Geçerli bir e-posta adresi giriniz." };
  }

  return { valid: true };
}

export function validatePassword(password: string): { valid: boolean; error?: string; strength?: "weak" | "medium" | "strong" } {
  if (!password) {
    return { valid: false, error: "Şifre gereklidir." };
  }

  if (password.length < 10) {
    return { valid: false, error: "Şifre en az 10 karakter olmalıdır." };
  }

  // Simple strength check
  let strength: "weak" | "medium" | "strong" = "weak";
  if (password.length >= 12 && /[a-z]/.test(password) && /[A-Z]/.test(password) && /[0-9]/.test(password)) {
    strength = /[^a-zA-Z0-9]/.test(password) ? "strong" : "medium";
  }

  return { valid: true, strength };
}

export function generatePassword(length: number = 16): string {
  const charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
  let password = "";
  for (let i = 0; i < length; i++) {
    password += charset.charAt(Math.floor(Math.random() * charset.length));
  }
  return password;
}
