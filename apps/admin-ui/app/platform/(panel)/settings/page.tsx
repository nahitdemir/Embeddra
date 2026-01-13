"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

/**
 * Settings page removed in MVP.
 * Redirects to platform dashboard.
 */
export default function PlatformSettingsRedirect() {
  const router = useRouter();
  
  useEffect(() => {
    router.replace("/platform");
  }, [router]);

  return null;
}
