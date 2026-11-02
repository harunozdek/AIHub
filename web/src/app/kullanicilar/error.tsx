"use client";

import { useRouter } from "next/navigation";
import { startTransition } from "react";

/** Okuma arızası: ayrıntı gösterilmez, yalnızca yeniden deneme sunulur (Spec 0004, AC-11; 0002, R-9). */
export default function KullanicilarError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const router = useRouter();
  const retry = () => startTransition(() => { router.refresh(); reset(); });

  return (
    <>
      <h1 className="mb-4 text-xl font-semibold">Kullanıcılar</h1>
      <div role="alert" className="rounded border border-red-300 bg-red-50 p-6 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100">
        <p className="font-medium">Kullanıcılar şu anda okunamıyor.</p>
        <p className="mt-1 text-sm">Lütfen biraz sonra yeniden deneyin.</p>
        <button type="button" onClick={retry} className="mt-4 rounded bg-red-800 px-4 py-2 text-white hover:bg-red-900">
          Yeniden dene
        </button>
      </div>
    </>
  );
}
