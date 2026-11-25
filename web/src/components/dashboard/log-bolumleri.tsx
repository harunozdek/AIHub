import Link from "next/link";
import { getDashboard, type DashboardQuery, type LogSummary } from "@/lib/api-client";
import { detailHref, listHref } from "@/lib/filters";
import { LocalTime } from "@/lib/format-time";
import { Bolum, BolumHatasi, BolumYukleniyor, BosBolum } from "./bolum";
import { DereceHalkasi } from "./derece-halkasi";
import { DERECE_RENGI, OranCubugu, yuzde } from "./oran-cubugu";
import { Trend } from "./trend";

const sayi = new Intl.NumberFormat("tr-TR");
const link = "text-blue-700 underline hover:text-blue-900 dark:text-blue-300";

/** Seçili pencerenin adı ("Son 1 hafta") ve bulunma eki ("Son 1 haftada"); Spec 0011, CB-2. */
export type PencereAdi = { ad: string; bulunma: string };

/** Trend bölümünün adı pencerenin adıdır; 24 saatlik dilimlerde "saatlik" (Spec 0007), diğerlerinde "dağılım". */
function trendMetni(ad: PencereAdi, saatlik: boolean) {
  return saatlik
    ? { yukleniyor: "Saatlik dağılım yükleniyor…", hata: "Saatlik dağılım şu anda okunamıyor." }
    : { yukleniyor: "Dağılım yükleniyor…", hata: "Dağılım şu anda okunamıyor." };
}

const ERROR_SEVIYELERI = ["Error", "Critical"];
const WARNING_SEVIYELERI = ["Warning"];

/**
 * Dashboard'un log bölümleri (Spec 0006, R-2 … R-5, R-7; Spec 0011): seçili pencere için tek istekle gelir. Her sayı,
 * aynı pencere ve ilgili filtrelerle log listesini açar (CB-4). Okunamazsa bütün log bölümleri kendi hata durumunu
 * gösterir (R-9). Trend ve oran çubukları (Spec 0007) ile son hatalar ve son uyarılar (Spec 0011, CB-8) aynı yanıttan
 * gelir. Başlıklar ve boş durumlar pencereyi adıyla söyler (CB-2).
 */
