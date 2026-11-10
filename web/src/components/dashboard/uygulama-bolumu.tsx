import Link from "next/link";
import { listMonitoredApps, type HealthStatus } from "@/lib/api-client";
import { LocalTime } from "@/lib/format-time";
import { statusText } from "@/lib/health";
import { Bolum, BolumHatasi, BolumYukleniyor, BosBolum } from "./bolum";

const link = "text-blue-700 underline hover:text-blue-900 dark:text-blue-300";

/**
 * İzlenen uygulamaların durum özeti (Spec 0006, R-6): durum sayıları ve Down olanlar. Özet, izlenen uygulamalar
 * ekranını açar (R-7). Okunamazsa yalnızca bu bölüm hata gösterir (R-9).
 */
export async function UygulamaBolumu() {
  const uygulamalar = await listMonitoredApps("/dashboard");

  if (!uygulamalar.ok) {
    return <Bolum id="uygulamalar" baslik="İzlenen uygulamalar"><BolumHatasi mesaj="İzlenen uygulamalar şu anda okunamıyor." /></Bolum>;
  }

  const liste = uygulamalar.data;
  if (liste.length === 0) {
    return (
      <Bolum id="uygulamalar" baslik="İzlenen uygulamalar">
        <BosBolum>Henüz izlenen uygulama yok</BosBolum>
        <Link href="/izlenen-uygulamalar" className={`mt-2 inline-block text-sm ${link}`}>İzlenen uygulamalar ekranına git</Link>
      </Bolum>
    );
  }

  const say = (s: HealthStatus) => liste.filter((a) => a.status === s).length;
  const down = liste.filter((a) => a.status === "Down");

  return (
    <Bolum id="uygulamalar" baslik="İzlenen uygulamalar">
      <Link href="/izlenen-uygulamalar" className={link}>
        <ul className="flex flex-wrap gap-4 text-sm">
          {(["Up", "Down", "Unknown"] as const).map((s) => (
            <li key={s}>
              <span className="font-medium">{statusText(s)}:</span> <span className="text-lg font-semibold">{say(s)}</span>
            </li>
          ))}
        </ul>
        <span className="sr-only">Durum özeti, izlenen uygulamalar ekranında aç</span>
      </Link>
      {down.length > 0 && (
        <ul aria-label="Down olan uygulamalar" className="mt-3 space-y-1 text-sm">
          {down.map((a) => (
            <li key={a.id} className="rounded bg-red-50 px-2 py-1 text-red-900 dark:bg-red-950 dark:text-red-100">
              <span className="font-medium">{a.serviceName}</span> — Down
              {a.downSince && <>, <LocalTime iso={a.downSince} /> zamanından beri</>}
            </li>
          ))}
        </ul>
      )}
    </Bolum>
  );
}

/** Uygulama bölümü yüklenirken başlığıyla ve yükleniyor satırıyla görünür (Spec 0006, States). */
export function UygulamaBolumuYukleniyor() {
  return <Bolum id="uygulamalar" baslik="İzlenen uygulamalar"><BolumYukleniyor metin="İzlenen uygulamalar yükleniyor…" /></Bolum>;
}
