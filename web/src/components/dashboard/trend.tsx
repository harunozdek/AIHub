"use client";

import Link from "next/link";
import type { TrendSlice } from "@/lib/api-client";
import { listHref } from "@/lib/filters";
import { useLocalClock, useTimeZoneName } from "@/lib/format-time";
import { DERECE_RENGI } from "./oran-cubugu";

const sayi = new Intl.NumberFormat("tr-TR");

/** Yığında yukarıdan aşağı: en ağır derece üstte. Açıklama ve ipucu da bu sırayla yazılır. */
const DERECELER = ["Critical", "Error", "Warning", "Info"] as const;

const toplam = (d: TrendSlice) => d.levels.reduce((t, l) => t + l.count, 0);

/** Ölçeğin üst sınırı: 1, 2, 5 × 10ⁿ adımlarından en büyük dilimi karşılayan ilk değer. */
function olcekUstu(enBuyuk: number): number {
  const kuvvet = 10 ** Math.floor(Math.log10(Math.max(1, enBuyuk)));
  return [1, 2, 5, 10].map((k) => k * kuvvet).find((v) => v >= enBuyuk)!;
}

/**
 * Son 24 saatin saatlik dağılımı (Spec 0007, CB-1, CB-2, CB-5). Her dilim, dilimin from/to değerleriyle log
 * listesini açan bir bağlantıdır; adı saat aralığını ve dört derecenin sayısını yazar. Üzerine gelince ya da
 * odaklanınca aynı bilgi bir ipucunda görünür. Saatler yerel saattedir: ilk çizim UTC, tarayıcıda yerel saate
 * döner. Eksende her 3 dilimde bir başlangıç saati görünür. Çizim kuralları: çubuk en fazla 24 px, üstü 4 px
 * yuvarlak, tabanı düz; yığın parçaları arasında 2 px boşluk; ızgara çizgileri 1 px ve geri planda.
 */
export function Trend({ dilimler }: { dilimler: TrendSlice[] }) {
  const ust = olcekUstu(Math.max(...dilimler.map(toplam)));
  const bolge = useTimeZoneName(dilimler[0].from);

  return (
    <>
      <ul aria-label="Dereceler" className="mb-4 flex flex-wrap gap-x-4 gap-y-1 text-sm text-zinc-700 dark:text-zinc-300">
        {DERECELER.map((d) => (
          <li key={d} className="flex items-center gap-1.5">
            <span aria-hidden="true" className={`inline-block size-2.5 rounded-sm ${DERECE_RENGI[d]}`} />
            {d}
          </li>
        ))}
      </ul>

      <div className="flex gap-2">
        {/* Ölçek: 0, yarı ve üst değer; ızgara çizgileri grafiğin arkasında aynı hizada. */}
        <ol aria-hidden="true" className="flex h-48 flex-col-reverse justify-between text-right text-xs tabular-nums text-zinc-500 dark:text-zinc-400">
          {[0, ust / 2, ust].map((v) => (
            <li key={v} className="-my-2 leading-4">{sayi.format(v)}</li>
          ))}
        </ol>
        <div className="relative min-w-0 flex-1">
          <div aria-hidden="true" className="pointer-events-none absolute inset-x-0 top-0 h-48">
            {[0, 50, 100].map((y) => (
              <span key={y} className="absolute inset-x-0 h-px bg-zinc-200 dark:bg-zinc-800" style={{ bottom: `${y}%` }} />
            ))}
          </div>
          <ol aria-label={`Saatlik dağılım (${bolge})`} className="relative flex h-48">
            {dilimler.map((d, i) => (
              // Görünen bitiş, bir sonraki dilimin başlangıcıdır; bağlantı dilimin kendi to değerini taşır.
              <Dilim key={d.from} dilim={d} bitis={dilimler[i + 1]?.from ?? d.to} ust={ust} hiza={i < 4 ? "sol" : i > 19 ? "sag" : "orta"} />
            ))}
          </ol>
          <ol aria-hidden="true" className="mt-1.5 grid grid-cols-24 text-xs tabular-nums text-zinc-500 dark:text-zinc-400">
            {dilimler.map((d, i) => (
              <li key={d.from} className="whitespace-nowrap">{i % 3 === 0 && <Saat iso={d.from} />}</li>
            ))}
          </ol>
        </div>
      </div>
      <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">Saatler {bolge} saat dilimindedir. Bir saate tıklayınca o saatin kayıtları açılır.</p>
    </>
  );
}

