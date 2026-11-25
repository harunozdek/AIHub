import { DERECE_CIZGISI } from "./oran-cubugu";

const sayi = new Intl.NumberFormat("tr-TR");

const YARICAP = 48;
const CEVRE = 2 * Math.PI * YARICAP;
/** Dilimler arasında zemin rengi boşluk (çevre birimi). */
const BOSLUK = 2;

/**
 * Önem derecelerinin toplama oranı, halka grafik (Spec 0007, CB-3; kullanıcı isteği, spec revizyonu 1). Ortada
 * toplam yazar. Grafik yalnızca görseldir: her derecenin adı, sayısı ve yüzdesi yanındaki listede metin olarak
 * durur, bu yüzden ekran okuyucudan gizlenir.
 */
export function DereceHalkasi({ dereceler }: { dereceler: { level: string; count: number }[] }) {
  const toplam = dereceler.reduce((t, l) => t + l.count, 0);
  const dolu = dereceler.filter((l) => l.count > 0);
  const bosluk = dolu.length > 1 ? BOSLUK : 0;
  const uzunluklar = dolu.map((l) => (l.count / toplam) * CEVRE);
  const baslangiclar = uzunluklar.map((_, i) => uzunluklar.slice(0, i).reduce((t, u) => t + u, 0));

  return (
    <div className="relative size-36 shrink-0">
      <svg aria-hidden="true" viewBox="0 0 120 120" className="size-36 -rotate-90">
        <circle cx="60" cy="60" r={YARICAP} fill="none" strokeWidth="14" className="stroke-zinc-100 dark:stroke-zinc-800" />
        {dolu.map((l, i) => (
          <circle
            key={l.level}
            data-derece={l.level}
            cx="60"
            cy="60"
            r={YARICAP}
            fill="none"
            strokeWidth="14"
            className={DERECE_CIZGISI[l.level]}
            strokeDasharray={`${Math.max(uzunluklar[i] - bosluk, 0.5)} ${CEVRE}`}
            strokeDashoffset={-baslangiclar[i]}
          />
        ))}
      </svg>
      <div aria-hidden="true" className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-xl font-semibold tabular-nums">{sayi.format(toplam)}</span>
        <span className="text-xs text-zinc-500 dark:text-zinc-400">kayıt</span>
      </div>
    </div>
  );
}
