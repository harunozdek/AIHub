import { randomBytes, randomUUID } from "node:crypto";
import { expect, type Page } from "@playwright/test";
import { sealData } from "iron-session";
import { Pool } from "pg";
import { E2E_KULLANICI, E2E_SIFRE } from "./kimlik";

/**
 * e2e verisi ve ortak adımlar. Kayıtlar kuyruğu atlayıp doğrudan depoya yazılır: alınma zamanı testin
 * kontrolündedir. SQL, container'ın açık portuna `pg` istemcisiyle gider.
 * Her test kendi benzersiz servis adıyla çalışır.
 */
export function appUrl(path = "/"): string {
  return new URL(path, process.env.E2E_APP_URL).href;
}

export function yeniServisAdi(): string {
  return `e2e-${randomUUID().slice(0, 12)}`;
}

/** Sorgu ve bağlantı üst sınırı: takılan bir çağrı testi açık bir hatayla düşürür, koşuyu asılı bırakmaz. */
const SQL_TIMEOUT_MS = 30_000;

/**
 * Testlerin veritabanı bağlantısı: container'ın açık portuna doğrudan bağlanır. SQL eskiden her seferinde
 * `docker exec psql` ile koşardı; Docker Desktop'ta bu çağrıların bir kısmı onlarca saniye takılıyor ve
 * rastgele bir testi düşürüyordu (R-03). Havuz worker başına bir kez kurulur; boştayken süreç kapanabilir.
 */
let havuz: Pool | undefined;

function baglanti(): Pool {
  if (!havuz) {
    havuz = new Pool({
      connectionString: process.env.E2E_PG_URL,
      max: 4,
      allowExitOnIdle: true,
      connectionTimeoutMillis: SQL_TIMEOUT_MS,
      query_timeout: SQL_TIMEOUT_MS,
    });
    // Boştaki bağlantının kopması (ör. @ariza PostgreSQL'i durdurur) worker'ı düşürmez; sorgular hatayla döner.
    havuz.on("error", () => {});
  }
  return havuz;
}

/** Docker CLI çağrılarının üst sınırı (ör. @ariza'da container'ı durdurmak; R-03). */
export const DOCKER_CLI_TIMEOUT_MS = 30_000;

/** Sonucu psql `-tA` biçiminde döndürür: satırlar alt alta, sütunlar `|` ile ayrılmış. */
export async function sql(statement: string): Promise<string> {
  const { rows } = await baglanti().query({ text: statement, rowMode: "array" });
  return rows.map((row: unknown[]) => row.join("|")).join("\n");
}

/** Arka planda koşan SQL (ör. tabloyu birkaç saniye kilitlemek); bittiğinde çözülür. Birden çok deyim içerebilir. */
export async function sqlInBackground(statement: string): Promise<void> {
  await baglanti().query(statement);
}

/** Metni SQL'e değiştirmeden taşır: içerik ne olursa olsun kapanmayan, rastgele etiketli dolar tırnağı. */
function literal(value: string | null | undefined): string {
  if (value === null || value === undefined) return "NULL";
  const tag = `$q${randomBytes(6).toString("hex")}$`;
  return `${tag}${value}${tag}`;
}

export type Kayit = {
  servis: string;
  alinma: Date;
  level?: string;
  message?: string;
  exceptionType?: string | null;
  stackTrace?: string | null;
  metadata?: unknown;
  kaynakZamani?: Date | null;
};

export async function kayitEkle(k: Kayit): Promise<string> {
  return sql(
    "INSERT INTO log_entries (id, service_name, level, message, exception_type, stack_trace, metadata, occurred_at, received_at) VALUES (" +
      [
        "gen_random_uuid()",
        literal(k.servis),
        literal(k.level ?? "Error"),
        literal(k.message ?? "m"),
        literal(k.exceptionType),
        literal(k.stackTrace),
        k.metadata === undefined ? "NULL" : `${literal(JSON.stringify(k.metadata))}::jsonb`,
        k.kaynakZamani ? `${literal(k.kaynakZamani.toISOString())}::timestamptz` : "NULL",
        `${literal(k.alinma.toISOString())}::timestamptz`,
      ].join(", ") +
      ") RETURNING id",
  );
}

/** `adet` kaydı dakikada bir, en yenisi `enYeni` olacak şekilde yükler. */
export async function kayitlarEkle(servis: string, adet: number, enYeni: Date): Promise<void> {
  await sql(
    `INSERT INTO log_entries (id, service_name, level, message, received_at)
     SELECT gen_random_uuid(), ${literal(servis)}, 'Error', 'kayıt ' || i, ${literal(enYeni.toISOString())}::timestamptz - i * interval '1 minute'
     FROM generate_series(0, ${adet - 1}) AS i`,
  );
}