function Saat({ iso }: { iso: string }) {
  return <time dateTime={iso}>{useLocalClock(iso)}</time>;
}

const IPUCU_HIZASI = { sol: "left-0", orta: "left-1/2 -translate-x-1/2", sag: "right-0" } as const;

function Dilim({ dilim, bitis, ust, hiza }: { dilim: TrendSlice; bitis: string; ust: number; hiza: keyof typeof IPUCU_HIZASI }) {
  const aralik = `${useLocalClock(dilim.from)}–${useLocalClock(bitis)}`;
  const sayilar = DERECELER.map((d) => ({ derece: d, sayi: dilim.levels.find((l) => l.level === d)?.count ?? 0 }));
  const tum = toplam(dilim);
  const dolu = sayilar.filter((s) => s.sayi > 0);

  return (
    <li className="group relative flex-1">
      <Link
        href={listHref({ from: dilim.from, to: dilim.to, levels: [] })}
        aria-label={`${aralik}: ${sayilar.map((s) => `${s.derece} ${sayi.format(s.sayi)}`).join(", ")} kayıt, log listesinde aç`}
        data-toplam={tum}
        className="flex h-full flex-col items-center justify-end rounded-sm px-px hover:bg-zinc-100 dark:hover:bg-zinc-900"
      >
        {tum === 0 ? (
          <span className="block h-0.5 w-full max-w-6 rounded-full bg-zinc-500" />
        ) : (
          <span className="flex w-full max-w-6 flex-col gap-0.5" style={{ height: `${(tum / ust) * 100}%` }}>
            {dolu.map((s, j) => (
              <span
                key={s.derece}
                className={`block min-h-0.5 ${DERECE_RENGI[s.derece]} ${j === 0 ? "rounded-t" : ""}`}
                style={{ flexGrow: s.sayi, flexBasis: 0 }}
              />
            ))}
          </span>
        )}
      </Link>
      {/* İpucu: üzerine gelince ve klavye odağında; aynı bilgi bağlantının adındadır (yalnızca görsel). */}
      <div
        aria-hidden="true"
        className={`pointer-events-none absolute top-0 z-10 hidden w-44 rounded-md border border-zinc-200 bg-white p-2.5 text-xs shadow-lg group-hover:block group-has-[:focus-visible]:block dark:border-zinc-700 dark:bg-zinc-900 ${IPUCU_HIZASI[hiza]}`}
      >
        <p className="mb-1.5 font-medium text-zinc-600 dark:text-zinc-300">{aralik}</p>
        <ul className="space-y-1">
          {sayilar.map((s) => (
            <li key={s.derece} className="flex items-center gap-2">
              <span className={`inline-block h-0.5 w-3 rounded-full ${DERECE_RENGI[s.derece]}`} />
              <span className="font-semibold tabular-nums text-zinc-900 dark:text-zinc-50">{sayi.format(s.sayi)}</span>
              <span className="text-zinc-500 dark:text-zinc-400">{s.derece}</span>
            </li>
          ))}
        </ul>
        <p className="mt-1.5 border-t border-zinc-200 pt-1.5 text-zinc-600 dark:border-zinc-700 dark:text-zinc-300">
          Toplam <span className="font-semibold tabular-nums text-zinc-900 dark:text-zinc-50">{sayi.format(tum)}</span>
        </p>
      </div>
    </li>
  );
}
