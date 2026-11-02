import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { E2E_KULLANICI, E2E_SIFRE } from "./kimlik";
import { appUrl, cikisYap, girisYap, izlemeleriTemizle, kayitEkle, kayitlarEkle, kullaniciEkleUi, saatOnce, saglikAdresi, saglikAyarla, uygulamaEkleUi, yeniKullaniciAdi, yeniServisAdi } from "./seed";

// Spec 0002 — AC-10, Spec 0004 — AC-12 ve Spec 0005 — AC-12: yalnızca klavyeyle akış, görünür odak, WCAG 2.1 AA.

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

async function axeIhlalleri(page: Page): Promise<string[]> {
  const sonuc = await new AxeBuilder({ page }).withTags(wcag21aa).analyze();
  return sonuc.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`);
}

test("AC-12 giriş, kullanıcı ekleme ve şifre değiştirme yalnızca klavyeyle tamamlanır", async ({ page }) => {
  const ad = yeniKullaniciAdi();
  const sifre = "klavye-sifresi-123";

  await page.goto(appUrl("/giris?returnTo=/kullanicilar"));
  await tabIle(page, page.getByLabel("Kullanıcı adı"));
  await page.keyboard.type(E2E_KULLANICI);
  await tabIle(page, page.getByLabel("Şifre"));
  await page.keyboard.type(E2E_SIFRE);
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(appUrl("/kullanicilar"));

  const form = page.getByRole("form", { name: "Kullanıcı ekle" });
  await tabIle(page, form.getByLabel("Kullanıcı adı"));
  await page.keyboard.type(ad);
  await tabIle(page, form.getByLabel("Görünen ad"));
  await page.keyboard.type("Klavye Kişisi");
  await tabIle(page, form.getByLabel("Başlangıç şifresi"));
  await page.keyboard.type(sifre);
  await tabIle(page, form.getByRole("button", { name: "Kullanıcı ekle" }));
  await page.keyboard.press("Enter");
  await expect(form.getByText(`${ad} eklendi.`)).toBeVisible();

  const pasiflestir = page.getByRole("button", { name: `${ad}: Pasifleştir` });
  await tabIle(page, pasiflestir);
  await page.keyboard.press("Enter");
  await tabIle(page, page.getByRole("button", { name: "Vazgeç" }));
  await page.keyboard.press("Enter");
  await expect(pasiflestir).toBeVisible();

  await tabIle(page, page.getByRole("link", { name: "Şifre değiştir" }));
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(appUrl("/sifre-degistir"));
  const sifreFormu = page.getByRole("form", { name: "Şifre değiştir" });
  await tabIle(page, sifreFormu.getByLabel("Mevcut şifre"));
  await page.keyboard.type("yanlis-sifre-123");
  await tabIle(page, sifreFormu.getByLabel("Yeni şifre"));
  await page.keyboard.type("baska-sifre-4567");
  await page.keyboard.press("Enter");
  await expect(sifreFormu.getByLabel("Mevcut şifre")).toHaveAttribute("aria-invalid", "true");

  // Review 1, F-05: başarılı değişiklik de klavyeyle tamamlanır. Ortak hesabın şifresi değişmesin diye
  // yeni eklenen kullanıcıyla yapılır.
  await tabIle(page, page.getByRole("button", { name: "Çıkış yap" }));
  await page.keyboard.press("Enter");
  await expect(page.getByRole("status")).toContainText("Çıkış yaptınız.");
  await page.goto(appUrl("/giris?returnTo=/sifre-degistir"));
  await tabIle(page, page.getByLabel("Kullanıcı adı"));
  await page.keyboard.type(ad);
  await tabIle(page, page.getByLabel("Şifre"));
  await page.keyboard.type(sifre);
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(appUrl("/sifre-degistir"));
  await tabIle(page, sifreFormu.getByLabel("Mevcut şifre"));
  await page.keyboard.type(sifre);
  await tabIle(page, sifreFormu.getByLabel("Yeni şifre"));
  await page.keyboard.type("baska-sifre-4567");
  await tabIle(page, sifreFormu.getByRole("button", { name: "Şifreyi değiştir" }));
  await page.keyboard.press("Enter");
  await expect(sifreFormu.getByText("Şifreniz değiştirildi.")).toBeVisible();
});

test("AC-12 giriş, kullanıcılar ve şifre değiştirme ekranlarında WCAG 2.1 AA ihlali yok, hata durumları dahil", async ({ page }) => {
  await page.goto(appUrl("/giris"));
  expect(await axeIhlalleri(page)).toEqual([]);
  await page.getByLabel("Kullanıcı adı").fill(E2E_KULLANICI);
  await page.getByLabel("Şifre").fill("yanlis-sifre-123");
  await page.getByRole("button", { name: "Giriş yap" }).click();
  await expect(page.getByRole("form", { name: "Giriş" }).getByRole("alert")).toBeVisible();
  expect(await axeIhlalleri(page)).toEqual([]);

  await girisYap(page, "/kullanicilar");
  await kullaniciEkleUi(page, yeniKullaniciAdi(), "erisim-sifresi-123");
  expect(await axeIhlalleri(page)).toEqual([]);
  const form = page.getByRole("form", { name: "Kullanıcı ekle" });
  await form.getByLabel("Kullanıcı adı").fill("ab");
  await form.getByLabel("Görünen ad").fill("x");
  await form.getByLabel("Başlangıç şifresi").fill("kisa");
  await form.getByRole("button", { name: "Kullanıcı ekle" }).click();
  await expect(form.getByLabel("Kullanıcı adı")).toHaveAttribute("aria-invalid", "true");
  expect(await axeIhlalleri(page)).toEqual([]);

  await page.goto(appUrl("/sifre-degistir"));
  expect(await axeIhlalleri(page)).toEqual([]);
  await cikisYap(page);
  expect(await axeIhlalleri(page)).toEqual([]);
});

test.describe("Spec 0005 — izlenen uygulamalar", () => {
  test.afterEach(izlemeleriTemizle);

  test("AC-12 uygulama ekleme, düzenleme ve izlemeden çıkarma yalnızca klavyeyle tamamlanır", async ({ page }) => {
    const ad = yeniServisAdi();
    await girisYap(page, "/izlenen-uygulamalar");

    const form = page.getByRole("form", { name: "Uygulama ekle" });
    await tabIle(page, form.getByLabel("ServiceName"));
    await page.keyboard.type(ad);
    await tabIle(page, form.getByLabel("Sağlık adresi"));
    await page.keyboard.type(saglikAdresi(`/${ad}`));
    await tabIle(page, form.getByRole("button", { name: "Uygulama ekle" }));
    await page.keyboard.press("Enter");
    await expect(form.getByText(`${ad} izlemeye alındı.`)).toBeVisible();

    await tabIle(page, page.getByRole("button", { name: `${ad}: Düzenle` }));
    await page.keyboard.press("Enter");
    const duzenle = page.getByRole("form", { name: `${ad}: sağlık adresini düzenle` });
    await tabIle(page, duzenle.getByLabel("Sağlık adresi"));
    await page.keyboard.press("Control+A");
    await page.keyboard.type(saglikAdresi(`/${ad}-yeni`));
    await page.keyboard.press("Enter");
    await expect(page.getByRole("cell", { name: saglikAdresi(`/${ad}-yeni`) })).toBeVisible();

    await tabIle(page, page.getByRole("button", { name: `${ad}: İzlemeden çıkar` }));
    await page.keyboard.press("Enter");
    await tabIle(page, page.getByRole("button", { name: "Evet, çıkar" }));
    await page.keyboard.press("Enter");
    await expect(page.getByText("Henüz izlenen uygulama yok")).toBeVisible();
  });

  test("AC-12 izlenen uygulamalar ekranında ve Down uyarısında WCAG 2.1 AA ihlali yok; durum metinle yazılır", async ({ page }) => {
    const ad = yeniServisAdi();
    await saglikAyarla(`/${ad}`, 503);
    await girisYap(page, "/izlenen-uygulamalar");
    expect(await axeIhlalleri(page)).toEqual([]);

    const form = page.getByRole("form", { name: "Uygulama ekle" });
    await form.getByLabel("ServiceName").fill(ad);
    await form.getByLabel("Sağlık adresi").fill("ftp://kural-disi");
    await form.getByRole("button", { name: "Uygulama ekle" }).click();
    await expect(form.getByLabel("Sağlık adresi")).toHaveAttribute("aria-invalid", "true");
    expect(await axeIhlalleri(page)).toEqual([]);

    await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
    const durum = page.getByRole("row").filter({ hasText: ad }).getByRole("cell").nth(2);
    await expect
      .poll(async () => {
        await page.reload();
        return durum.textContent();
      }, { timeout: 20_000 })
      .toBe("Down");
    // Uyarı role="alert" ile duyurulur; Down satırı ve uyarı birlikte denetlenir.
    await expect(page.getByRole("alert").filter({ hasText: ad })).toBeVisible();
    expect(await axeIhlalleri(page)).toEqual([]);

    await page.goto(appUrl("/logs"));
    await expect(page.getByRole("alert").filter({ hasText: ad })).toBeVisible();
    expect(await axeIhlalleri(page)).toEqual([]);
  });
});
