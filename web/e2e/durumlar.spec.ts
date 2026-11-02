import { randomUUID } from "node:crypto";
import { execFileSync } from "node:child_process";
import { expect, test } from "@playwright/test";
import { appUrl, DOCKER_CLI_TIMEOUT_MS, girisYap, kayitEkle, saatOnce, sql, sqlInBackground, yeniServisAdi } from "./seed";

// Spec 0002 — AC-9: liste ve ayrıntı ekranlarının durumları.

test("@bos-depo hiç kayıt yokken 'Henüz log kaydı yok' görünür", async ({ page }) => {
  await girisYap(page, "/logs");

  await expect(page.getByText("Henüz log kaydı yok")).toBeVisible();
  await expect(page.getByRole("table")).toHaveCount(0);
});

// Review 1, F-05: kayıt var ama hepsi son 24 saatten eski. "Filtreleri temizle" burada çıkmaz bir yol olurdu.
test("@bos-depo filtre yokken son 24 saatte kayıt yoksa daha eski aralık seçme yolu gösterilir", async ({ page }) => {
  kayitEkle({ servis: yeniServisAdi(), alinma: saatOnce(48), message: "eski-kayit" });
  await girisYap(page, "/logs");

  await expect(page.getByText("Son 24 saatte kayıt yok")).toBeVisible();
  await expect(page.getByText("Bu filtrelere uyan kayıt yok")).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Filtreleri temizle" })).toHaveCount(0);
  await expect(page.getByText("Daha eski kayıtlar için Başlangıç ve Bitiş alanlarından bir aralık seçin.")).toBeVisible();
});

test("filtreye uyan kayıt yoksa mesaj ve 'filtreleri temizle' görünür", async ({ page }) => {
  const servis = yeniServisAdi();
  kayitEkle({ servis, alinma: saatOnce(1), level: "Error" });
  await girisYap(page, `/logs?service=${servis}&level=Critical`);

  await expect(page.getByText("Bu filtrelere uyan kayıt yok")).toBeVisible();
  await page.getByRole("link", { name: "Filtreleri temizle" }).click();
  await expect(page).toHaveURL(appUrl("/logs"));
});

test("var olmayan ve bozuk kimlik 'kayıt bulunamadı' gösterir", async ({ page }) => {
  await girisYap(page, `/logs/${randomUUID()}`);
  await expect(page.getByRole("heading", { name: "Kayıt bulunamadı" })).toBeVisible();

  await page.goto(appUrl("/logs/bozuk-kimlik"));
  await expect(page.getByRole("heading", { name: "Kayıt bulunamadı" })).toBeVisible();
});

test("veri gelene kadar liste ve ayrıntıda yükleniyor durumu görünür", async ({ page }) => {
  const servis = yeniServisAdi();
  const id = kayitEkle({ servis, alinma: saatOnce(1), message: `kayit-${servis}` });
  await girisYap(page, `/logs?service=${servis}`);

  for (const [adres, yukleniyor, sonra] of [
    [`/logs?service=${servis}&level=Error`, "Loglar yükleniyor…", page.getByRole("table")],
    [`/logs/${id}`, "Kayıt yükleniyor…", page.getByRole("heading", { name: "Log ayrıntısı" })],
  ] as const) {
    // Tablo birkaç saniye kilitlenir: sorgu bekler, sayfa bu sürede yükleniyor durumunu gösterir.
    const kilit = sqlInBackground("BEGIN; LOCK TABLE log_entries IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(3); COMMIT;");
    await expect
      .poll(() => sql("SELECT count(*) FROM pg_locks l JOIN pg_class c ON c.oid = l.relation WHERE c.relname = 'log_entries' AND l.mode = 'AccessExclusiveLock' AND l.granted"))
      .toBe("1");

    await page.goto(appUrl(adres), { waitUntil: "commit" });
    await expect(page.getByText(yukleniyor)).toBeVisible();
    await kilit;
    await expect(sonra).toBeVisible();
  }
});

test("@ariza okuma arızasında anlaşılır mesaj ve 'yeniden dene' görünür, iç ayrıntı görünmez", async ({ page }) => {
  const servis = yeniServisAdi();
  const id = kayitEkle({ servis, alinma: saatOnce(1) });
  await girisYap(page, `/logs?service=${servis}`);

  // Depo durur: bu proje en son koşar, sonraki hiçbir test depoya ihtiyaç duymaz.
  execFileSync("docker", ["stop", process.env.E2E_PG_CONTAINER!], { timeout: DOCKER_CLI_TIMEOUT_MS });

  for (const adres of [`/logs?service=${servis}`, `/logs/${id}`]) {
    await page.goto(appUrl(adres));

    const hata = page.getByRole("alert").filter({ hasText: "Loglar şu anda okunamıyor." });
    await expect(hata).toBeVisible();
    const metin = await page.locator("body").innerText();
    for (const ic of ["aihub-e2e", "Host=", "Npgsql", "Exception", "   at ", "5432", "localhost"]) {
      expect(metin).not.toContain(ic);
    }

    await hata.getByRole("button", { name: "Yeniden dene" }).click();
    await expect(hata).toBeVisible();
  }
});
