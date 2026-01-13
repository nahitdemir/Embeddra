"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";

export default function EmbedPage() {
  const params = useParams();
  const router = useRouter();
  const tenantId = params.tenantId as string;

  useEffect(() => {
    router.replace(`/tenant/${tenantId}/getting-started`);
  }, [router, tenantId]);

  return null;
}
