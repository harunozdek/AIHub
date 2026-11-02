import { randomBytes, randomUUID } from "node:crypto";
import { expect, type Page } from "@playwright/test";
import { sealData } from "iron-session";
import { Pool } from "pg";

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

/** Mock kimlik sağlayıcısının etkileşimli formuyla giriş yapar ve panelde istenen adrese döner. */
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

export async function girisFormunuDoldur(page: Page): Promise<void> {
  await expect(page).toHaveURL((url) => url.origin === process.env.E2E_OIDC_ORIGIN);
  await page.locator('input[name="username"]').fill("e2e-kullanici");
  await page.locator('input[type="submit"]').click();
}

/** Sunucunun göreceği biçimde, süresi dolmuş ve yenilenemeyen bir oturum çerezi. */
export async function suresiDolmusOturum(): Promise<string> {
  return sealData(
    { accessToken: "suresi-dolmus", expiresAt: Math.floor(Date.now() / 1000) - 60 },
    { password: process.env.E2E_SESSION_SECRET!, ttl: 0 },
  );
}
