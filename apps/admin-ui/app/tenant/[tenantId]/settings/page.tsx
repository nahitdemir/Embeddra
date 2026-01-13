"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useParams } from "next/navigation";

/**
 * Settings page removed in MVP.
 * Redirects to tenant dashboard.
 */
export default function TenantSettingsRedirect() {
  const router = useRouter();
  const params = useParams();
  const tenantId = params.tenantId as string;
  
  useEffect(() => {
    if (tenantId) {
      router.replace(`/tenant/${tenantId}`);
    } else {
      router.replace("/login");
    }
  }, [router, tenantId]);

  return null;
}
