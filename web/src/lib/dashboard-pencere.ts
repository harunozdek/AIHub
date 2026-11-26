/**
 * Dashboard'un zaman penceresi seçimi (Spec 0011). Seçim sayfa adresinde durur (CB-1, CB-7): hazır pencere
 * `?pencere=7g`, serbest aralık `?pencere=aralik&baslangic=2026-11-01&bitis=2026-11-15` (adreste yalnızca tarihler,
 * S-2). Tanımsız ya da geçersiz seçim "Son 24 saat"tir. Uzunluklar ve dilimler backend'dedir (DashboardWindow);
 * burada yalnızca adres, ad ve yerel gün sınırları vardır.
 */

export type HazirPencere = {
  /** Adresteki değer. */
  deger: string;
  /** API'nin `window` değeri. */
  api: string;
  /** "Son 1 hafta" */
  ad: string;
  /** Boş durum ve özet için bulunma eki: "Son 1 haftada". */
  bulunma: string;
};

export const HAZIR_PENCERELER: readonly HazirPencere[] = [
  { deger: "24s", api: "24h", ad: "Son 24 saat", bulunma: "Son 24 saatte" },
  { deger: "48s", api: "48h", ad: "Son 48 saat", bulunma: "Son 48 saatte" },
  { deger: "7g", api: "7d", ad: "Son 1 hafta", bulunma: "Son 1 haftada" },
  { deger: "14g", api: "14d", ad: "Son 2 hafta", bulunma: "Son 2 haftada" },
  { deger: "30g", api: "30d", ad: "Son 1 ay", bulunma: "Son 1 ayda" },
  { deger: "90g", api: "90d", ad: "Son 3 ay", bulunma: "Son 3 ayda" },
  { deger: "180g", api: "180d", ad: "Son 6 ay", bulunma: "Son 6 ayda" },
  { deger: "365g", api: "365d", ad: "Son 1 yıl", bulunma: "Son 1 yılda" },
];

export const ARALIK = "aralik";
export const VARSAYILAN = HAZIR_PENCERELER[0];

/** Serbest aralığın en uzun hali, gün olarak (başlangıç ve bitiş günü dahil; CB-7). */
export const EN_UZUN_GUN = 365;

export type Secim =
  | { tur: "hazir"; pencere: HazirPencere }
  /** Tarihler `YYYY-MM-DD`, kullanıcının yerel günü. */
  | { tur: "aralik"; baslangic: string; bitis: string };

type RawParams = Record<string, string | string[] | undefined>;

function tek(value: string | string[] | undefined): string | undefined {
  const v = Array.isArray(value) ? value[0] : value;
  return v || undefined;
}

const TARIH = /^\d{4}-\d{2}-\d{2}$/;

/** `YYYY-MM-DD` takvimde gerçekten var olan bir gün mü (ör. 2026-02-30 değil). */
export function gecerliTarih(value: string): boolean {
  if (!TARIH.test(value)) return false;
  const [y, m, d] = value.split("-").map(Number);
  const t = new Date(Date.UTC(y, m - 1, d));
  return t.getUTCFullYear() === y && t.getUTCMonth() === m - 1 && t.getUTCDate() === d;
}

/** İki gün arasındaki gün farkı (bitis − baslangic). */
export function gunFarki(baslangic: string, bitis: string): number {
  const [a, b] = [baslangic, bitis].map((v) => {
    const [y, m, d] = v.split("-").map(Number);
    return Date.UTC(y, m - 1, d);
  });
  return Math.round((b - a) / 86_400_000);
}

/**
 * Serbest aralığın doğrulaması (CB-7). `bugun` kullanıcının yerel günüdür. Geçerliyse null, değilse alanın yanında
 * gösterilecek mesaj ve alan.
 */
export function araligiDogrula(baslangic: string, bitis: string, bugun: string): { alan: "baslangic" | "bitis"; mesaj: string } | null {
  if (!gecerliTarih(baslangic)) return { alan: "baslangic", mesaj: "Başlangıç tarihini seçin." };
  if (!gecerliTarih(bitis)) return { alan: "bitis", mesaj: "Bitiş tarihini seçin." };
  if (gunFarki(baslangic, bugun) < 0) return { alan: "baslangic", mesaj: "Başlangıç tarihi gelecekte olamaz." };
  if (gunFarki(baslangic, bitis) < 0) return { alan: "bitis", mesaj: "Bitiş tarihi başlangıç tarihinden önce olamaz." };
  if (gunFarki(baslangic, bitis) + 1 > EN_UZUN_GUN) return { alan: "bitis", mesaj: `Tarih aralığı en fazla ${EN_UZUN_GUN} gün olabilir.` };
  return null;
}

/** Adresteki seçim; tanımsız ya da geçersizse "Son 24 saat" (CB-1, CB-7). */
export function secimiOku(params: RawParams, bugun: string): Secim {
  const deger = tek(params.pencere);
  if (deger === ARALIK) {
    const baslangic = tek(params.baslangic) ?? "";
    const bitis = tek(params.bitis) ?? "";
    if (araligiDogrula(baslangic, bitis, bugun) === null) {
      return { tur: "aralik", baslangic, bitis };
    }
  }
  return { tur: "hazir", pencere: HAZIR_PENCERELER.find((p) => p.deger === deger) ?? VARSAYILAN };
}

