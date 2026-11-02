import { expect, test, type Page } from "@playwright/test";
import { appUrl, cikisYap, girisYap, izlemeleriTemizle, saglikAdresi, saglikAyarla, uygulamaEkleUi, yeniServisAdi } from "./seed";

// Spec 0005 — AC-8, AC-10: her ekrandaki Down uyarısı. Denetim aralığı e2e'de 1 sn (global-setup); panelin
// 15 sn'lik yoklaması tarayıcı saatiyle ileri sarılır (page.clock): "15 sn içinde, sayfa yenilenmeden".

test.afterEach(izlemeleriTemizle);

const YOKLAMA_MS = 15_000;

function uyari(page: Page) {
  return page.getByRole("alert").filter({ hasText: "Down" });
}

/** Sağlık sunucusunu 503'e çevirip uygulamayı ekler ve backend'de Down olmasını bekler (≈3 sn). */
async function downUygulama(page: Page): Promise<string> {
  const ad = yeniServisAdi();
  await saglikAyarla(`/${ad}`, 503);
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await expect
    .poll(async () => {
      await page.reload();
      return page.getByRole("row").filter({ hasText: ad }).getByRole("cell").nth(2).textContent();
    }, { timeout: 20_000 })
    .toBe("Down");
  return ad;
}

/** Sayfa yenilenmeden kalındığını kanıtlamak için pencereye bir işaret bırakır. */
async function isaretKoy(page: Page) {
  await page.evaluate(() => ((window as unknown as { __isaret: number }).__isaret = 1));
}

async function isaretDuruyor(page: Page) {
  expect(await page.evaluate(() => (window as unknown as { __isaret?: number }).__isaret)).toBe(1);
}

/** Bir sonraki yoklamayı tetikler ve yanıtını bekler. */
async function yoklamayiIlerlet(page: Page) {
  const yanit = page.waitForResponse((r) => r.url().endsWith("/durum/down"));
  await page.clock.runFor(YOKLAMA_MS);
  await yanit;
}

test("AC-8 Down uygulama varken her ekranda uyarı görünür, Ayakta olunca sayfa yenilenmeden 15 sn içinde kalkar", async ({ page }) => {
  await page.clock.install();
  await girisYap(page, "/izlenen-uygulamalar");
  const ad = await downUygulama(page);

  for (const adres of ["/logs", "/kullanicilar", "/izlenen-uygulamalar", "/sifre-degistir"]) {
    await page.goto(appUrl(adres));
    await expect(uyari(page)).toContainText(ad);
    await expect(uyari(page)).toContainText("zamanından beri");
  }
  await expect(uyari(page).getByRole("link", { name: ad })).toHaveAttribute("href", "/izlenen-uygulamalar");

  await page.goto(appUrl("/logs"));
  await expect(uyari(page)).toBeVisible();
  await isaretKoy(page);
  await saglikAyarla(`/${ad}`, 200);
  // Backend tek başarılı denetimle Ayakta yapar (≈1 sn); panel sonraki yoklamada görür.
  await expect
    .poll(async () => {
      await yoklamayiIlerlet(page);
      return uyari(page).count();
    }, { timeout: 20_000, intervals: [500] })
    .toBe(0);
  await isaretDuruyor(page);
});

test("AC-8 izlemeden çıkarılan Down uygulama başka bir ekrandaki uyarıdan sayfa yenilenmeden düşer", async ({ page, context }) => {
  await page.clock.install();
  await girisYap(page, "/izlenen-uygulamalar");
  const ad = await downUygulama(page);
  const diger = await context.newPage();
  await diger.clock.install();
  await diger.goto(appUrl("/logs"));
  await expect(uyari(diger)).toContainText(ad);
  await isaretKoy(diger);

  const satir = page.getByRole("row").filter({ hasText: ad });
  await satir.getByRole("button", { name: `${ad}: İzlemeden çıkar` }).click();
  await satir.getByRole("button", { name: "Evet, çıkar" }).click();
  await expect(satir).toHaveCount(0);

  await yoklamayiIlerlet(diger);
  await expect(uyari(diger)).toHaveCount(0);
  await isaretDuruyor(diger);
});

test("AC-8 Down uygulama yokken uyarı görünmez; giriş ekranında Down uygulama varken de görünmez", async ({ page }) => {
  const ilkYoklama = page.waitForResponse((r) => r.url().endsWith("/durum/down"));
  await girisYap(page, "/logs");
  await ilkYoklama;
  await expect(uyari(page)).toHaveCount(0);

  await page.goto(appUrl("/izlenen-uygulamalar"));
  await downUygulama(page);
  await page.goto(appUrl("/logs"));
  await expect(uyari(page)).toBeVisible();

  await cikisYap(page);
  await expect(page).toHaveURL((url) => url.pathname === "/giris");
  await expect(uyari(page)).toHaveCount(0);
});

test("AC-10 durum okunamazsa uyarı son bilinen durumu 'durum güncellenemedi' notuyla gösterir", async ({ page }) => {
  await page.clock.install();
  await girisYap(page, "/izlenen-uygulamalar");
  const ad = await downUygulama(page);
  await page.goto(appUrl("/logs"));
  await expect(uyari(page)).toContainText(ad);

  await page.route("**/durum/down", (route) => route.abort());
  await page.clock.runFor(YOKLAMA_MS);

  await expect(uyari(page)).toContainText("Durum güncellenemedi");
  await expect(uyari(page)).toContainText(ad);
});
