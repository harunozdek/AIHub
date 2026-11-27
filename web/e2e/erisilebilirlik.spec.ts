import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { E2E_KULLANICI, E2E_SIFRE } from "./kimlik";
import { appUrl, ayaktaSatiri, cikisYap, dagilimBolumu, dagilimDilimleri, dilimAraliklari, girisYap, HALKA_CEVRESI, izlemeleriTemizle, kayitEkle, kayitlarEkle, kullaniciEkleUi, saatOnce, durumuBekle, izlemeBolumu, saglikAdresi, saglikAyarla, sql, uygulamaEkleUi, yeniKullaniciAdi, yeniServisAdi } from "./seed";

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
    await expect(page.getByRole("cell", { name: saglikAdresi(`/${ad}-yeni`) })).toBeVisible({ timeout: 5_000 });

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
    await durumuBekle(page, ad, "Down");
    // Uyarı role="alert" ile duyurulur; Down kartı ve uyarı birlikte denetlenir.
    await expect(page.getByRole("alert").filter({ hasText: ad })).toBeVisible();
    expect(await axeIhlalleri(page)).toEqual([]);

    await page.goto(appUrl("/logs"));
    await expect(page.getByRole("alert").filter({ hasText: ad })).toBeVisible();
    expect(await axeIhlalleri(page)).toEqual([]);
  });

  // Spec 0012, CB-7: iki bölümle ve boş Down bölümüyle, açık ve koyu temada WCAG 2.1 AA (simgeler metin dışı içerik
  // olarak 3:1); bölümler adlarıyla gezilir, başlıklar başlık olarak okunur.
  test("0012 CB-7 iki bölüm ve boş Down bölümü açık ve koyu temada WCAG 2.1 AA ihlali içermez", async ({ page }) => {
    const up = yeniServisAdi();
    const down = yeniServisAdi();
    await girisYap(page, "/izlenen-uygulamalar");
    await uygulamaEkleUi(page, up, saglikAdresi(`/${up}`));
    await durumuBekle(page, up, "Ayakta");

    for (const tema of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme: tema });
      await expect(izlemeBolumu(page, "Down").getByText("Down olan uygulama yok")).toBeVisible();
      expect(await axeIhlalleri(page), `boş Down, ${tema}`).toEqual([]);
    }

    await saglikAyarla(`/${down}`, 503);
    await uygulamaEkleUi(page, down, saglikAdresi(`/${down}`));
    await durumuBekle(page, down, "Down");
    for (const tema of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme: tema });
      expect(await axeIhlalleri(page), `iki bölüm, ${tema}`).toEqual([]);
    }

    // Review 2 B-3: dar ekranda Ayakta uygulamaları karttır; kartlar da taranır.
    await page.setViewportSize({ width: 320, height: 800 });
    await expect(izlemeBolumu(page, "Ayakta").getByRole("table")).toHaveCount(0);
    await expect(ayaktaSatiri(page, up)).toBeVisible();
    for (const tema of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme: tema });
      expect(await axeIhlalleri(page), `dar ekran kartları, ${tema}`).toEqual([]);
    }
    await page.setViewportSize({ width: 1280, height: 720 });

    await expect(izlemeBolumu(page, "Ayakta").getByRole("heading", { level: 2 })).toHaveText(/^Ayakta \(\d+\)$/);
    await expect(izlemeBolumu(page, "Down").getByRole("heading", { level: 2 })).toHaveText(/^Down \(\d+\)$/);
  });
  // Spec 0013, CB-5: grafik tek özet olarak okunur, dilimler ayrıca okunmaz; durumlar açıklamada simge ve adla; dilimleri
  // zemin renginde boşluk ayırır, her dilim zemine karşı 3:1 (revizyon 1); grafik değişimi ayrıca duyurulmaz; axe temiz.
  test("0013 CB-5 durum dağılımı tek özet okunur, dilimler boşlukla ayrılır ve zemine karşı 3:1, WCAG 2.1 AA ihlali yok", async ({ page }) => {
    const up = yeniServisAdi();
    const up2 = yeniServisAdi();
    const down = yeniServisAdi();
    await girisYap(page, "/izlenen-uygulamalar");
    await uygulamaEkleUi(page, up, saglikAdresi(`/${up}`));
    await durumuBekle(page, up, "Ayakta");
    const bolum = dagilimBolumu(page);
    const ozet = bolum.getByRole("img");
    await expect(ozet).toHaveAccessibleName("Durum dağılımı: 1 uygulama; Ayakta 1, Down 0, Bilinmiyor 0");
    // Bilinmiyor → Ayakta geçişi grafiği değiştirdi; duyurulmaz. Grafik canlı bölge değildir, canlı bölgenin içinde de değildir.
    await expect(page.locator("p.sr-only[aria-live=polite]")).toHaveText("");
    expect(await bolum.evaluate((el) => el.closest("[aria-live]") === null && el.querySelector("[aria-live]") === null)).toBe(true);

    // Paylar eşit değil (2 Ayakta, 1 Down): dilimlerin uzunluğu ve yeri sıraya göre eşit bölmeden ayırt edilir (Review 2 N-1).
    await uygulamaEkleUi(page, up2, saglikAdresi(`/${up2}`));
    await durumuBekle(page, up2, "Ayakta");
    await saglikAyarla(`/${down}`, 503);
    await uygulamaEkleUi(page, down, saglikAdresi(`/${down}`));
    await durumuBekle(page, down, "Down");
    await expect(ozet).toHaveAccessibleName("Durum dağılımı: 3 uygulama; Ayakta 2, Down 1, Bilinmiyor 0");
    // Dilimler ve orta metin ekran okuyucudan gizli: özet bir kez okunur.
    await expect(ozet.locator("svg")).toHaveAttribute("aria-hidden", "true");
    await expect(ozet.locator("div")).toHaveAttribute("aria-hidden", "true");
    // Durumlar yalnızca renkle ayrılmaz: açıklamada simge ve ad.
    await expect(bolum.getByRole("listitem")).toHaveText([/^Ayakta /, /^Down /, /^Bilinmiyor /]);
    await expect(bolum.getByRole("listitem").locator("svg[data-durum]")).toHaveCount(3);

    // Boşluk: iki dilimin çizilen uzunlukları + her dilimden sonra 2 birim boşluk = çevre.
    const dilimler = await dagilimDilimleri(page);
    expect(dilimler.map((d) => d.durum)).toEqual(["Up", "Down"]);
    expect(Math.abs(dilimler.reduce((t, d) => t + d.uzunluk + 2, 0) - HALKA_CEVRESI)).toBeLessThan(0.5);
    // Uzunluklar paylarla orantılı: Ayakta 2/3, Down 1/3 (çizilen uzunluk + 2 birim boşluk).
    expect(Math.abs(dilimler[0].uzunluk + 2 - (HALKA_CEVRESI * 2) / 3), "Ayakta dilimi 2/3").toBeLessThan(0.5);
    expect(Math.abs(dilimler[1].uzunluk + 2 - HALKA_CEVRESI / 3), "Down dilimi 1/3").toBeLessThan(0.5);
    // Boşluklar dilimlerin arasında: her dilimin bitişi ile sonrakinin başlangıcı arası 2 birim (üst üste çizim yok).
    for (const aralik of dilimAraliklari(dilimler)) expect(Math.abs(aralik - 2)).toBeLessThan(0.5);

    for (const genislik of [1280, 320]) {
      await page.setViewportSize({ width: genislik, height: 900 });
      for (const tema of ["light", "dark"] as const) {
        await page.emulateMedia({ colorScheme: tema });
        // Her dilimin rengi ile bölümün görünen zemini arasındaki karşıtlık (hesaplanan renkler, sRGB).
        const oranlar = await bolum.evaluate((el) => {
          const rgb = (renk: string) => {
            const tuval = document.createElement("canvas");
            tuval.width = tuval.height = 1;
            const c = tuval.getContext("2d")!;
            c.fillStyle = renk;
            c.fillRect(0, 0, 1, 1);
            return [...c.getImageData(0, 0, 1, 1).data].slice(0, 3);
          };
          const parlaklik = (renk: string) => {
            const [r, g, b] = rgb(renk).map((v) => {
              const s = v / 255;
              return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
            });
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
          };
          let zemin = "rgb(255, 255, 255)";
          for (let e: Element | null = el; e; e = e.parentElement) {
            const b = getComputedStyle(e).backgroundColor;
            if (b !== "rgba(0, 0, 0, 0)" && b !== "transparent") {
              zemin = b;
              break;
            }
          }
          const z = parlaklik(zemin);
          const oran = (renk: string) => {
            const p = parlaklik(renk);
            return (Math.max(p, z) + 0.05) / (Math.min(p, z) + 0.05);
          };
          // Çizilen dilimler ve (sayısı 0 olan Bilinmiyor da dahil) üç durumun rengi: açıklama simgeleri aynı kaynaktan
          // boyanır (`durumRengi`; dilim ile simgenin aynı renk olduğu izlenen-uygulamalar.spec.ts'te sınanır).
          return [
            ...[...el.querySelectorAll("[role=img] svg circle")].map((d) => ({ durum: `dilim ${d.getAttribute("data-durum")}`, oran: oran(getComputedStyle(d).stroke) })),
            ...[...el.querySelectorAll("li svg[data-durum]")].map((d) => ({ durum: `renk ${d.getAttribute("data-durum")}`, oran: oran(getComputedStyle(d).color) })),
          ];
        });
        expect(oranlar).toHaveLength(5);
        for (const o of oranlar) expect(o.oran, `${o.durum} zemine karşı (${genislik} px, ${tema})`).toBeGreaterThanOrEqual(3);
        expect(await axeIhlalleri(page), `durum dağılımı, ${genislik} px, ${tema}`).toEqual([]);
      }
    }
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.emulateMedia({ colorScheme: "light" });
  });
});

