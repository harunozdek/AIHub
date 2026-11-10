/**
 * Dashboard renkleri ve oran çubukları (Spec 0007, CB-3, CB-5). Renk tek başına bilgi taşımaz: sayı yanında
 * metin olarak durur, dereceler açıklamada adıyla yazılır.
 *
 * Derece renkleri dört serilik bir küme olarak doğrulandı (dataviz validate_palette, bitişik çiftler):
 * - Açık (#ffffff zemin): #9f1d1d, #e0473a, #f2a01a, #3a7bd5 — tüm denetimler geçer; Warning 2,1:1 kontrastla
 *   "relief" ister: açıklama, sayılar ve dilim ipucu metni taşır.
 * - Koyu (#09090b zemin): #a52d6c, #e8505f, #b4900a, #4a8ae0 — tüm denetimler geçer; Error↔Warning renk körlüğü
 *   ayrımı 6,4 (6–8 bandı) ikincil kodlamayla yasaldır: açıklama, 2 px boşluk ve ipucu.
 */
export const DERECE_RENGI: Record<string, string> = {
  Critical: "bg-[#9f1d1d] dark:bg-[#a52d6c]",
  Error: "bg-[#e0473a] dark:bg-[#e8505f]",
  Warning: "bg-[#f2a01a] dark:bg-[#b4900a]",
  Info: "bg-[#3a7bd5] dark:bg-[#4a8ae0]",
};

/** SVG çizimleri için aynı renkler (`fill`/`stroke`). */
export const DERECE_CIZGISI: Record<string, string> = {
  Critical: "stroke-[#9f1d1d] dark:stroke-[#a52d6c]",
  Error: "stroke-[#e0473a] dark:stroke-[#e8505f]",
  Warning: "stroke-[#f2a01a] dark:stroke-[#b4900a]",
  Info: "stroke-[#3a7bd5] dark:stroke-[#4a8ae0]",
};

/**
 * Kaydı olmayan dilimin taban çizgisi: bağlantının görünen tek izi. zinc-500 (#71717b) beyaz zeminde ≈4,8:1,
 * zinc-950 zeminde ≈4,1:1 (WCAG 1.4.11; review 1, F-1).
 */
export const TABAN_RENGI = "bg-zinc-500";

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
    <span aria-hidden="true" data-oran={oran} className="mt-1 block h-1.5 w-full rounded-full bg-zinc-100 dark:bg-zinc-800">
      <span className={`block h-1.5 rounded-full ${renk}`} style={{ width: `${oran}%` }} />
    </span>
  );
}

/** Birden çok parçalı yatay çubuk; parçalar arasında zemin rengi 2 px boşluk bırakır. */
export function YiginCubuk({ parcalar }: { parcalar: { ad: string; oran: number; renk: string }[] }) {
  return (
    <span aria-hidden="true" className="mt-3 flex h-2 w-full gap-0.5 overflow-hidden rounded-full">
      {parcalar.filter((p) => p.oran > 0).map((p) => (
        <span key={p.ad} data-durum={p.ad} data-oran={p.oran} className={`block h-2 ${p.renk}`} style={{ width: `${p.oran}%` }} />
      ))}
    </span>
  );
}
