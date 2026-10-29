import { expect, test, type Page } from "@playwright/test";
import { appUrl, girisYap, kayitEkle, kayitlarEkle, saatOnce, yeniServisAdi } from "./seed";

// Spec 0002 — AC-3, AC-4, AC-5, AC-6: liste, filtreler, sayfalama ve geçersiz filtre.

const satirlar = (page: Page) => page.locator("tbody tr");

test("AC-3 liste son 24 saati en yeni önce gösterir, satırda istenen alanlar ve kısaltma bulunur", async ({ page }) => {
  const servis = yeniServisAdi();
  kayitEkle({ servis, alinma: saatOnce(25), message: "eski-kayit" });
  kayitEkle({ servis, alinma: saatOnce(23), level: "Warning", message: "a-kaydi" });
  kayitEkle({ servis, alinma: saatOnce(2), message: "b-kaydi\nikinci satır", exceptionType: "System.TimeoutException" });
  kayitEkle({ servis, alinma: saatOnce(0.02), level: "Critical", message: "x".repeat(500) });

  await girisYap(page, `/logs?service=${servis}`);

  await expect(satirlar(page)).toHaveCount(3);
  await expect(page.locator("tbody")).not.toContainText("eski-kayit");
  const [c, b, a] = [satirlar(page).nth(0), satirlar(page).nth(1), satirlar(page).nth(2)];
  await expect(c).toContainText("x".repeat(200) + " … (kısaltıldı)");
  await expect(c).not.toContainText("x".repeat(201));
  await expect(c).toContainText("Critical");
  await expect(b).toContainText(servis);
  await expect(b).toContainText("Error");
  await expect(b).toContainText("b-kaydi … (kısaltıldı)");
  await expect(b).not.toContainText("ikinci satır");
  await expect(b).toContainText("System.TimeoutException");
  await expect(a).toContainText("Warning");
  await expect(a).toContainText("a-kaydi");
  await expect(b.locator("time")).toHaveText(/GMT\+3/);
});

test("AC-4 filtreler birlikte uygulanır ve filtreli adres yeni sekmede aynı sonucu verir", async ({ page, context }) => {
  const servis = yeniServisAdi();
  const baska = yeniServisAdi();
  kayitEkle({ servis, alinma: saatOnce(3), level: "Error", message: "uyan-1" });
  kayitEkle({ servis, alinma: saatOnce(2), level: "Critical", message: "uyan-2" });
  kayitEkle({ servis, alinma: saatOnce(2), level: "Info", message: "seviye-disi" });
  kayitEkle({ servis, alinma: saatOnce(10), level: "Error", message: "aralik-disi" });
  kayitEkle({ servis: baska, alinma: saatOnce(2), level: "Error", message: "servis-disi" });
  await girisYap(page, "/logs");

  await expect(page.getByLabel("Servis").locator("option", { hasText: servis })).toHaveCount(1);
  await page.getByLabel("Servis").selectOption(servis);
  await page.getByRole("checkbox", { name: "Error" }).check();
  await page.getByRole("checkbox", { name: "Critical" }).check();
  await page.getByLabel("Başlangıç (yerel saat)").fill(yerelGirdi(saatOnce(5)));
  await page.getByLabel("Bitiş (yerel saat)").fill(yerelGirdi(new Date(Date.now() + 60_000)));
  await page.getByRole("button", { name: "Uygula" }).click();

  await expect(page).toHaveURL(/level=Error&level=Critical/);
  await expect(satirlar(page)).toHaveCount(2);
  await expect(satirlar(page).nth(0)).toContainText("uyan-2");
  await expect(satirlar(page).nth(1)).toContainText("uyan-1");

  const yeniSekme = await context.newPage();
  await yeniSekme.goto(page.url());
  await expect(satirlar(yeniSekme)).toHaveCount(2);
  const kayitAdresleri = (p: Page) => p.locator("tbody tr a").evaluateAll((as) => as.map((a) => new URL((a as HTMLAnchorElement).href).pathname));
  expect(await kayitAdresleri(yeniSekme)).toEqual(await kayitAdresleri(page));
});

test("AC-5 sayfalar arasında ileri ve geri gidilir, kayıt tekrarlanmaz", async ({ page }) => {
  const servis = yeniServisAdi();
  kayitlarEkle(servis, 55, saatOnce(1));
  await girisYap(page, `/logs?service=${servis}`);

  await expect(satirlar(page)).toHaveCount(50);
  await expect(satirlar(page).first()).toContainText("kayıt 0");
  await expect(satirlar(page).last()).toContainText("kayıt 49");
  await page.getByRole("link", { name: "Sonraki sayfa →" }).click();
  await expect(satirlar(page)).toHaveCount(5);
  await expect(satirlar(page).first()).toContainText("kayıt 50");

  await page.getByRole("link", { name: "← Önceki sayfa" }).click();
  await expect(satirlar(page)).toHaveCount(50);
  await expect(satirlar(page).first()).toContainText("kayıt 0");
  await expect(satirlar(page).last()).toContainText("kayıt 49");
});

test("AC-6 geçersiz aralık ve tanımsız LogLevel ilgili alanın yanında mesajla reddedilir", async ({ page }) => {
  await girisYap(page, "/logs?from=2026-10-27T10:00:00.000Z&to=2026-10-27T09:00:00.000Z");
  await expect(page.getByLabel("Bitiş (yerel saat)")).toHaveAccessibleDescription("Bitiş zamanı başlangıç zamanından önce olamaz.");
  await expect(page.getByRole("table")).toHaveCount(0);

  await page.goto(appUrl("/logs?from=2026-09-01T00:00:00.000Z&to=2026-10-27T00:00:00.000Z"));
  await expect(page.getByLabel("Başlangıç (yerel saat)")).toHaveAccessibleDescription("Zaman aralığı en fazla 31 gün olabilir.");

  await page.goto(appUrl("/logs?level=Debug"));
  await expect(page.getByRole("group", { name: "Seviye" })).toHaveAccessibleDescription(/'Debug' tanımlı bir LogLevel değil/);

  await page.goto(appUrl("/logs?from=2026-09-26T00:00:00.000Z&to=2026-10-27T00:00:00.000Z"));
  await expect(page.getByText(/en fazla 31 gün/)).toHaveCount(0);
});

/** `datetime-local` değeri: testin saat dilimi (Europe/Istanbul) ile tarayıcınınki aynıdır. */
function yerelGirdi(tarih: Date): string {
  const parcalar = new Intl.DateTimeFormat("sv-SE", {
    timeZone: "Europe/Istanbul",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(tarih);
  return parcalar.replace(" ", "T");
}
