import Link from "next/link";
import { getDashboard } from "@/lib/api-client";
import { listHref } from "@/lib/filters";
import { LocalTime } from "@/lib/format-time";
import { Bolum, BolumHatasi, BosBolum } from "./bolum";

const sayi = new Intl.NumberFormat("tr-TR");
const link = "text-blue-700 underline hover:text-blue-900 dark:text-blue-300";

/**
 * Dashboard'un log bölümleri (Spec 0006, R-2 … R-5, R-7): tek istekle gelir. Her sayı, aynı pencere ve ilgili
 * filtrelerle log listesini açar. Okunamazsa üç bölüm de kendi hata durumunu gösterir (R-9).
 */
export async function LogBolumleri() {
  const ozet = await getDashboard("/dashboard");

  if (!ozet.ok) {
    return (
      <>
        <Bolum id="dereceler" baslik="Önem dereceleri"><BolumHatasi mesaj="Log sayıları şu anda okunamıyor." /></Bolum>
        <Bolum id="servisler" baslik="En çok hata üreten servisler"><BolumHatasi mesaj="Servis özeti şu anda okunamıyor." /></Bolum>
        <Bolum id="exceptionlar" baslik="En sık exception'lar"><BolumHatasi mesaj="Exception özeti şu anda okunamıyor." /></Bolum>
      </>
    );
  }

  const d = ozet.data;
  const pencere = { from: d.from, to: d.to };
  const toplam = d.levels.reduce((t, l) => t + l.count, 0);

  return (
    <>
      <p className="text-sm text-zinc-600 md:col-span-2 dark:text-zinc-400">
        Son 24 saat: <LocalTime iso={d.from} /> – <LocalTime iso={d.to} />. Yüklendi: <LocalTime iso={d.to} />.
      </p>

      <Bolum id="dereceler" baslik="Önem dereceleri">
        {toplam === 0 ? (
          <BosBolum>Son 24 saatte kayıt yok</BosBolum>
        ) : (
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {d.levels.map((l) => (
              <li key={l.level} className="rounded border border-zinc-200 p-3 dark:border-zinc-800">
                <Link href={listHref({ levels: [l.level], ...pencere })} className={link}>
                  <span className="block text-sm font-medium">{l.level}</span>{" "}
                  <span className="block text-2xl font-semibold">{sayi.format(l.count)}</span>
                  <span className="sr-only"> kayıt, log listesinde aç</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Bolum>

      <Bolum id="servisler" baslik="En çok hata üreten servisler">
        {d.topErrorServices.length === 0 ? (
          <BosBolum>Son 24 saatte hata yok</BosBolum>
        ) : (
          <Tablo
            basliklar={["Servis", "Error + Critical"]}
            satirlar={d.topErrorServices.map((s) => ({
              anahtar: s.serviceName,
              hucreler: [
                s.serviceName,
                <Link key="n" href={listHref({ service: s.serviceName, levels: ["Error", "Critical"], ...pencere })} className={link} aria-label={`${s.serviceName}: ${sayi.format(s.count)} hata, log listesinde aç`}>
                  {sayi.format(s.count)}
                </Link>,
              ],
            }))}
          />
        )}
      </Bolum>

      <Bolum id="exceptionlar" baslik="En sık exception'lar">
        {d.topExceptionTypes.length === 0 ? (
          <BosBolum>Son 24 saatte exception yok</BosBolum>
        ) : (
          <Tablo
            basliklar={["Exception türü", "Sayı", "Son görülme"]}
            satirlar={d.topExceptionTypes.map((t) => ({
              anahtar: t.exceptionType,
              hucreler: [
                <span key="t" className="break-all font-mono text-sm">{t.exceptionType}</span>,
                <Link key="n" href={listHref({ exceptionType: t.exceptionType, levels: [], ...pencere })} className={link} aria-label={`${t.exceptionType}: ${sayi.format(t.count)} kayıt, log listesinde aç`}>
                  {sayi.format(t.count)}
                </Link>,
                <LocalTime key="z" iso={t.lastSeenAt} />,
              ],
            }))}
          />
        )}
      </Bolum>
    </>
  );
}

function Tablo({ basliklar, satirlar }: { basliklar: string[]; satirlar: { anahtar: string; hucreler: React.ReactNode[] }[] }) {
  return (
    <table className="w-full text-left text-sm">
      <thead>
        <tr className="border-b border-zinc-200 dark:border-zinc-800">
          {basliklar.map((b) => <th key={b} scope="col" className="py-1.5 pr-3 font-medium">{b}</th>)}
        </tr>
      </thead>
      <tbody>
        {satirlar.map((s) => (
          <tr key={s.anahtar} className="border-b border-zinc-100 dark:border-zinc-900">
            {s.hucreler.map((h, i) => <td key={i} className="py-1.5 pr-3">{h}</td>)}
          </tr>
        ))}
      </tbody>
    </table>
  );
}


