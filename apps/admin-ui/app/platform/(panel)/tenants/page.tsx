"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { EmptyState } from "@/components/EmptyState";
import { Stepper } from "@/components/Stepper";
import { FormField } from "@/components/FormField";
import { Modal } from "@/components/Modal";
import { KebabMenu } from "@/components/KebabMenu";
import { adminRequest } from "@/lib/admin-api";
import { useAdminSettings } from "@/lib/admin-settings";
import { useI18n } from "@/lib/i18n";
import { canManageTenants } from "@/lib/roles";
import { formatDate } from "@/lib/utils";
import { slugify } from "@/lib/slugify";
import {
  validateTenantId,
  validateEmail,
  validatePassword,
  generatePassword,
} from "@/lib/validation";

type TenantSummary = {
  id: string;
  name: string;
  status: string;
  createdAt: string;
};

type ApiKeyCreatedResponse = {
  apiKeyId: string;
  apiKey: string;
  apiKeyPrefix: string;
  keyType?: string | null;
};

const DEMO_PRODUCTS = [
  {
    id: "sku-red-shoe",
    name: "Red Shoe",
    description: "Lightweight trainer with breathable mesh.",
    brand: "Acme",
    category: "Shoes",
    price: 89.99,
    in_stock: true,
  },
  {
    id: "sku-blue-shirt",
    name: "Blue Shirt",
    description: "Soft cotton shirt for everyday wear.",
    brand: "Acme",
    category: "Apparel",
    price: 29.5,
    in_stock: true,
  },
  {
    id: "sku-green-backpack",
    name: "Green Backpack",
    description: "Durable backpack with laptop compartment.",
    brand: "Northwind",
    category: "Accessories",
    price: 59.0,
    in_stock: false,
  },
];

type WizardStep = 1 | 2 | 3 | 4;

type FormData = {
  tenantName: string;
  tenantId: string;
  industry: string;
  ownerName: string;
  ownerEmail: string;
  ownerPassword: string;
  keyName: string;
  allowedOrigins: string;
  seedDemo: boolean;
};

type FormErrors = Partial<Record<keyof FormData, string>>;