export function secimAdresi(secim: Secim): string {
  const q = new URLSearchParams();
  if (secim.tur === "hazir") {
    q.set("pencere", secim.pencere.deger);
  } else {
    q.set("pencere", ARALIK);
    q.set("baslangic", secim.baslangic);
    q.set("bitis", secim.bitis);
  }
  return `/dashboard?${q}`;
}

/** Suspense ve yeniden yükleme anahtarı: aynı seçim aynı anahtar. */
export function secimAnahtari(secim: Secim): string {
  return secim.tur === "hazir" ? secim.pencere.deger : `${secim.baslangic}_${secim.bitis}`;
}

const AYLAR = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

function tarihAdi(value: string, yilla: boolean): string {
  const [y, m, d] = value.split("-").map(Number);
  return `${d} ${AYLAR[m - 1]}${yilla ? ` ${y}` : ""}`;
}

/** Seçimin adı ve bulunma eki: "Son 1 hafta" / "Son 1 haftada"; aralıkta "1 Kasım – 15 Kasım 2026 arasında". */
export function secimAdi(secim: Secim): { ad: string; bulunma: string } {
  if (secim.tur === "hazir") return { ad: secim.pencere.ad, bulunma: secim.pencere.bulunma };
  const ayniYil = secim.baslangic.slice(0, 4) === secim.bitis.slice(0, 4);
  const ad =
    secim.baslangic === secim.bitis
      ? tarihAdi(secim.baslangic, true)
      : `${tarihAdi(secim.baslangic, !ayniYil)} – ${tarihAdi(secim.bitis, true)}`;
  return { ad, bulunma: secim.baslangic === secim.bitis ? `${ad} tarihinde` : `${ad} arasında` };
}

/** Bir saat diliminde verilen anın UTC'den farkı (ms). */
function dilimFarki(an: number, timeZone: string): number {
  const p = Object.fromEntries(
    new Intl.DateTimeFormat("en-US", {
      timeZone,
      hourCycle: "h23",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    })
      .formatToParts(new Date(an))
      .map((x) => [x.type, x.value]),
  );
  return Date.UTC(+p.year, +p.month - 1, +p.day, +p.hour, +p.minute, +p.second) - (an - (an % 1000));
}

/** Verilen yerel günün 00:00'ının UTC anı (yaz saati geçişinde de doğru). */
export function gunBasi(tarih: string, timeZone: string): Date {
  const [y, m, d] = tarih.split("-").map(Number);
  const tahmin = Date.UTC(y, m - 1, d);
  let an = tahmin - dilimFarki(tahmin, timeZone);
  an = tahmin - dilimFarki(an, timeZone);
  return new Date(an);
}

function ertesiGun(tarih: string): string {
  const [y, m, d] = tarih.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d + 1)).toISOString().slice(0, 10);
}

/**
 * Serbest aralığın API sınırları (CB-7): başlangıç gününün 00:00'ı ile bitiş gününün sonu (ertesi gün 00:00, hariç).
 * API [from, to] kapsar; ertesi günün ilk anı dışarıda kalsın diye bitiş 1 ms öncedir (alınma zamanı milisaniye altı
 * hane taşısa da API'nin 1 µs'lik sınırının içinde kalır).
 */
export function aralikSinirlari(baslangic: string, bitis: string, timeZone: string): { from: string; to: string } {
  const from = gunBasi(baslangic, timeZone);
  const sonraki = gunBasi(ertesiGun(bitis), timeZone);
  return { from: from.toISOString(), to: new Date(sonraki.getTime() - 1).toISOString() };
}

/** Verilen saat dilimindeki bugün (`YYYY-MM-DD`). */
export function bugunDilimde(timeZone: string, an = new Date()): string {
  return new Intl.DateTimeFormat("sv-SE", { timeZone, year: "numeric", month: "2-digit", day: "2-digit" }).format(an);
}

/** Günü en önde olan saat dilimi (UTC+14, yaz saati yok): dilim bilinmiyorken "gelecekte mi" sorusunun hoşgörülü yanıtı. */
export const EN_ILERI_SAAT_DILIMI = "Etc/GMT-14";

/** Saat dilimi çerezinin adı (plan 0011, risk 3). Sır değildir; yalnızca IANA adı taşır. */
export const SAAT_DILIMI_CEREZI = "aihub_tz";

/** Çerezdeki değer geçerli bir IANA saat dilimi adı mı; değilse null. */
export function gecerliSaatDilimi(value: string | undefined): string | null {
  if (!value || value.length > 64 || !/^[A-Za-z][A-Za-z0-9_+\-]*(\/[A-Za-z0-9_+\-]+)*$/.test(value)) return null;
  try {
    new Intl.DateTimeFormat("en-US", { timeZone: value });
    return value;
  } catch {
    return null;
  }
}
