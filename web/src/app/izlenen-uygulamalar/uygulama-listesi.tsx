"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { DurumSimgesi } from "@/components/durum-simgesi";
import { LocalTime } from "@/lib/format-time";
import { failureText, statusText, type EkranUygulamasi } from "@/lib/health";
import { SatirIslemleri, type SatirIsi } from "./satir-islemleri";

/** Ekranın yoklama aralığı (Spec 0012, CB-6). */
export const YOKLAMA_MS = 10_000;

const hucre = "px-3 py-2 align-top";

/**
 * "İzlenen uygulamalar" listesi (Spec 0012). İki bölüm: "Down" ve "Ayakta" (Bilinmiyor dahil). Geniş ekranda Ayakta
 * genişliğin 2/3'ü solda, Down 1/3'ü sağda; dar ekranda alt alta ve Down üstte (CB-2). DOM sırası Down önce: dar ekranda
 * görünen sırayla aynıdır (plan 0012, risk 4).
 *
 * İlk veri sunucudan gelir (ilk açılış, yükleniyor ve hata 0005'teki gibi). Sonra liste 10 sn'de bir ve sekme yeniden
 * görünür olunca `/durum/uygulamalar`'dan okunur (CB-6; plan 0012, risk 7). Ekleme, düzenleme ve çıkarmadan sonra
 * sunucunun yeni verisi hemen alınır; ondan önce başlamış bir yoklamanın sonucu atılır (E-5).
 *
 * Satırdaki iş (açık form, taslak değer, hata ve odak) satırın dışında, uygulama kimliğine bağlı tutulur: uygulama bölüm
 * değiştirince satır yeniden kurulsa da form açık kalır, değer ve odak geri gelir (CB-6; plan 0012, risk 4).
 */