export default function TenantsPage() {
  const { settings, isReady } = useAdminSettings();
  const { t } = useI18n();
  const [tenants, setTenants] = useState<TenantSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [currentStep, setCurrentStep] = useState<WizardStep>(1);
  const [showModal, setShowModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [formData, setFormData] = useState<FormData>({
    tenantName: "",
    tenantId: "",
    industry: "e-commerce",
    ownerName: "",
    ownerEmail: "",
    ownerPassword: "",
    keyName: "Search Public Key",
    allowedOrigins: "",
    seedDemo: true,
  });
  const [formErrors, setFormErrors] = useState<FormErrors>({});
  const [createdTenant, setCreatedTenant] = useState<{ id: string; name: string } | null>(null);
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [createdKeyPrefix, setCreatedKeyPrefix] = useState<string | null>(null);

  const canManage = canManageTenants(settings.role);
  const canLoad = Boolean(settings.authToken);

  useEffect(() => {
    if (!isReady || !canLoad) return;

    let isMounted = true;
    setLoading(true);
    setError(null);

    adminRequest<{ tenants: TenantSummary[] }>(settings, "/tenants", {
      skipTenantHeader: true,
    })
      .then((response) => {
        if (isMounted) {
          setTenants(response.tenants ?? []);
        }
      })
      .catch((err: Error) => {
        if (isMounted) {
          const errorMessage = err.message;
          if (errorMessage.includes("invalid_token") || errorMessage.includes("401")) {
            setError("Oturum süresi doldu. Lütfen tekrar giriş yapın.");
          } else {
            setError(errorMessage);
          }
        }
      })
      .finally(() => {
        if (isMounted) {
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [isReady, canLoad, settings.authToken]);

  // Auto-generate tenant ID from name
  useEffect(() => {
    if (formData.tenantName && currentStep === 1) {
      const slug = slugify(formData.tenantName);
      if (slug && slug !== formData.tenantId) {
        setFormData((prev) => ({ ...prev, tenantId: slug }));
      }
    }
  }, [formData.tenantName, currentStep]);

  const validateStep = (step: WizardStep): boolean => {
    const errors: FormErrors = {};

    if (step === 1) {
      if (!formData.tenantName.trim()) {
        errors.tenantName = "Tenant adı gereklidir.";
      }
      const tenantIdValidation = validateTenantId(formData.tenantId);
      if (!tenantIdValidation.valid) {
        errors.tenantId = tenantIdValidation.error;
      }
    }

    if (step === 2) {
      if (!formData.ownerName.trim()) {
        errors.ownerName = "Ad Soyad gereklidir.";
      }
      const emailValidation = validateEmail(formData.ownerEmail);
      if (!emailValidation.valid) {
        errors.ownerEmail = emailValidation.error;
      }
      const passwordValidation = validatePassword(formData.ownerPassword);
      if (!passwordValidation.valid) {
        errors.ownerPassword = passwordValidation.error;
      }
    }

    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleNext = () => {
    if (!validateStep(currentStep)) return;

    if (currentStep < 3) {
      setCurrentStep((prev) => (prev + 1) as WizardStep);
    } else {
      handleCreateTenant();
    }
  };

  const handleBack = () => {
    if (currentStep > 1) {
      setCurrentStep((prev) => (prev - 1) as WizardStep);
      setFormErrors({});
    }
  };

  const handleCreateTenant = async () => {
    if (!validateStep(3)) return;

    setError(null);
    setCreating(true);
    setCreatedTenant(null);
    setCreatedKey(null);
    setCreatedKeyPrefix(null);

    try {
      await adminRequest(
        settings,
        "/tenants",
        {
          method: "POST",
          body: JSON.stringify({
            tenantId: formData.tenantId.trim(),
            name: formData.tenantName.trim(),
          }),
          skipTenantHeader: true,
        },
        "application/json"
      );

      await adminRequest(
        settings,
        "/auth/users",
        {
          method: "POST",
          body: JSON.stringify({
            tenantId: formData.tenantId.trim(),
            email: formData.ownerEmail.trim(),
            name: formData.ownerName.trim(),
            password: formData.ownerPassword,
          }),
          skipTenantHeader: true,
        },
        "application/json"
      );

      const created = await adminRequest<ApiKeyCreatedResponse>(
        settings,
        "/api-keys",
        {
          method: "POST",
          tenantId: formData.tenantId.trim(),
          body: JSON.stringify({
            name: formData.keyName.trim() || "Search Public Key",
            type: "search_public",
            ...(formData.allowedOrigins.trim() && {
              allowedOrigins: formData.allowedOrigins
                .trim()
                .split(",")
                .map((o) => o.trim())
                .filter(Boolean),
            }),
          }),
        },
        "application/json"
      );

      setCreatedKey(created.apiKey);
      setCreatedKeyPrefix(created.apiKeyPrefix);
      setCreatedTenant({ id: formData.tenantId.trim(), name: formData.tenantName.trim() });

      if (formData.seedDemo) {
        await adminRequest<{ job_id?: string; jobId?: string }>(
          settings,
          "/products:bulk",
          {
            method: "POST",
            tenantId: formData.tenantId.trim(),
            body: JSON.stringify(DEMO_PRODUCTS),
          },
          "application/json"
        );
      }

      const refreshed = await adminRequest<{ tenants: TenantSummary[] }>(
        settings,
        "/tenants",
        { skipTenantHeader: true }
      );
      setTenants(refreshed.tenants ?? []);

      setCurrentStep(4);
    } catch (err) {
      const errorMessage = (err as Error).message;

      if (errorMessage.includes("tenant_exists") || errorMessage.includes("409")) {
        setFormErrors({ tenantId: "Bu Tenant ID zaten kullanılıyor. Lütfen farklı bir ID seçin." });
        setCurrentStep(1);
      } else if (errorMessage.includes("invalid_token") || errorMessage.includes("401")) {
        setError("Oturum süresi doldu. Lütfen tekrar giriş yapın.");
      } else if (errorMessage.includes("password_too_short")) {
        setFormErrors({ ownerPassword: "Şifre en az 8 karakter olmalıdır." });
        setCurrentStep(2);
      } else if (errorMessage.includes("invalid_payload") || errorMessage.includes("400")) {
        setError("Girilen bilgiler geçersiz. Lütfen tüm alanları kontrol edin.");
      } else if (errorMessage.includes("forbidden") || errorMessage.includes("403")) {
        setError("Bu işlem için yetkiniz bulunmamaktadır.");
      } else if (errorMessage.includes("tenant_not_found") || errorMessage.includes("404")) {
        setError("Tenant bulunamadı. Lütfen tekrar deneyin.");
      } else {
        const friendlyMessage = errorMessage
          .replace(/invalid_token/g, "Oturum hatası")
          .replace(/tenant_exists/g, "Tenant zaten mevcut")
          .replace(/invalid_payload/g, "Geçersiz veri")
          .replace(/forbidden/g, "Yetki hatası");
        setError(friendlyMessage || "Bir hata oluştu. Lütfen tekrar deneyin.");
      }
    } finally {
      setCreating(false);
    }
  };

  const handleReset = () => {
    setShowModal(false);
    setCurrentStep(1);
    setFormData({
      tenantName: "",
      tenantId: "",
      industry: "e-commerce",
      ownerName: "",
      ownerEmail: "",
      ownerPassword: "",
      keyName: "Search Public Key",
      allowedOrigins: "",
      seedDemo: true,
    });
    setFormErrors({});
    setError(null);
    setCreatedTenant(null);
    setCreatedKey(null);
    setCreatedKeyPrefix(null);
  };

  const handleCopyKey = async () => {
    if (!createdKey) return;
    try {
      await navigator.clipboard.writeText(createdKey);
    } catch {
      // Ignore
    }
  };

  const handleCopyTenantId = async (tenantId: string) => {
    try {
      await navigator.clipboard.writeText(tenantId);
    } catch {
      // Ignore
    }
  };

  const passwordStrength = formData.ownerPassword
    ? validatePassword(formData.ownerPassword).strength
    : undefined;

  const filteredTenants = tenants.filter(
    (tenant) =>
      tenant.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      tenant.id.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const getStatusLabel = (status: string) => {
    const statusKey = status?.toLowerCase() ?? "";
    return statusKey === "active"
      ? "Aktif"
      : statusKey === "disabled"
        ? "Devre dışı"
        : statusKey === "suspended"
          ? "Askıya alındı"
          : "Bilinmiyor";
  };

  const getStatusColor = (status: string) => {
    const statusKey = status?.toLowerCase() ?? "";
    return statusKey === "active"
      ? "bg-emerald-100 text-emerald-700"
      : statusKey === "disabled"
        ? "bg-gray-100 text-gray-700"
        : statusKey === "suspended"
          ? "bg-amber-100 text-amber-700"
          : "bg-gray-100 text-gray-700";
  };

  return (
    <div className="space-y-8">
      <PageHeader
        title="Tenantlar"
        subtitle="Kiracıları yönetin ve hesap hiyerarşisini planlayın."
        action={
          canManage && (
            <button
              type="button"
              className="btn-primary flex items-center gap-2"
              onClick={() => setShowModal(true)}
            >
              <svg
                className="h-4 w-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 4v16m8-8H4"
                />
              </svg>
              Yeni Tenant
            </button>
          )
        }
      />

      {!settings.authToken && (
        <EmptyState
          title="Admin API'yi bağlayın"
          description="Kiracıları listelemek veya oluşturmak için platform girişi yapın."
        />
      )}

      {settings.authToken && (
        <>
          {tenants.length === 0 && !loading ? (
            <div className="card p-12">
              <EmptyState
                title="Henüz tenant yok"
                description="İlk tenant'ınızı oluşturarak başlayın. Tenant, ilk yönetici kullanıcı ve search anahtarı tek akışta oluşturulur."
                action={
                  canManage && (
                    <button
                      type="button"
                      className="btn-primary"
                      onClick={() => setShowModal(true)}
                    >
                      İlk tenant'ı oluştur
                    </button>
                  )
                }
              />
            </div>
          ) : (
            <div className="card p-6">
              <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
                <div>
                  <h2 className="font-display text-2xl text-[var(--ink)]">Tenant listesi</h2>
                  <p className="mt-1 text-sm text-[var(--muted)]">
                    {tenants.length} {tenants.length === 1 ? "tenant" : "tenant"}
                  </p>
                </div>
                {tenants.length > 0 && (
                  <div className="flex items-center gap-3">
                    <input
                      type="search"
                      className="input w-64"
                      placeholder="Tenant ara..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                    />
                  </div>
                )}
              </div>

              {error && (
                <div className="mb-4 rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
                  {error}
                </div>
              )}

              {loading ? (
                <div className="py-12 text-center text-sm text-[var(--muted)]">
                  Yükleniyor...
                </div>
              ) : filteredTenants.length === 0 && searchQuery ? (
                <div className="py-12 text-center text-sm text-[var(--muted)]">
                  "{searchQuery}" için sonuç bulunamadı.
                </div>
              ) : (
                <div className="space-y-2">
                  {filteredTenants.map((tenant) => {
                    const statusLabel = getStatusLabel(tenant.status);
                    const statusColor = getStatusColor(tenant.status);
                    const isHighlighted = createdTenant?.id === tenant.id;

                    return (
                      <div
                        key={tenant.id}
                        className={`card-soft flex items-center justify-between px-4 py-4 transition-colors hover:bg-[var(--accent-hover)] ${
                          isHighlighted ? "ring-2 ring-emerald-500" : ""
                        }`}
                      >
                        <div className="flex-1">
                          <div className="flex items-center gap-3">
                            <p className="font-semibold text-[var(--ink)]">{tenant.name}</p>
                            <span className={`pill text-xs ${statusColor}`}>{statusLabel}</span>
                          </div>
                          <p className="mt-1 text-xs text-[var(--muted)]">
                            Oluşturma: {formatDate(tenant.createdAt)} · ID:{" "}
                            <code className="font-mono">{tenant.id}</code>
                          </p>
                        </div>
                        <KebabMenu
                          items={[
                            {
                              label: "Görüntüle",
                              onClick: () => {
                                // TODO: Navigate to tenant detail page
                                window.location.href = `/tenant/${tenant.id}`;
                              },
                            },
                            {
                              label: "Tenant ID'yi Kopyala",
                              onClick: () => handleCopyTenantId(tenant.id),
                            },
                          ]}
                        />
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </>
      )}

      {/* Create Tenant Modal */}
      <Modal
        isOpen={showModal}
        onClose={handleReset}
        title="Yeni Tenant Oluştur"
        size="lg"
      >
        <div className="space-y-6">
          {/* Stepper */}
          <Stepper
            steps={[
              { label: "Tenant Bilgileri", description: "Temel bilgiler" },
              { label: "İlk Admin", description: "Yönetici kullanıcı" },
              { label: "Entegrasyon", description: "Search anahtarı" },
            ]}
            currentStep={currentStep}
          />

          {/* Step Content */}
          {currentStep === 1 && (
            <div className="space-y-6">
              <div>
                <h3 className="font-display text-lg font-semibold text-[var(--ink)]">
                  Tenant Bilgileri
                </h3>
                <p className="mt-1 text-sm text-[var(--muted)]">
                  Yeni tenant için temel bilgileri girin.
                </p>
              </div>

              <FormField
                label="Tenant Adı"
                required
                error={formErrors.tenantName}
                helperText="Örn: Demo Mağaza, Acme Store"
              >
                <input
                  className="input mt-2"
                  placeholder="Demo Mağaza"
                  value={formData.tenantName}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, tenantName: e.target.value }))
                  }
                  disabled={creating}
                  autoFocus
                />
              </FormField>

              <FormField
                label="Tenant ID (slug)"
                required
                error={formErrors.tenantId}
                helperText="URL'lerde ve entegrasyonlarda kullanılır. Boşluk yok, küçük harf, tire."
              >
                <input
                  className="input mt-2 font-mono text-sm"
                  placeholder="demo-store"
                  value={formData.tenantId}
                  onChange={(e) => {
                    const value = e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, "");
                    setFormData((prev) => ({ ...prev, tenantId: value }));
                  }}
                  disabled={creating}
                  maxLength={32}
                />
              </FormField>

              <FormField
                label="Sektör"
                helperText="Opsiyonel: Tenant'ın faaliyet gösterdiği sektör"
              >
                <select
                  className="input mt-2"
                  value={formData.industry}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, industry: e.target.value }))
                  }
                  disabled={creating}
                >
                  <option value="e-commerce">E-ticaret</option>
                  <option value="retail">Perakende</option>
                  <option value="b2b">B2B</option>
                  <option value="other">Diğer</option>
                </select>
              </FormField>
            </div>
          )}

          {currentStep === 2 && (
            <div className="space-y-6">
              <div>
                <h3 className="font-display text-lg font-semibold text-[var(--ink)]">
                  İlk Admin (Owner) Kullanıcı
                </h3>
                <p className="mt-1 text-sm text-[var(--muted)]">
                  Bu kullanıcı Tenant paneline giriş yapacak ilk yöneticidir.
                </p>
              </div>

              <FormField label="Ad Soyad" required error={formErrors.ownerName}>
                <input
                  className="input mt-2"
                  placeholder="Ahmet Yılmaz"
                  value={formData.ownerName}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, ownerName: e.target.value }))
                  }
                  disabled={creating}
                  autoFocus
                />
              </FormField>

              <FormField
                label="E-posta"
                required
                error={formErrors.ownerEmail}
                helperText="Giriş için kullanılacak e-posta adresi"
              >
                <input
                  className="input mt-2"
                  type="email"
                  placeholder="owner@example.com"
                  value={formData.ownerEmail}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, ownerEmail: e.target.value }))
                  }
                  disabled={creating}
                />
              </FormField>

              <FormField
                label="Şifre"
                required
                error={formErrors.ownerPassword}
                helperText={
                  passwordStrength
                    ? passwordStrength === "strong"
                      ? "Şifre gücü: Güçlü"
                      : passwordStrength === "medium"
                        ? "Şifre gücü: Orta"
                        : "Şifre gücü: Zayıf - Daha güçlü bir şifre seçin"
                    : "En az 10 karakter"
                }
              >
                <div className="mt-2 space-y-2">
                  <div className="flex gap-2">
                    <input
                      className="input flex-1"
                      type="password"
                      placeholder="********"
                      value={formData.ownerPassword}
                      onChange={(e) =>
                        setFormData((prev) => ({ ...prev, ownerPassword: e.target.value }))
                      }
                      disabled={creating}
                    />
                    <button
                      type="button"
                      className="btn-outline whitespace-nowrap"
                      onClick={() => {
                        const newPassword = generatePassword();
                        setFormData((prev) => ({ ...prev, ownerPassword: newPassword }));
                      }}
                      disabled={creating}
                    >
                      Güçlü Şifre Oluştur
                    </button>
                  </div>
                  {formData.ownerPassword && passwordStrength && (
                    <div className="flex gap-1">
                      {[1, 2, 3].map((level) => {
                        const isActive =
                          (passwordStrength === "weak" && level === 1) ||
                          (passwordStrength === "medium" && level <= 2) ||
                          (passwordStrength === "strong" && level <= 3);
                        return (
                          <div
                            key={level}
                            className={`h-1 flex-1 rounded ${
                              isActive
                                ? passwordStrength === "strong"
                                  ? "bg-emerald-500"
                                  : passwordStrength === "medium"
                                    ? "bg-amber-500"
                                    : "bg-rose-500"
                                : "bg-[var(--border)]"
                            }`}
                          />
                        );
                      })}
                    </div>
                  )}
                </div>
              </FormField>
            </div>
          )}

          {currentStep === 3 && (
            <div className="space-y-6">
              <div>
                <h3 className="font-display text-lg font-semibold text-[var(--ink)]">
                  Entegrasyon / Search Anahtarı
                </h3>
                <p className="mt-1 text-sm text-[var(--muted)]">
                  Bu anahtar JS widget (client-side) için kullanılır. Sadece search scope ve origin
                  kısıtı önerilir.
                </p>
              </div>

              <FormField label="Search Key Adı" helperText="Varsayılan: Search Public Key">
                <input
                  className="input mt-2"
                  value={formData.keyName}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, keyName: e.target.value }))
                  }
                  disabled={creating}
                  autoFocus
                />
              </FormField>

              <FormField
                label="Allowed Origins"
                helperText="Opsiyonel: Virgülle ayrılmış origin listesi (örn: https://example.com, https://app.example.com)"
              >
                <input
                  className="input mt-2"
                  placeholder="https://example.com"
                  value={formData.allowedOrigins}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, allowedOrigins: e.target.value }))
                  }
                  disabled={creating}
                />
              </FormField>

              <label className="flex items-start gap-3 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 rounded border-[var(--border)]"
                  checked={formData.seedDemo}
                  onChange={(e) =>
                    setFormData((prev) => ({ ...prev, seedDemo: e.target.checked }))
                  }
                  disabled={creating}
                />
                <div>
                  <span className="font-medium text-[var(--ink)]">Demo ürün datası yükle</span>
                  <p className="mt-0.5 text-xs text-[var(--muted)]">
                    Tenant oluşturulduktan sonra örnek ürünler otomatik yüklenecek.
                  </p>
                </div>
              </label>
            </div>
          )}

          {currentStep === 4 && createdTenant && (
            <div className="space-y-6">
              <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-6">
                <div className="flex items-start gap-4">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-500 text-white">
                    <svg
                      className="h-6 w-6"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M5 13l4 4L19 7"
                      />
                    </svg>
                  </div>
                  <div className="flex-1">
                    <h3 className="font-display text-lg font-semibold text-emerald-900">
                      Tenant başarıyla oluşturuldu!
                    </h3>
                    <p className="mt-1 text-sm text-emerald-700">
                      Tenant, owner kullanıcı ve search anahtarı hazır.
                    </p>
                  </div>
                </div>

                <div className="mt-6 space-y-4 rounded-xl bg-white/60 p-4">
                  <div>
                    <p className="text-xs font-medium text-emerald-800">Tenant</p>
                    <p className="mt-1 font-semibold text-emerald-900">{createdTenant.name}</p>
                    <p className="mt-0.5 font-mono text-xs text-emerald-700">
                      {createdTenant.id}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs font-medium text-emerald-800">Owner Kullanıcı</p>
                    <p className="mt-1 font-semibold text-emerald-900">{formData.ownerEmail}</p>
                    <p className="mt-0.5 text-xs text-emerald-700">Owner kullanıcı oluşturuldu</p>
                  </div>
                  {createdKey && (
                    <div>
                      <p className="text-xs font-medium text-emerald-800">Search Public Key</p>
                      <div className="mt-2 flex items-center gap-2">
                        <code className="flex-1 rounded-lg bg-emerald-100 px-3 py-2 font-mono text-xs text-emerald-900">
                          {createdKey}
                        </code>
                        <button
                          type="button"
                          className="btn-outline whitespace-nowrap"
                          onClick={handleCopyKey}
                        >
                          Kopyala
                        </button>
                      </div>
                      <p className="mt-1 text-xs text-emerald-700">
                        Bu anahtar sadece şimdi gösterilir. Lütfen güvenli bir yere kaydedin.
                      </p>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* Navigation */}
          {currentStep < 4 && (
            <div className="flex items-center justify-between border-t border-[var(--border)] pt-6">
              <button
                type="button"
                className="btn-outline"
                onClick={currentStep === 1 ? handleReset : handleBack}
                disabled={creating}
              >
                {currentStep === 1 ? "İptal" : "Geri"}
              </button>
              <button
                type="button"
                className="btn-primary"
                onClick={handleNext}
                disabled={creating}
              >
                {creating
                  ? "Oluşturuluyor..."
                  : currentStep === 3
                    ? "Tenant Oluştur"
                    : "İleri"}
              </button>
            </div>
          )}

          {currentStep === 4 && (
            <div className="flex items-center justify-end border-t border-[var(--border)] pt-6">
              <button type="button" className="btn-primary" onClick={handleReset}>
                Kapat
              </button>
            </div>
          )}

          {error && (
            <div className="rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
              {error}
            </div>
          )}
        </div>
      </Modal>
    </div>
  );
}
