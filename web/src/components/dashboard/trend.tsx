"use client";

import Link from "next/link";
import type { TrendSlice } from "@/lib/api-client";
import { listHref } from "@/lib/filters";
import { useLocalClock, useTimeZoneName } from "@/lib/format-time";
import { DERECE_RENGI } from "./oran-cubugu";

const sayi = new Intl.NumberFormat("tr-TR");

/** Yığında yukarıdan aşağı: en ağır derece üstte. Açıklama da bu sırayla yazılır. */
const DERECELER = ["Critical", "Error", "Warning", "Info"] as const;

const toplam = (d: TrendSlice) => d.levels.reduce((t, l) => t + l.count, 0);

/**
 * Son 24 saatin saatlik dağılımı (Spec 0007, CB-1, CB-2, CB-5). Her dilim, dilimin from/to değerleriyle log
 * listesini açan bir bağlantıdır; adı saat aralığını ve dört derecenin sayısını yazar. Saatler yerel saattedir:
 * ilk çizim UTC, tarayıcıda yerel saate döner. Eksende her 3 dilimde bir başlangıç saati görünür.
 */
export function Trend({ dilimler }: { dilimler: TrendSlice[] }) {
  const enBuyuk = Math.max(1, ...dilimler.map(toplam));
  const bolge = useTimeZoneName(dilimler[0].from);

  return (
    <>
      <ul aria-label="Dereceler" className="mb-3 flex flex-wrap gap-x-4 gap-y-1 text-sm">
        {DERECELER.map((d) => (
          <li key={d} className="flex items-center gap-1.5">
            <span aria-hidden="true" className={`inline-block size-3 rounded-sm ${DERECE_RENGI[d]}`} />
            {d}
          </li>
        ))}
      </ul>
      <ol aria-label={`Saatlik dağılım (${bolge})`} className="flex h-40 gap-0.5">
        {dilimler.map((d, i) => (
          // Görünen bitiş, bir sonraki dilimin başlangıcıdır; bağlantı dilimin kendi to değerini taşır.
          <Dilim key={d.from} dilim={d} bitis={dilimler[i + 1]?.from ?? d.to} enBuyuk={enBuyuk} />
        ))}
      </ol>
      <ol aria-hidden="true" className="mt-1 grid grid-cols-24 text-xs text-zinc-600 dark:text-zinc-400">
        {dilimler.map((d, i) => (
          <li key={d.from} className="whitespace-nowrap">{i % 3 === 0 && <Saat iso={d.from} />}</li>
        ))}
      </ol>
      <p className="mt-2 text-xs text-zinc-600 dark:text-zinc-400">Saatler {bolge} saat dilimindedir.</p>
    </>
  );
}

function Saat({ iso }: { iso: string }) {
  return <time dateTime={iso}>{useLocalClock(iso)}</time>;
}

function Dilim({ dilim, bitis, enBuyuk }: { dilim: TrendSlice; bitis: string; enBuyuk: number }) {
  const aralik = `${useLocalClock(dilim.from)}–${useLocalClock(bitis)}`;
  const sayilar = DERECELER.map((d) => ({ derece: d, sayi: dilim.levels.find((l) => l.level === d)?.count ?? 0 }));
  const metin = `${aralik}: ${sayilar.map((s) => `${s.derece} ${sayi.format(s.sayi)}`).join(", ")}`;
  const tum = toplam(dilim);

  return (
    <li className="flex-1">
      <Link
        href={listHref({ from: dilim.from, to: dilim.to, levels: [] })}
        aria-label={`${metin} kayıt, log listesinde aç`}
        title={metin}
        data-toplam={tum}
        className="flex h-full flex-col justify-end gap-px rounded-sm hover:bg-zinc-100 dark:hover:bg-zinc-900"
      >
        {tum === 0 ? (
          <span className="block h-px bg-zinc-400 dark:bg-zinc-600" />
        ) : (
          sayilar
            .filter((s) => s.sayi > 0)
            .map((s) => (
              <span key={s.derece} className={`block min-h-px ${DERECE_RENGI[s.derece]}`} style={{ height: `${(s.sayi / enBuyuk) * 100}%` }} />
            ))
        )}
      </Link>
    </li>
  );
}
