import type { HealthStatus } from "@/lib/api-client";

/**
 * Uygulama durumunun simgesi (Spec 0012, CB-1). Renk tek ayırt edici değildir; biçimler de farklıdır: Ayakta daire
 * içinde onay, Down daire içinde çarpı, Bilinmiyor daire içinde soru işareti. Simge yalnızca görseldir
 * (`aria-hidden`): durum metni her zaman yanında yazılıdır ve ekran okuyucu metni bir kez okur.
 *
 * Renkler metin dışı içerik için 3:1'i açık ve koyu temada geçer (CB-7). Tailwind 4.3 oklch değerlerinden hesaplanan
 * oranlar: açık zeminde green-700 4,9:1, red-700 6,4:1, zinc-500 4,8:1; Down satırının red-50 zemininde red-700 5,9:1.
 * Koyu zeminde (zinc-950) green-400 11,2:1, red-400 6,9:1, zinc-400 7,6:1; red-950 zemininde red-400 5,6:1.
 */
export function DurumSimgesi({ status, className = "" }: { status: HealthStatus; className?: string }) {
  const renk =
    status === "Up" ? "text-green-700 dark:text-green-400" : status === "Down" ? "text-red-700 dark:text-red-400" : "text-zinc-500 dark:text-zinc-400";
  return (
    <svg
      aria-hidden="true"
      data-durum={status}
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={`inline-block size-5 shrink-0 ${renk} ${className}`}
    >
      <circle cx="10" cy="10" r="8.5" />
      {status === "Up" && <path d="M6 10.5l2.75 2.75L14 7.5" />}
      {status === "Down" && <path d="M7 7l6 6M13 7l-6 6" />}
      {status === "Unknown" && (
        <>
          <path d="M7.75 7.75a2.25 2.25 0 1 1 3.25 2c-.6.3-1 .8-1 1.5v.5" />
          <circle cx="10" cy="14.5" r="0.6" fill="currentColor" />
        </>
      )}
    </svg>
  );
}
