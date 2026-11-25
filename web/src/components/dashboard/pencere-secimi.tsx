"use client";

import { useRouter } from "next/navigation";
import { useId, useState } from "react";
import {
  ARALIK,
  HAZIR_PENCERELER,
  araligiDogrula,
  secimAdi,
  secimAdresi,
  type Secim,
} from "@/lib/dashboard-pencere";

/** Tarayıcının yerel günü, `YYYY-MM-DD`. */
function yerelGun(gunEkle = 0): string {
  const t = new Date();
  t.setDate(t.getDate() + gunEkle);
  const iki = (n: number) => String(n).padStart(2, "0");
  return `${t.getFullYear()}-${iki(t.getMonth() + 1)}-${iki(t.getDate())}`;
}

const alan =
  "rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:border-zinc-700 dark:bg-zinc-900";

/**
 * Dashboard'un zaman penceresi seçimi (Spec 0011, CB-1, CB-6, CB-7). Etiketli yerel bir açılır liste: yalnızca klavyeyle
 * değiştirilir, odak görünürdür. Hazır pencere seçilince adres değişir ve log bölümleri yeni pencereyle yüklenir.
 * "Tarih aralığı seç" iki tarih alanı ve "Uygula" düğmesi açar; hatalı aralık uygulanmaz, alanın yanında nedeni
 * yazılır ve ekrandaki veri değişmez. Seçili pencere bir canlı bölgeden duyurulur.
 */
export function PencereSecimi({ secim }: { secim: Secim }) {
  const router = useRouter();
  const kimlik = useId();
  const [aralikAcik, setAralikAcik] = useState(secim.tur === "aralik");
  const [baslangic, setBaslangic] = useState(secim.tur === "aralik" ? secim.baslangic : "");
  const [bitis, setBitis] = useState(secim.tur === "aralik" ? secim.bitis : "");
  const [hata, setHata] = useState<{ alan: "baslangic" | "bitis"; mesaj: string } | null>(null);

  const deger = aralikAcik ? ARALIK : secim.tur === "hazir" ? secim.pencere.deger : ARALIK;

  function degisti(yeni: string) {
    if (yeni === ARALIK) {
      setAralikAcik(true);
      setHata(null);
      // İlk açılışta son 7 gün: 7 gün öncesi – bugün (S-5).
      if (!baslangic) setBaslangic(yerelGun(-7));
      if (!bitis) setBitis(yerelGun());
      return;
    }
    setAralikAcik(false);
    setHata(null);
    const pencere = HAZIR_PENCERELER.find((p) => p.deger === yeni)!;
    router.push(secimAdresi({ tur: "hazir", pencere }));
  }

  function uygula(e: React.FormEvent) {
    e.preventDefault();
    const sorun = araligiDogrula(baslangic, bitis, yerelGun());
    setHata(sorun);
    if (sorun) return;
    router.push(secimAdresi({ tur: "aralik", baslangic, bitis }));
  }

  const hataKimligi = `${kimlik}-hata`;
  const alanOzellikleri = (ad: "baslangic" | "bitis") =>
    hata?.alan === ad ? { "aria-invalid": true as const, "aria-describedby": hataKimligi } : {};

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex flex-col gap-1">
        <label htmlFor={`${kimlik}-pencere`} className="text-sm font-medium">
          Zaman penceresi
        </label>
        <select id={`${kimlik}-pencere`} value={deger} onChange={(e) => degisti(e.target.value)} className={alan}>
          {HAZIR_PENCERELER.map((p) => (
            <option key={p.deger} value={p.deger}>
              {p.ad}
            </option>
          ))}
          <option value={ARALIK}>Tarih aralığı seç</option>
        </select>
      </div>

      {aralikAcik && (
        <form onSubmit={uygula} noValidate className="flex flex-wrap items-end gap-3">
          <div className="flex flex-col gap-1">
            <label htmlFor={`${kimlik}-baslangic`} className="text-sm font-medium">
              Başlangıç tarihi
            </label>
            <input id={`${kimlik}-baslangic`} type="date" value={baslangic} onChange={(e) => setBaslangic(e.target.value)} className={alan} {...alanOzellikleri("baslangic")} />
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor={`${kimlik}-bitis`} className="text-sm font-medium">
              Bitiş tarihi
            </label>
            <input id={`${kimlik}-bitis`} type="date" value={bitis} onChange={(e) => setBitis(e.target.value)} className={alan} {...alanOzellikleri("bitis")} />
          </div>
          <button type="submit" className="rounded bg-blue-700 px-3 py-1.5 text-sm text-white hover:bg-blue-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600">
            Uygula
          </button>
          {hata && (
            <p id={hataKimligi} role="alert" className="basis-full text-sm text-red-700 dark:text-red-400">
              {hata.mesaj}
            </p>
          )}
        </form>
      )}

      {/* Pencere değişince ekran okuyucu yeni pencereyi duyar (CB-6). */}
      <p aria-live="polite" className="sr-only">
        Zaman penceresi: {secimAdi(secim).ad}
      </p>
    </div>
  );
}
