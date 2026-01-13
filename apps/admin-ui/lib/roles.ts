export type AdminRole = "owner" | "platform_owner";

export const roleLabels: Record<AdminRole, string> = {
  owner: "Kiracı Sahibi",
  platform_owner: "Platform Sahibi",
};

export function isPlatformOwner(role: AdminRole) {
  return role === "platform_owner";
}

export function canManageTenants(role: AdminRole) {
  return isPlatformOwner(role);
}

export function canManageImports(role: AdminRole) {
  return role === "owner";
}

export function canManageSecurity(role: AdminRole) {
  return role === "owner";
}

export function canManageSearchTuning(role: AdminRole) {
  return role === "owner";
}

export function isReadOnly() {
  return false;
}
