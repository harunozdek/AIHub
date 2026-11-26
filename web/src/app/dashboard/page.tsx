import type { Metadata } from "next";
import { cookies } from "next/headers";
import { Suspense } from "react";
import { YenileDugmesi } from "@/components/dashboard/bolum";
import { LogBolumleri, LogBolumleriYukleniyor } from "@/components/dashboard/log-bolumleri";
import { PencereSecimi } from "@/components/dashboard/pencere-secimi";
import { UygulamaBolumu, UygulamaBolumuYukleniyor } from "@/components/dashboard/uygulama-bolumu";
import { SaatDilimiCerezi } from "@/components/saat-dilimi-cerezi";
import type { DashboardQuery } from "@/lib/api-client";
import {
  EN_ILERI_SAAT_DILIMI,
  SAAT_DILIMI_CEREZI,
  aralikSinirlari,
  bugunDilimde,
  gecerliSaatDilimi,
  secimAdi,
  secimAdresi,
  secimAnahtari,
  secimiOku,
} from "@/lib/dashboard-pencere";

export const metadata: Metadata = { title: "AIHub — Dashboard" };

/**
 * Genel durum ekranı (Spec 0006; Spec 0011). Log bölümleri ve uygulama bölümü ayrı yüklenir: biri yüklenirken ya da
 * arızalıyken öteki kendi durumunu gösterir (R-9). Veri ekran açılırken bir kez gelir; "Yenile" seçili pencereyi
 * koruyarak yeniden yükler (R-8, CB-1). Seçim adreste durur; Suspense'in anahtarı seçim olduğu için yeni seçimde
 * yükleniyor durumu görünür ve önceki sonuç ekrana gelmez (CB-5).
 *
 * Serbest aralığın gün sınırları kullanıcının yerel saatine göredir (CB-7). Sunucu saat dilimini tarayıcının yazdığı
 * çerezden okur; çerez yoksa bölümler yükleniyor görünür, çerez yazılır ve sayfa bir kez yenilenir (plan 0011, risk 3).
 */
export default async function DashboardPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const dilim = gecerliSaatDilimi((await cookies()).get(SAAT_DILIMI_CEREZI)?.value);
  // Dilim bilinmiyorken aralık en ileri saat dilimine (UTC+14) göre gelecekte değilse kabul edilir; gerçek doğrulama çerez
  // yazılıp sayfa yenilenince kullanıcının dilimiyle yapılır. UTC'ye göre doğrulamak, UTC'nin önündeki dilimde gece
  // yarısından sonra paylaşılan "bugün" aralığını sessizce 24 saate düşürürdü (Review 1, F-2).
  const secim = secimiOku(params, bugunDilimde(dilim ?? EN_ILERI_SAAT_DILIMI));
  const ad = secimAdi(secim);
  const saatlik = secim.tur === "hazir" && secim.pencere.deger === "24s";
  const returnTo = params.pencere === undefined ? "/dashboard" : secimAdresi(secim);

  // Aralık seçiliyken saat dilimi bilinmiyorsa veri istenmez: UTC ile hesaplanmış yanlış günler hiç görünmez.
  const bekle = secim.tur === "aralik" && dilim === null;
  const sorgu: DashboardQuery | null =
    secim.tur === "hazir" ? { window: secim.pencere.api } : dilim ? aralikSinirlari(secim.baslangic, secim.bitis, dilim) : null;

  return (
    <>
      <SaatDilimiCerezi mevcut={dilim} yenile={bekle} />
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-xl font-semibold">Dashboard</h1>
        <div className="flex flex-wrap items-end gap-3">
          <PencereSecimi secim={secim} />
          <YenileDugmesi />
        </div>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <Suspense key={secimAnahtari(secim)} fallback={<LogBolumleriYukleniyor ad={ad} saatlik={saatlik} />}>
          {sorgu ? <LogBolumleri sorgu={sorgu} ad={ad} saatlik={saatlik} returnTo={returnTo} /> : <LogBolumleriYukleniyor ad={ad} saatlik={saatlik} />}
        </Suspense>
        <Suspense fallback={<UygulamaBolumuYukleniyor />}>
          <UygulamaBolumu returnTo={returnTo} />
        </Suspense>
      </div>
    </>
  );
}
