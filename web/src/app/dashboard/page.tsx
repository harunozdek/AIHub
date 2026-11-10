import type { Metadata } from "next";
import { Suspense } from "react";
import { BolumYukleniyor, YenileDugmesi } from "@/components/dashboard/bolum";
import { LogBolumleri } from "@/components/dashboard/log-bolumleri";
import { UygulamaBolumu } from "@/components/dashboard/uygulama-bolumu";

export const metadata: Metadata = { title: "AIHub — Dashboard" };

/**
 * Genel durum ekranı (Spec 0006). Log bölümleri ve uygulama bölümü ayrı yüklenir: biri yüklenirken ya da
 * arızalıyken öteki kendi durumunu gösterir (R-9). Veri ekran açılırken bir kez gelir; "Yenile" yeniden yükler (R-8).
 */
export default function DashboardPage() {
  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Dashboard</h1>
        <YenileDugmesi />
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <Suspense fallback={<BolumYukleniyor metin="Log özeti yükleniyor…" />}>
          <LogBolumleri />
        </Suspense>
        <Suspense fallback={<BolumYukleniyor metin="İzlenen uygulamalar yükleniyor…" />}>
          <UygulamaBolumu />
        </Suspense>
      </div>
    </>
  );
}
