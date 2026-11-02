"use client";

import { useRouter } from "next/navigation";
import { startTransition } from "react";
import { ErrorState } from "@/components/error-state";

/** Beklenmeyen hata: ayrıntı gösterilmez, yalnızca yeniden deneme sunulur (Spec 0002, R-9). */
export default function LogDetailError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const router = useRouter();
  const retry = () => startTransition(() => { router.refresh(); reset(); });

  return (
    <>
      <h1 className="mb-4 text-xl font-semibold">Log ayrıntısı</h1>
      <ErrorState onRetry={retry} />
    </>
  );
}