export async function LogBolumleri({ sorgu, ad, saatlik, returnTo }: { sorgu: DashboardQuery; ad: PencereAdi; saatlik: boolean; returnTo: string }) {
  const ozet = await getDashboard(sorgu, returnTo);
  const trend = trendMetni(ad, saatlik);

  if (!ozet.ok) {
    return (
      <>
        <Bolum id="trend" baslik={ad.ad} className="md:col-span-2"><BolumHatasi mesaj={trend.hata} /></Bolum>
        <Bolum id="dereceler" baslik="Önem dereceleri" ek={ad.ad}><BolumHatasi mesaj="Log sayıları şu anda okunamıyor." /></Bolum>
        <Bolum id="servisler" baslik="En çok hata üreten servisler" ek={ad.ad}><BolumHatasi mesaj="Servis özeti şu anda okunamıyor." /></Bolum>
        <Bolum id="exceptionlar" baslik="En sık exception'lar" ek={ad.ad}><BolumHatasi mesaj="Exception özeti şu anda okunamıyor." /></Bolum>
        <Bolum id="son-hatalar" baslik="Son hatalar" ek={ad.ad}><BolumHatasi mesaj="Son hatalar şu anda okunamıyor." /></Bolum>
        <Bolum id="son-uyarilar" baslik="Son uyarılar" ek={ad.ad}><BolumHatasi mesaj="Son uyarılar şu anda okunamıyor." /></Bolum>
      </>
    );
  }

  const d = ozet.data;
  const pencere = { from: d.from, to: d.to };
  const toplam = d.levels.reduce((t, l) => t + l.count, 0);
  const enCokHata = Math.max(0, ...d.topErrorServices.map((s) => s.count));
  const enSikTur = Math.max(0, ...d.topExceptionTypes.map((t) => t.count));

  return (
    <>
      <p className="text-sm text-zinc-600 md:col-span-2 dark:text-zinc-400">
        {ad.ad}: <LocalTime iso={d.from} /> – <LocalTime iso={d.to} />. Yüklendi: <LocalTime iso={d.to} />.
      </p>

      <Bolum id="trend" baslik={ad.ad} className="md:col-span-2">
        {toplam === 0 ? <BosBolum>{ad.bulunma} kayıt yok</BosBolum> : <Trend dilimler={d.slices} />}
      </Bolum>

      <Bolum id="dereceler" baslik="Önem dereceleri" ek={ad.ad}>
        {toplam === 0 ? (
          <BosBolum>{ad.bulunma} kayıt yok</BosBolum>
        ) : (
          <div className="flex flex-wrap items-center gap-6">
            <DereceHalkasi dereceler={d.levels} />
            <ul className="min-w-40 flex-1 space-y-2">
              {d.levels.map((l) => (
                <li key={l.level} className="flex items-center gap-2.5">
                  <span aria-hidden="true" className={`inline-block size-2.5 shrink-0 rounded-sm ${DERECE_RENGI[l.level]}`} />
                  <Link href={listHref({ levels: [l.level], ...pencere })} className={`flex flex-1 items-baseline justify-between gap-3 ${link}`}>
                    <span className="text-sm font-medium">{l.level}</span>{" "}
                    <span className="text-lg font-semibold tabular-nums">{sayi.format(l.count)}</span>
                    <span className="sr-only"> kayıt, log listesinde aç</span>
                  </Link>
                  <span data-oran={yuzde(l.count, toplam)} className="w-12 text-right text-sm tabular-nums text-zinc-500 dark:text-zinc-400">
                    %{yuzde(l.count, toplam)}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Bolum>

      <Bolum id="servisler" baslik="En çok hata üreten servisler" ek={ad.ad}>
        {d.topErrorServices.length === 0 ? (
          <BosBolum>{ad.bulunma} hata yok</BosBolum>
        ) : (
          <Tablo
            basliklar={["Servis", "Error + Critical"]}
            satirlar={d.topErrorServices.map((s) => ({
              anahtar: s.serviceName,
              hucreler: [
                <span key="s" className="block">
                  {s.serviceName}
                  <OranCubugu oran={yuzde(s.count, enCokHata)} renk={DERECE_RENGI.Error} />
                </span>,
                <Link key="n" href={listHref({ service: s.serviceName, levels: ERROR_SEVIYELERI, ...pencere })} className={link} aria-label={`${s.serviceName}: ${sayi.format(s.count)} hata, log listesinde aç`}>
                  {sayi.format(s.count)}
                </Link>,
              ],
            }))}
          />
        )}
      </Bolum>

      <Bolum id="exceptionlar" baslik="En sık exception'lar" ek={ad.ad}>
        {d.topExceptionTypes.length === 0 ? (
          <BosBolum>{ad.bulunma} exception yok</BosBolum>
        ) : (
          <Tablo
            basliklar={["Exception türü", "Sayı", "Son görülme"]}
            satirlar={d.topExceptionTypes.map((t) => ({
              anahtar: t.exceptionType,
              hucreler: [
                <span key="t" className="block">
                  <span className="break-all font-mono text-sm">{t.exceptionType}</span>
                  <OranCubugu oran={yuzde(t.count, enSikTur)} renk={DERECE_RENGI.Info} />
                </span>,
                <Link key="n" href={listHref({ exceptionType: t.exceptionType, levels: [], ...pencere })} className={link} aria-label={`${t.exceptionType}: ${sayi.format(t.count)} kayıt, log listesinde aç`}>
                  {sayi.format(t.count)}
                </Link>,
                <LocalTime key="z" iso={t.lastSeenAt} />,
              ],
            }))}
          />
        )}
      </Bolum>

      <SonKayitlar id="son-hatalar" baslik="Son hatalar" ad={ad} bos="hata yok" kayitlar={d.recentErrors} liste={listHref({ levels: ERROR_SEVIYELERI, ...pencere })} />
      <SonKayitlar id="son-uyarilar" baslik="Son uyarılar" ad={ad} bos="uyarı yok" kayitlar={d.recentWarnings} liste={listHref({ levels: WARNING_SEVIYELERI, ...pencere })} />
    </>
  );
}

/**
 * Son hatalar / son uyarılar (Spec 0011, CB-8): penceredeki en yeni en fazla 10 kayıt, en yeni üstte. Satır kaydın
 * ayrıntı sayfasını açar (0002 R-5); "Tümünü gör" listeyi aynı pencere ve derece(ler)le açar. Mesaj düz metindir:
 * React metni kaçışlar, HTML yorumlanmaz (0002 R-6).
 */
function SonKayitlar({ id, baslik, ad, bos, kayitlar, liste }: { id: string; baslik: string; ad: PencereAdi; bos: string; kayitlar: LogSummary[]; liste: string }) {
  return (
    <Bolum id={id} baslik={baslik} ek={ad.ad}>
      {kayitlar.length === 0 ? (
        <BosBolum>{ad.bulunma} {bos}</BosBolum>
      ) : (
        <>
          <ol className="divide-y divide-zinc-100 text-sm dark:divide-zinc-900">
            {kayitlar.map((k) => (
              <li key={k.id}>
                <Link href={detailHref(k.id, liste)} className="block py-1.5 hover:bg-zinc-50 dark:hover:bg-zinc-900">
                  <span className="flex flex-wrap items-baseline gap-x-2 text-xs text-zinc-600 dark:text-zinc-400">
                    <LocalTime iso={k.receivedAt} />
                    <span className="inline-flex items-center gap-1 font-medium text-zinc-800 dark:text-zinc-200">
                      <span aria-hidden="true" className={`inline-block size-2 rounded-sm ${DERECE_RENGI[k.level]}`} />
                      {k.level}
                    </span>
                    <span className="break-all">{k.serviceName}</span>
                  </span>
                  <span className={`mt-0.5 block break-words ${link}`}>
                    {k.messagePreview}
                    {k.messageTruncated && "…"}
                  </span>
                </Link>
              </li>
            ))}
          </ol>
          <Link href={liste} className={`mt-2 inline-block text-sm ${link}`}>
            Tümünü gör<span className="sr-only"> ({baslik.toLocaleLowerCase("tr-TR")}, log listesinde)</span>
          </Link>
        </>
      )}
    </Bolum>
  );
}

/** Log bölümleri yüklenirken her biri başlığıyla ve kendi yükleniyor satırıyla görünür; veri yine tek istekle gelir (Spec 0006, States). */
export function LogBolumleriYukleniyor({ ad, saatlik }: { ad: PencereAdi; saatlik: boolean }) {
  return (
    <>
      <Bolum id="trend" baslik={ad.ad} className="md:col-span-2"><BolumYukleniyor metin={trendMetni(ad, saatlik).yukleniyor} /></Bolum>
      <Bolum id="dereceler" baslik="Önem dereceleri" ek={ad.ad}><BolumYukleniyor metin="Önem dereceleri yükleniyor…" /></Bolum>
      <Bolum id="servisler" baslik="En çok hata üreten servisler" ek={ad.ad}><BolumYukleniyor metin="Servis özeti yükleniyor…" /></Bolum>
      <Bolum id="exceptionlar" baslik="En sık exception'lar" ek={ad.ad}><BolumYukleniyor metin="Exception özeti yükleniyor…" /></Bolum>
      <Bolum id="son-hatalar" baslik="Son hatalar" ek={ad.ad}><BolumYukleniyor metin="Son hatalar yükleniyor…" /></Bolum>
      <Bolum id="son-uyarilar" baslik="Son uyarılar" ek={ad.ad}><BolumYukleniyor metin="Son uyarılar yükleniyor…" /></Bolum>
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
