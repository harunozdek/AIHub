import { execFile, execFileSync } from "node:child_process";
import { randomBytes, randomUUID } from "node:crypto";
import { expect, type Page } from "@playwright/test";
import { sealData } from "iron-session";

/**
 * e2e verisi ve ortak adımlar. Kayıtlar kuyruğu atlayıp doğrudan depoya yazılır: alınma zamanı testin
 * kontrolündedir. SQL, container içindeki psql ile koşar; ek bir veritabanı istemcisi gerekmez.
 * Her test kendi benzersiz servis adıyla çalışır.
 */
export function appUrl(path = "/"): string {
  return new URL(path, process.env.E2E_APP_URL).href;
}

export function yeniServisAdi(): string {
  return `e2e-${randomUUID().slice(0, 12)}`;
}

function psqlArgs(sql: string): string[] {
  return ["exec", process.env.E2E_PG_CONTAINER!, "psql", "-U", "aihub", "-d", "aihub", "-tAq", "-v", "ON_ERROR_STOP=1", "-c", sql];
}

export function sql(statement: string): string {
  return execFileSync("docker", psqlArgs(statement), { encoding: "utf8" }).trim();
}

/** Arka planda koşan SQL (ör. tabloyu birkaç saniye kilitlemek); bittiğinde çözülür. */
export function sqlInBackground(statement: string): Promise<void> {
  return new Promise((resolve, reject) => execFile("docker", psqlArgs(statement), (error) => (error ? reject(error) : resolve())));
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

export function kayitEkle(k: Kayit): string {
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
export function kayitlarEkle(servis: string, adet: number, enYeni: Date): void {
  sql(
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
