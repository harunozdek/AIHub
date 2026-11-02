import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { appUrl, girisYap, kayitEkle, kayitlarEkle, saatOnce, yeniServisAdi } from "./seed";

// Spec 0002 — AC-10: yalnızca klavyeyle akış, görünür odak, WCAG 2.1 AA.

const wcag21aa = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"];

/** Odak hedefe gelene kadar Tab'a basar; her adımda odaklanan öğenin odak çizgisi görünür olmalıdır. */
async function tabIle(page: Page, hedef: Locator, enFazla = 100) {
  for (let i = 0; i < enFazla; i++) {
    await page.keyboard.press("Tab");
    const odakGorunur = await page.evaluate(() => {
      const el = document.activeElement as HTMLElement | null;
      if (!el || el === document.body) return true;
      return getComputedStyle(el).outlineStyle !== "none" && getComputedStyle(el).outlineWidth !== "0px";
    });
    expect(odakGorunur, `Odak çizgisi görünmüyor: ${await page.evaluate(() => document.activeElement?.outerHTML.slice(0, 120))}`).toBe(true);
    if (await hedef.evaluate((el) => el === document.activeElement)) return;
  }
  throw new Error("Hedefe klavyeyle ulaşılamadı.");
}

/** Tarih alanına klavyeyle yazar: tr-TR düzeninde gün.ay.yıl, Tab, saat:dakika (Europe/Istanbul). */
async function tarihYaz(page: Page, tarih: Date) {
  const p = Object.fromEntries(
    new Intl.DateTimeFormat("en-GB", { timeZone: "Europe/Istanbul", year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" })
      .formatToParts(tarih)
      .map((x) => [x.type, x.value]),
  );
  await page.keyboard.type(`${p.day}${p.month}${p.year}`);
  await page.keyboard.press("Tab");
  await page.keyboard.type(`${p.hour}${p.minute}`);
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`;
}

test("AC-10 filtre, zaman aralığı, sayfalama, satır seçimi ve geri dönüş yalnızca klavyeyle yapılır", async ({ page }) => {
  const servis = yeniServisAdi();
  await kayitlarEkle(servis, 50, saatOnce(1));
  await kayitEkle({ servis, alinma: saatOnce(2), level: "Error", message: `klavye-${servis}` });
  await kayitEkle({ servis, alinma: saatOnce(1), level: "Info", message: `elenecek-${servis}` });
  await girisYap(page, "/logs");

  await tabIle(page, page.getByLabel("Servis"));
  await page.keyboard.type(servis);
  await tabIle(page, page.getByRole("checkbox", { name: "Error" }));
  await page.keyboard.press("Space");
  await tabIle(page, page.getByLabel("Başlangıç (yerel saat)"));
  await expect(page.getByLabel("Başlangıç (yerel saat)")).toHaveValue(await tarihYaz(page, saatOnce(5)));
  await tabIle(page, page.getByLabel("Bitiş (yerel saat)"));
  await expect(page.getByLabel("Bitiş (yerel saat)")).toHaveValue(await tarihYaz(page, new Date(Date.now() + 3_600_000)));
  await tabIle(page, page.getByRole("button", { name: "Uygula" }));
  await page.keyboard.press("Enter");

  await expect(page).toHaveURL(/level=Error/);
  await expect(page).toHaveURL(/from=/);
  await expect(page.locator("tbody tr")).toHaveCount(50);
  await tabIle(page, page.getByRole("link", { name: "Sonraki sayfa →" }));
  await page.keyboard.press("Enter");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await tabIle(page, page.getByRole("link", { name: "← Önceki sayfa" }));
  await page.keyboard.press("Enter");
  await expect(page.locator("tbody tr")).toHaveCount(50);
  await tabIle(page, page.getByRole("link", { name: "Sonraki sayfa →" }));
  await page.keyboard.press("Enter");
  await expect(page.locator("tbody tr")).toHaveCount(1);

  const satirBaglantisi = page.locator("tbody tr").first().getByRole("link");
  await tabIle(page, satirBaglantisi);
  await page.keyboard.press("Enter");
  await expect(page.getByRole("heading", { name: "Log ayrıntısı" })).toBeVisible();

  const geri = page.getByRole("link", { name: "← Listeye dön" });
  await tabIle(page, geri);
  await page.keyboard.press("Enter");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await expect(page.getByLabel("Servis")).toHaveValue(servis);
});

test("AC-10 liste ve ayrıntıda WCAG 2.1 AA ihlali yok, LogLevel metinle belirtilir", async ({ page }) => {
  const servis = yeniServisAdi();
  const id = await kayitEkle({ servis, alinma: saatOnce(1), level: "Critical", message: "erişilebilirlik", metadata: { a: 1 } });
  for (const level of ["Info", "Warning", "Error"]) {
    await kayitEkle({ servis, alinma: saatOnce(2), level, message: level });
  }

  for (const adres of [`/logs?service=${servis}`, `/logs/${id}`, "/logs?level=Debug"]) {
    await (adres === `/logs?service=${servis}` ? girisYap(page, adres) : page.goto(appUrl(adres)));
    await expect(page.locator("main")).not.toBeEmpty();

    const sonuc = await new AxeBuilder({ page }).withTags(wcag21aa).analyze();
    expect(sonuc.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`)).toEqual([]);
  }

  await page.goto(appUrl(`/logs?service=${servis}`));
  for (const level of ["Info", "Warning", "Error", "Critical"]) {
    await expect(page.locator("tbody")).toContainText(level);
  }
});
