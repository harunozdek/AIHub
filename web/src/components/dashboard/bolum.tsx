"use client";

import { useRouter } from "next/navigation";
import type { ReactNode } from "react";

/** Dashboard bölümü: başlığıyla adlandırılır; ekran okuyucu bölümleri başlıklardan tanır (Spec 0006, Erişilebilirlik). */
export function Bolum({ id, baslik, children }: { id: string; baslik: string; children: ReactNode }) {
  return (
    <section aria-labelledby={`${id}-baslik`} className="rounded border border-zinc-200 p-4 dark:border-zinc-800">
      <h2 id={`${id}-baslik`} className="mb-3 text-base font-semibold">{baslik}</h2>
      {children}
    </section>
  );
}

/** Bölümün boş durumu (Spec 0006, States). */
export function BosBolum({ children }: { children: ReactNode }) {
  return <p role="status" className="text-sm text-zinc-600 dark:text-zinc-400">{children}</p>;
}

/**
 * Bölüm arızası: mesaj ve "yeniden dene". Yalnızca bu bölüm hata gösterir; diğer bölümler kendi verisiyle kalır
 * (Spec 0006, R-9). İç ayrıntı gösterilmez.
 */
export function BolumHatasi({ mesaj }: { mesaj: string }) {
  const router = useRouter();
  return (
    <div role="alert" className="rounded border border-red-300 bg-red-50 p-4 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100">
      <p className="font-medium">{mesaj}</p>
      <p className="mt-1 text-sm">Lütfen biraz sonra yeniden deneyin.</p>
      <button type="button" onClick={() => router.refresh()} className="mt-3 rounded bg-red-800 px-3 py-1.5 text-sm text-white hover:bg-red-900">
        Yeniden dene
      </button>
    </div>
  );
}

/** Bölüm yükleniyor durumu. */
export function BolumYukleniyor({ metin }: { metin: string }) {
  return <p role="status" aria-live="polite" className="text-sm text-zinc-600 dark:text-zinc-400">{metin}</p>;
}

/** Veriyi ve pencereyi yeniden yükler (Spec 0006, R-8). */
export function YenileDugmesi() {
  const router = useRouter();
  return (
    <button type="button" onClick={() => router.refresh()} className="rounded border border-zinc-300 px-3 py-1.5 text-sm hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800">
      Yenile
    </button>
  );
}
