import type { Metadata } from "next";
import { listMonitoredApps } from "@/lib/api-client";
import { ekranUygulamasi } from "@/lib/health";
import { UygulamaFormu } from "./uygulama-formu";
import { UygulamaListesi } from "./uygulama-listesi";

export const metadata: Metadata = { title: "AIHub — İzlenen uygulamalar" };

/**
 * "İzlenen uygulamalar" ekranı (Spec 0005, R-2, AC-10; Spec 0012). Ekleme formu ve liste. İlk veri burada, sunucuda
 * okunur: ilk açılışın yükleniyor ve hata ("yeniden dene") durumları 0005'teki gibidir (Spec 0012, PB-4). Liste
 * "Ayakta" ve "Down" bölümleriyle ve 10 sn'lik güncellemeyle istemcide çizilir (`UygulamaListesi`). Ekleyen ve son
 * değiştiren ekrana taşınmaz (Spec 0012, CB-3).
 */
export default async function IzlenenUygulamalarPage() {
  const result = await listMonitoredApps("/izlenen-uygulamalar");
  if (!result.ok) {
    // Hata sınırı "yeniden dene" gösterir (error.tsx).
    throw new Error("İzlenen uygulamalar okunamadı.");
  }

  return (
    <>
      <h1 className="mb-4 text-xl font-semibold">İzlenen uygulamalar</h1>
      <UygulamaFormu mode="add" />
      <UygulamaListesi ilk={result.data.map(ekranUygulamasi)} />
    </>
  );
}
