"use client";

import { useRouter } from "next/navigation";

/**
 * Okuma arızası: anlaşılır mesaj ve "yeniden dene". İç sistem ayrıntısı (bağlantı dizesi, yığın izi,
 * sunucu adı) hiçbir zaman gösterilmez (Spec 0002, R-9).
 */
export function ErrorState({ onRetry }: { onRetry?: () => void }) {
  const router = useRouter();

  return (
    <div role="alert" className="rounded border border-red-300 bg-red-50 p-6 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100">
      <p className="font-medium">Loglar şu anda okunamıyor.</p>
      <p className="mt-1 text-sm">Lütfen biraz sonra yeniden deneyin.</p>
      <button
        type="button"
        onClick={onRetry ?? (() => router.refresh())}
        className="mt-4 rounded bg-red-800 px-4 py-2 text-white hover:bg-red-900"
      >
        Yeniden dene
      </button>
    </div>
  );
}