export function saatOnce(saat: number): Date {
  return new Date(Date.now() - saat * 3_600_000);
}

/** Giriş ekranından formla giriş yapar ve panelde istenen adrese döner (Spec 0004, AC-2). */
export async function girisYap(page: Page, hedef = "/logs"): Promise<void> {
  await page.goto(appUrl(hedef));
  await girisFormunuDoldur(page);
  await expect(page).toHaveURL(ayniAdres(hedef));
}

/** Kodlamadan bağımsız adres karşılaştırması: `:` ile `%3A` aynı sorgu değeridir. */
export function ayniAdres(hedef: string): (url: URL) => boolean {
  const beklenen = new URL(appUrl(hedef));
  return (url) =>
    url.origin === beklenen.origin &&
    url.pathname === beklenen.pathname &&
    new URLSearchParams(url.search).toString() === new URLSearchParams(beklenen.search).toString();
}

/** Giriş ekranındaki formu doldurur (Spec 0004); varsayılan hesap açılışta oluşan ilk yöneticidir. */
export async function girisFormunuDoldur(page: Page, kullanici = E2E_KULLANICI, sifre = E2E_SIFRE): Promise<void> {
  await expect(page).toHaveURL((url) => url.pathname === "/giris");
  await page.getByLabel("Kullanıcı adı").fill(kullanici);
  await page.getByLabel("Şifre").fill(sifre);
  await page.getByRole("button", { name: "Giriş yap" }).click();
}

/** Sunucunun göreceği biçimde, süresi dolmuş bir oturum çerezi (Spec 0004, R-10: yenileme yok). */
export async function suresiDolmusOturum(): Promise<string> {
  return sealData(
    { accessToken: "suresi-dolmus", expiresAt: Math.floor(Date.now() / 1000) - 60 },
    { password: process.env.E2E_SESSION_SECRET!, ttl: 0 },
  );
}

/** Kullanıcılar ekranından kullanıcı ekler (Spec 0004, AC-7); oturum açık olmalıdır. */
export async function kullaniciEkleUi(page: Page, ad: string, sifre: string, gorunenAd = `Görünen ${ad}`): Promise<void> {
  await page.goto(appUrl("/kullanicilar"));
  const form = page.getByRole("form", { name: "Kullanıcı ekle" });
  await form.getByLabel("Kullanıcı adı").fill(ad);
  await form.getByLabel("Görünen ad").fill(gorunenAd);
  await form.getByLabel("Başlangıç şifresi").fill(sifre);
  await form.getByRole("button", { name: "Kullanıcı ekle" }).click();
  await expect(form.getByText(`${ad} eklendi.`)).toBeVisible();
}

export async function cikisYap(page: Page): Promise<void> {
  await page.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(page.getByRole("status")).toContainText("Çıkış yaptınız.");
}

/** Benzersiz ve kurala uyan kullanıcı adı (3–64 karakter, ASCII). */
export function yeniKullaniciAdi(): string {
  return `k-${randomUUID().slice(0, 12)}`;
}

// --- Spec 0005: sahte sağlık sunucusu ve izlenen uygulamalar ---------------------------------------

/** Sahte sağlık sunucusunda bir yolun adresi (global-setup başlatır). */
export function saglikAdresi(yol: string): string {
  return new URL(yol, process.env.E2E_SAGLIK_URL).href;
}

/** O yolun yanıt kodunu ve gecikmesini (ms) ayarlar. */
export async function saglikAyarla(yol: string, kod: number, gecikme = 0): Promise<void> {
  const url = new URL("/__ayarla", process.env.E2E_SAGLIK_URL);
  url.search = new URLSearchParams({ yol, kod: String(kod), gecikme: String(gecikme) }).toString();
  expect((await fetch(url)).status).toBe(204);
}

/** Testler izlenen uygulamaları paylaşmaz: Down kalan bir uygulamanın uyarısı başka testin ekranına düşmesin. */
export async function izlemeleriTemizle(): Promise<void> {
  await sql("DELETE FROM monitored_apps");
}

/** "İzlenen uygulamalar" ekranından uygulama ekler; oturum açık olmalıdır. */
export async function uygulamaEkleUi(page: Page, ad: string, adres: string): Promise<void> {
  await page.goto(appUrl("/izlenen-uygulamalar"));
  const form = page.getByRole("form", { name: "Uygulama ekle" });
  await form.getByLabel("ServiceName").fill(ad);
  await form.getByLabel("Sağlık adresi").fill(adres);
  await form.getByRole("button", { name: "Uygulama ekle" }).click();
  await expect(form.getByText(`${ad} izlemeye alındı.`)).toBeVisible();
}
