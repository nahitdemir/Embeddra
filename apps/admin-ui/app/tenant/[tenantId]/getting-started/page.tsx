"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { ROUTES } from "@/lib/constants";

export default function GettingStartedPage() {
  const params = useParams();
  const router = useRouter();
  const tenantId = params.tenantId as string;

  useEffect(() => {
    router.replace(ROUTES.TENANT_INTEGRATION(tenantId));
  }, [router, tenantId]);

  return null;
}
