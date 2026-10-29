"use client";

import { useRouter } from "next/navigation";
import { startTransition } from "react";
import { ErrorState } from "@/components/error-state";

/** Beklenmeyen hata: ayrıntı gösterilmez, yalnızca yeniden deneme sunulur (Spec 0002, R-9). */
export default function LogsError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const router = useRouter();
  // Sunucu bileşeni yeniden çalışsın diye önce veri yenilenir, sonra hata sınırı sıfırlanır.
  const retry = () => startTransition(() => { router.refresh(); reset(); });

  return (
    <>
      <h1 className="mb-4 text-xl font-semibold">Loglar</h1>
      <ErrorState onRetry={retry} />
    </>
  );
}