export function UygulamaListesi({ ilk }: { ilk: EkranUygulamasi[] }) {
  const [uygulamalar, setUygulamalar] = useState(ilk);
  const [eskiIlk, setEskiIlk] = useState(ilk);
  const [bayat, setBayat] = useState(false);
  const [duyuru, setDuyuru] = useState("");
  const [isler, setIsler] = useState<Record<string, SatirIsi>>({});
  const nesil = useRef(0);
  const oncekiDurumlar = useRef(new Map(ilk.map((a) => [a.id, a.status] as const)));

  // Sunucudan yeni ilk veri (server action sonrası): hemen gösterilir; o ana kadar başlamış yoklamalar atılır.
  if (eskiIlk !== ilk) {
    setEskiIlk(ilk);
    setUygulamalar(ilk);
  }
  useEffect(() => {
    nesil.current++;
  }, [ilk]);

  // Duyuru yalnızca ekran açıkken olan Down'a giriş ve Down'dan çıkış için (CB-7, E-4); ilk açılış duyurulmaz.
  useEffect(() => {
    const onceki = oncekiDurumlar.current;
    const mesajlar = uygulamalar.flatMap((a) => {
      const eski = onceki.get(a.id);
      if (eski === undefined || eski === a.status || (eski !== "Down" && a.status !== "Down")) return [];
      return [a.status === "Down" ? `${a.serviceName} Down oldu` : `${a.serviceName} ${statusText(a.status)}`];
    });
    oncekiDurumlar.current = new Map(uygulamalar.map((a) => [a.id, a.status] as const));
    if (mesajlar.length > 0) setDuyuru(mesajlar.join(". ") + ".");
  }, [uygulamalar]);

  useEffect(() => {
    let iptal = false;
    async function oku() {
      const benim = nesil.current;
      try {
        const yanit = await fetch("/durum/uygulamalar", { cache: "no-store", redirect: "manual" });
        if (!yanit.ok) throw new Error(String(yanit.status));
        const veri = (await yanit.json()) as { uygulamalar: EkranUygulamasi[] };
        if (iptal || benim !== nesil.current) return;
        setUygulamalar(veri.uygulamalar);
        setBayat(false);
      } catch {
        if (!iptal && benim === nesil.current) setBayat(true);
      }
    }
    const zamanlayici = setInterval(oku, YOKLAMA_MS);
    const gorununce = () => {
      if (document.visibilityState === "visible") void oku();
    };
    document.addEventListener("visibilitychange", gorununce);
    return () => {
      iptal = true;
      clearInterval(zamanlayici);
      document.removeEventListener("visibilitychange", gorununce);
    };
  }, []);

  const isGuncelle = useCallback((id: string, degisiklik: Partial<SatirIsi> | null) => {
    setIsler((onceki) => {
      if (degisiklik === null) {
        const kalan = { ...onceki };
        delete kalan[id];
        return kalan;
      }
      return { ...onceki, [id]: { ...(onceki[id] ?? { mod: "idle" }), ...degisiklik } };
    });
  }, []);

  const down = uygulamalar.filter((a) => a.status === "Down");
  const ayakta = uygulamalar.filter((a) => a.status !== "Down");

  return (
    <>
      {/* Duyuru ve not bölgeleri sürekli DOM'dadır; içerikleri değişince okunur. */}
      <p aria-live="polite" className="sr-only">{duyuru}</p>
      <div aria-live="polite">
        {bayat && (
          <p className="mb-4 rounded border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
            Durum güncellenemedi; son bilinen durum gösteriliyor.
          </p>
        )}
      </div>

      {uygulamalar.length === 0 ? (
        <div role="status" className="rounded border border-dashed border-zinc-300 p-10 text-center dark:border-zinc-700">
          <p className="font-medium">Henüz izlenen uygulama yok</p>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            Yukarıdaki &ldquo;Uygulama ekle&rdquo; formuyla bir ServiceName ve sağlık adresi girin.
          </p>
        </div>
      ) : (
        <div className="grid min-w-0 gap-4 md:grid-cols-3">
          <section aria-label="Down" className="min-w-0 rounded border border-zinc-200 p-4 md:order-2 md:col-span-1 dark:border-zinc-800">
            <h2 className="mb-3 text-lg font-semibold">Down ({down.length})</h2>
            {down.length === 0 ? (
              <p className="flex items-center gap-2 text-sm">
                <DurumSimgesi status="Up" />
                Down olan uygulama yok
              </p>
            ) : (
              <ul className="grid gap-3">
                {down.map((app) => (
                  <li key={app.id} className="rounded border border-red-200 bg-red-50 p-3 text-sm dark:border-red-900 dark:bg-red-950">
                    <p className="flex items-center gap-2 font-semibold text-red-800 dark:text-red-200">
                      <DurumSimgesi status="Down" />
                      <span>{statusText(app.status)}</span>
                      <span className="min-w-0 break-all font-mono text-zinc-900 dark:text-zinc-50">{app.serviceName}</span>
                    </p>
                    <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
                      <Alan ad="Sağlık adresi"><span className="break-all font-mono text-xs">{app.healthUrl}</span></Alan>
                      <Alan ad="Down olduğu zaman">{app.downSince ? <LocalTime iso={app.downSince} /> : "—"}</Alan>
                      <Alan ad="Son başarısızlık">{failureText(app.lastFailureReason, app.lastFailureStatusCode)}</Alan>
                      <Alan ad="Son denetim">{app.lastCheckedAt ? <LocalTime iso={app.lastCheckedAt} /> : "—"}</Alan>
                    </dl>
                    <div className="mt-2">
                      <SatirIslemleri app={app} is={isler[app.id]} isGuncelle={isGuncelle} />
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section aria-label="Ayakta" className="min-w-0 rounded border border-zinc-200 p-4 md:order-1 md:col-span-2 dark:border-zinc-800">
            <h2 className="mb-3 text-lg font-semibold">Ayakta ({ayakta.length})</h2>
            {ayakta.length === 0 ? (
              <p className="text-sm">Ayakta uygulama yok</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-sm">
                  <caption className="sr-only">Ayakta ve durumu bilinmeyen uygulamalar</caption>
                  <thead className="border-b border-zinc-300 dark:border-zinc-700">
                    <tr>
                      {["Durum", "ServiceName", "Sağlık adresi", "Son denetim"].map((baslik) => (
                        <th key={baslik} scope="col" className="px-3 py-2 font-semibold">{baslik}</th>
                      ))}
                      <th scope="col" className="px-3 py-2 font-semibold"><span className="sr-only">İşlem</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {ayakta.map((app) => (
                      <tr key={app.id} className="border-b border-zinc-200 dark:border-zinc-800">
                        <td className={hucre}>
                          <span className="flex items-center gap-2 whitespace-nowrap">
                            <DurumSimgesi status={app.status} />
                            {statusText(app.status)}
                          </span>
                        </td>
                        <td className={`${hucre} break-all font-mono`}>{app.serviceName}</td>
                        <td className={`${hucre} max-w-xs break-all font-mono text-xs`}>{app.healthUrl}</td>
                        <td className={hucre}>{app.lastCheckedAt ? <LocalTime iso={app.lastCheckedAt} /> : "—"}</td>
                        <td className={hucre}>
                          <SatirIslemleri app={app} is={isler[app.id]} isGuncelle={isGuncelle} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}
    </>
  );
}

function Alan({ ad, children }: { ad: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-zinc-600 dark:text-zinc-300">{ad}</dt>
      <dd className="min-w-0">{children}</dd>
    </>
  );
}

