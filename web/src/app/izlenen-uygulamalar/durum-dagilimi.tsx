import { yuzde } from "@/components/dashboard/oran-cubugu";
import { DurumSimgesi, durumRengi } from "@/components/durum-simgesi";
import type { HealthStatus } from "@/lib/api-client";
import { statusText, type EkranUygulamasi } from "@/lib/health";

const sayi = new Intl.NumberFormat("tr-TR");

/** Açıklamanın ve dilimlerin sırası (Spec 0013, CB-2). */
const SIRA: HealthStatus[] = ["Up", "Down", "Unknown"];

const YARICAP = 48;
const CEVRE = 2 * Math.PI * YARICAP;
/** Dilimler arasında zemin rengi boşluk (çevre birimi; Spec 0013, CB-5 revizyon 1). 0007'deki halkayla aynı. */
const BOSLUK = 2;

/**
 * İzlenen uygulamaların durumlara göre dağılımı (Spec 0013): halka grafik ve açıklama. Listeyle aynı diziden, aynı
 * render'da çizilir; bu yüzden bölüm başlıklarıyla hiçbir an çelişmez (CB-3).
 *
 * Halka 0007'deki "Önem dereceleri" halkasıyla aynı yöntemle çizilir (dashboard'a dokunulmaz, plan 0013 risk 2).
 * Dilimler simgelerle aynı rengi aynı kaynaktan alır (`durumRengi`, CB-1). Renkler birbirine karşı 3:1 sağlamaz;
 * dilimleri zemin renginde boşluk ayırır ve her renk zemine karşı 3:1'i geçer (CB-5 revizyon 1). Arkada iz halkası
 * yoktur: boşlukta görünen, bölümün kendi zeminidir.
 *
 * Ekran okuyucu grafiği tek özet olarak okur (`role="img"`); dilimler ayrıca okunmaz, sayılar açıklamada metindir.
 * Bölge canlı değildir: grafik değişince duyuru yapılmaz (CB-5).
 */
export function DurumDagilimi({ uygulamalar }: { uygulamalar: EkranUygulamasi[] }) {
  const toplam = uygulamalar.length;
  const durumlar = SIRA.map((status) => ({ status, adet: uygulamalar.filter((a) => a.status === status).length }));
  const dolu = durumlar.filter((d) => d.adet > 0);
  const bosluk = dolu.length > 1 ? BOSLUK : 0;
  const uzunluklar = dolu.map((d) => (d.adet / toplam) * CEVRE);
  const baslangiclar = uzunluklar.map((_, i) => uzunluklar.slice(0, i).reduce((t, u) => t + u, 0));
  const ozet = `Durum dağılımı: ${sayi.format(toplam)} uygulama; ${durumlar.map((d) => `${statusText(d.status)} ${sayi.format(d.adet)}`).join(", ")}`;

  return (
    <section aria-label="Durum dağılımı" className="mb-4 min-w-0 rounded border border-zinc-200 p-4 dark:border-zinc-800">
      <h2 className="mb-3 text-lg font-semibold">Durum dağılımı</h2>
      <div className="flex flex-wrap items-center gap-6">
        <div role="img" aria-label={ozet} className="relative size-36 shrink-0">
          <svg aria-hidden="true" viewBox="0 0 120 120" className="size-36 -rotate-90">
            {dolu.map((d, i) => (
              <circle
                key={d.status}
                data-durum={d.status}
                data-oran={yuzde(d.adet, toplam)}
                cx="60"
                cy="60"
                r={YARICAP}
                fill="none"
                stroke="currentColor"
                strokeWidth="14"
                className={durumRengi(d.status)}
                strokeDasharray={`${Math.max(uzunluklar[i] - bosluk, 0.5)} ${CEVRE}`}
                strokeDashoffset={-baslangiclar[i]}
              />
            ))}
          </svg>
          <div aria-hidden="true" className="absolute inset-0 flex flex-col items-center justify-center">
            <span data-toplam className="text-xl font-semibold tabular-nums">{sayi.format(toplam)}</span>{" "}
            <span className="text-xs text-zinc-500 dark:text-zinc-400">uygulama</span>
          </div>
        </div>
        <ul className="min-w-40 flex-1 space-y-2">
          {durumlar.map((d) => (
            <li key={d.status} data-durum={d.status} className="flex items-center gap-2.5">
              <DurumSimgesi status={d.status} />
              <span className="flex-1 text-sm font-medium">{statusText(d.status)}</span>{" "}
              <span data-adet className="text-lg font-semibold tabular-nums">{sayi.format(d.adet)}</span>{" "}
              <span className="w-14 text-right text-sm tabular-nums text-zinc-500 dark:text-zinc-400">(%{yuzde(d.adet, toplam)})</span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
