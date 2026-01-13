"use client";

import { Suspense, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";

function SearchPreviewRedirect() {
  const params = useParams();
  const router = useRouter();
  const tenantId = params.tenantId as string;

  useEffect(() => {
    router.replace(`/tenant/${tenantId}/getting-started?tab=test`);
  }, [router, tenantId]);

  return null;
}

export default function SearchPreviewPage() {
  return (
    <Suspense fallback={null}>
      <SearchPreviewRedirect />
    </Suspense>
  );
}