// Spec 0006, AC-9: dashboard yalnızca klavyeyle kullanılır; sayılar ve durumlar metinle de belirtilir; WCAG 2.1 AA.
test("0006 AC-9 dashboard klavyeyle kullanılır, durumlar metinle belirtilir ve WCAG 2.1 AA ihlali yok", async ({ page }) => {
  const servis = yeniServisAdi();
  const down = yeniServisAdi();
  await kayitEkle({ servis, alinma: saatOnce(1), level: "Critical", message: "erisilebilirlik", exceptionType: "E2E.ErisilebilirlikException" });
  await saglikAyarla(`/${down}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, down, saglikAdresi(`/${down}`));
  try {
    await expect
      .poll(async () => {
        await page.goto(appUrl("/dashboard"));
        // Uygulama bölümü akışla sonradan gelir; beklemeyen count() yükleniyor satırını okumasın (docs/testing.md).
        const uygulamaBolumu = page.getByRole("region", { name: "İzlenen uygulamalar" });
        await expect(uygulamaBolumu).toBeVisible();
        await expect(uygulamaBolumu.getByText("İzlenen uygulamalar yükleniyor…")).toHaveCount(0);
        return page.getByRole("list", { name: "Down olan uygulamalar" }).count();
      }, { timeout: 20_000 })
      .toBe(1);

    // Bölümler başlıklarıyla adlandırılır; durum metinle yazılır.
    for (const baslik of ["Önem dereceleri", "En çok hata üreten servisler", "En sık exception'lar", "İzlenen uygulamalar"]) {
      await expect(page.getByRole("region", { name: baslik })).toBeVisible();
    }
    await expect(page.getByRole("list", { name: "Down olan uygulamalar" })).toContainText(`${down} — Down`);

    expect((await new AxeBuilder({ page }).withTags(wcag21aa).analyze()).violations).toEqual([]);

    // Klavye: "Yenile", derece, servis, exception ve durum özeti bağlantıları sırayla Tab ile erişilir (VERIFY G-4);
    // Enter durum özetini açar.
    const bolum = (baslik: string) => page.getByRole("region", { name: baslik });
    await tabIle(page, page.getByRole("button", { name: "Yenile" }));
    await tabIle(page, bolum("Önem dereceleri").getByRole("link", { name: /^Critical / }));
    await tabIle(page, bolum("En çok hata üreten servisler").getByRole("link").first());
    await tabIle(page, bolum("En sık exception'lar").getByRole("link").first());
    await tabIle(page, bolum("İzlenen uygulamalar").getByRole("link", { name: /Durum özeti/ }));
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(appUrl("/izlenen-uygulamalar"));

    // Enter derece sayısında listeyi açar.
    await page.goto(appUrl("/dashboard"));
    await tabIle(page, bolum("Önem dereceleri").getByRole("link", { name: /^Critical / }));
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL((url) => url.pathname === "/logs" && url.searchParams.getAll("level").join() === "Critical");

    // Arızada "Yeniden dene" de klavyeyle erişilir ve Enter yeniden yükler.
    await page.goto(appUrl("/dashboard"));
    // R-03 (plan 0012): sayfanın ilk akışı bitmeden "Yenile"ye basılırsa geç gelen akış parçası gizli bir kopya olarak
    // DOM'da kalır ve aşağıdaki metin iki öğede bulunur. Arıza, bölüm yüklendikten sonra başlatılır.
    await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();
    const yenidenDene = bolum("Önem dereceleri").getByRole("button", { name: "Yeniden dene" });
    await sql("ALTER TABLE log_entries RENAME TO log_entries_e2e_ariza");
    try {
      await page.getByRole("button", { name: "Yenile" }).click();
      await expect(yenidenDene).toBeVisible();
    } finally {
      await sql("ALTER TABLE log_entries_e2e_ariza RENAME TO log_entries");
    }
    await tabIle(page, yenidenDene);
    await page.keyboard.press("Enter");
    await expect(yenidenDene).toHaveCount(0);
    await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();
  } finally {
    await izlemeleriTemizle();
  }
});

// Spec 0007, CB-5: grafik metinle de okunur, dereceler yalnızca renkle ayrılmaz, dilimler klavyeyle seçilip açılır;
// WCAG 2.1 AA.
test("0007 CB-5 saatlik trend klavyeyle kullanılır, dilimler metinle okunur ve WCAG 2.1 AA ihlali yok", async ({ page }) => {
  await kayitEkle({ servis: yeniServisAdi(), alinma: saatOnce(1), level: "Error", message: "trend-erisilebilirlik" });
  await girisYap(page, "/dashboard");

  const trend = page.getByRole("region", { name: "Son 24 saat" });
  await expect(trend.getByRole("list", { name: "Dereceler" }).getByRole("listitem")).toHaveText(["Critical", "Error", "Warning", "Info"]);
  const dilimler = trend.getByRole("list", { name: /^Saatlik dağılım/ }).getByRole("link");
  await expect(dilimler).toHaveCount(24);
  await expect(dilimler.first()).toHaveAccessibleName(/^\d\d:\d\d–\d\d:\d\d: Critical [\d.]+, Error [\d.]+, Warning [\d.]+, Info [\d.]+ kayıt, log listesinde aç$/);

  expect((await new AxeBuilder({ page }).withTags(wcag21aa).analyze()).violations).toEqual([]);

  await tabIle(page, page.getByRole("button", { name: "Yenile" }));
  await tabIle(page, dilimler.first());
  await tabIle(page, dilimler.last());
  const hedef = new URL((await dilimler.last().getAttribute("href"))!, appUrl("/")).searchParams;
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL((url) => url.pathname === "/logs" && url.searchParams.get("from") === hedef.get("from") && url.searchParams.get("to") === hedef.get("to"));
});

// Spec 0011, CB-6: seçim kontrolü etiketlidir, yalnızca klavyeyle değiştirilir, odak görünürdür; pencere değişince ekran
// okuyucu yeni pencereyi duyar; her seçenekte ve serbest aralıkta (hata mesajı açıkken) WCAG 2.1 AA ihlali yoktur.
test("0011 CB-6 zaman penceresi klavyeyle seçilir, duyurulur ve her seçenekte WCAG 2.1 AA ihlali yok", async ({ page }) => {
  await kayitEkle({ servis: yeniServisAdi(), alinma: saatOnce(1), level: "Warning", message: "pencere-erisilebilirlik" });
  await girisYap(page, "/dashboard");

  const secim = page.getByLabel("Zaman penceresi");
  const duyuru = page.locator("[aria-live=polite]").filter({ hasText: /^Zaman penceresi:/ });
  await expect(duyuru).toHaveText("Zaman penceresi: Son 24 saat");

  // Yalnızca klavye: Tab ile seçime gelinir (odak görünür), ok tuşu bir sonraki pencereyi seçer.
  // Canlı bölge değişiklikten önce DOM'da durur (aynı düğüm): içeriğiyle yeni eklenen bölge duyurulmaz.
  await duyuru.evaluate((el) => el.setAttribute("data-kalici", ""));
  await tabIle(page, secim);
  await page.keyboard.press("ArrowDown");
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "48s");
  await expect(duyuru).toHaveText("Zaman penceresi: Son 48 saat");
  await expect(page.getByText(/Yüklendi:/)).toBeVisible();
  // Yeni pencere gelince odak seçimde kalır; klavye kullanıcısı ok tuşuyla devam eder (Review 1, F-1).
  await expect(secim).toBeFocused();
  await expect(duyuru).toHaveAttribute("data-kalici", "");
  await page.keyboard.press("ArrowDown");
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "7g");
  await expect(duyuru).toHaveText("Zaman penceresi: Son 1 hafta");

  for (const deger of ["24s", "48s", "7g", "14g", "30g", "90g", "180g", "365g"]) {
    await page.goto(appUrl(`/dashboard?pencere=${deger}`));
    await expect(page.getByText(/Yüklendi:/)).toBeVisible();
    expect((await new AxeBuilder({ page }).withTags(wcag21aa).analyze()).violations, deger).toEqual([]);
  }

  // Serbest aralık klavyeyle: seçenek, iki tarih alanı ve "Uygula"; hatalı aralıkta mesaj alanın açıklamasıdır.
  await page.goto(appUrl("/dashboard"));
  await tabIle(page, secim);
  await secim.selectOption({ label: "Tarih aralığı seç" });
  const baslangic = page.getByLabel("Başlangıç tarihi");
  const bitis = page.getByLabel("Bitiş tarihi");
  await tabIle(page, baslangic);
  await tabIle(page, bitis);
  await bitis.fill("2020-01-01");
  await tabIle(page, page.getByRole("button", { name: "Uygula" }));
  await page.keyboard.press("Enter");
  await expect(bitis).toHaveAccessibleDescription("Bitiş tarihi başlangıç tarihinden önce olamaz.");
  expect((await new AxeBuilder({ page }).withTags(wcag21aa).analyze()).violations).toEqual([]);

  const bugun = new Intl.DateTimeFormat("sv-SE", { timeZone: "Europe/Istanbul" }).format(new Date());
  await bitis.fill(bugun);
  await page.getByRole("button", { name: "Uygula" }).focus();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "aralik");
  await expect(duyuru).toHaveText(/^Zaman penceresi: \d+ .+ \d{4}$/);
  await expect(page.getByText(/Yüklendi:/)).toBeVisible();
  expect((await new AxeBuilder({ page }).withTags(wcag21aa).analyze()).violations).toEqual([]);
});
