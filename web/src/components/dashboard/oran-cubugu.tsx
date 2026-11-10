/**
 * Dashboard renkleri ve oran çubukları (Spec 0007, CB-3, CB-5). Renk tek başına bilgi taşımaz: sayı yanında
 * metin olarak durur, dereceler açıklamada adıyla yazılır. Her renk açık ve koyu zeminde en az 3:1 kontrastlıdır
 * (WCAG 1.4.11; plan 0007, risk 4).
 */
export const DERECE_RENGI: Record<string, string> = {
  Critical: "bg-red-900 dark:bg-red-300",
  Error: "bg-red-600 dark:bg-red-500",
  Warning: "bg-amber-700 dark:bg-amber-400",
  Info: "bg-sky-700 dark:bg-sky-400",
};

export const DURUM_RENGI = {
  Up: "bg-green-700 dark:bg-green-500",
  Down: "bg-red-600 dark:bg-red-500",
  Unknown: "bg-zinc-500 dark:bg-zinc-400",
} as const;

/** 0–1 arası oranın tam sayı yüzdesi. */
export function yuzde(pay: number, payda: number): number {
  return payda > 0 ? Math.round((Math.max(0, pay) / payda) * 100) : 0;
}

/**
 * Sayıyla orantılı yatay çubuk. Yanındaki sayı zaten metin olduğundan ekran okuyucudan gizlenir; yüzde
 * `data-oran` olarak da yazılır.
 */
export function OranCubugu({ oran, renk }: { oran: number; renk: string }) {
  return (
    <span aria-hidden="true" data-oran={oran} className="mt-1 block h-2 w-full rounded-sm bg-zinc-100 dark:bg-zinc-800">
      <span className={`block h-2 rounded-sm ${renk}`} style={{ width: `${oran}%` }} />
    </span>
  );
}

/** Birden çok parçalı yatay çubuk; parçalar arasında zemin rengi bir boşluk bırakır. */
export function YiginCubuk({ parcalar }: { parcalar: { ad: string; oran: number; renk: string }[] }) {
  return (
    <span aria-hidden="true" className="mt-3 flex h-3 w-full gap-px overflow-hidden rounded-sm bg-zinc-100 dark:bg-zinc-800">
      {parcalar.filter((p) => p.oran > 0).map((p) => (
        <span key={p.ad} data-durum={p.ad} data-oran={p.oran} className={`block h-3 ${p.renk}`} style={{ width: `${p.oran}%` }} />
      ))}
    </span>
  );
}
