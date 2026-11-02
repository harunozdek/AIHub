import { expect, test, type Page } from "@playwright/test";
import { appUrl, girisYap, izlemeleriTemizle, saglikAdresi, saglikAyarla, uygulamaEkleUi, yeniServisAdi } from "./seed";

// Spec 0005 — AC-1…AC-5, AC-9, AC-10: "İzlenen uygulamalar" ekranı. Denetim aralığı e2e'de 1 sn (global-setup).

test.afterEach(izlemeleriTemizle);

function satir(page: Page, ad: string) {
  return page.getByRole("row").filter({ has: page.getByRole("cell", { name: ad, exact: true }) });
}

/** Sütunlar: 0 ServiceName, 1 adres, 2 durum, 3 son denetim, 4 son başarısızlık, 5 Down zamanı, 6 ekleyen, 7 değiştiren. */
function hucre(page: Page, ad: string, sutun: number) {
  return satir(page, ad).getByRole("cell").nth(sutun);
}

/** Ekran kendiliğinden yenilenmez; durum değişimi sayfa yeniden yüklenerek okunur. */
async function durumBekle(page: Page, ad: string, durum: string) {
  await expect
    .poll(async () => {
      await page.reload();
      return hucre(page, ad, 2).textContent();
    }, { timeout: 20_000 })
    .toBe(durum);
}

test("AC-1 oturumsuz kullanıcı izlenen uygulamalar ekranı yerine giriş ekranına gider", async ({ page }) => {
  await page.goto(appUrl("/izlenen-uygulamalar"));

  await expect(page).toHaveURL((url) => url.pathname === "/giris");
});

test("AC-2 eklenen uygulama Bilinmiyor durumunda ekleyenle görünür; aynı ServiceName alan yanında reddedilir", async ({ page }) => {
  const ad = yeniServisAdi();
  // İlk denetimler zaman aşımına düşer: üç başarısızlığa kadar durum Bilinmiyor kalır.
  await saglikAyarla(`/${ad}`, 200, 5_000);
  await girisYap(page, "/izlenen-uygulamalar");

  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));

  for (const baslik of ["ServiceName", "Sağlık adresi", "Durum", "Son denetim", "Son başarısızlık", "Down olduğu zaman", "Ekleyen", "Son değiştiren"]) {
    await expect(page.getByRole("columnheader", { name: baslik })).toBeVisible();
  }
  await expect(hucre(page, ad, 2)).toHaveText("Bilinmiyor");
  await expect(hucre(page, ad, 6)).toContainText("E2E Kullanıcısı");
  await expect(hucre(page, ad, 7)).toHaveText("—");

  const form = page.getByRole("form", { name: "Uygulama ekle" });
  await form.getByLabel("ServiceName").fill(ad);
  await form.getByLabel("Sağlık adresi").fill(saglikAdresi("/baska"));
  await form.getByRole("button", { name: "Uygulama ekle" }).click();
  await expect(form.getByLabel("ServiceName")).toHaveAttribute("aria-invalid", "true");
  await expect(form.getByLabel("ServiceName")).toHaveAccessibleDescription(/zaten izleniyor/);
  await expect(satir(page, ad)).toHaveCount(1);
});

test("AC-3 kural dışı adres ve boş ServiceName alan yanında mesajla reddedilir", async ({ page }) => {
  await girisYap(page, "/izlenen-uygulamalar");
  const form = page.getByRole("form", { name: "Uygulama ekle" });

  for (const [ad, adres, alan, mesaj] of [
    [yeniServisAdi(), "ftp://odeme.kurum.local/health", "Sağlık adresi", "http:// ya da https://"],
    [yeniServisAdi(), "/health", "Sağlık adresi", "http:// ya da https://"],
    [yeniServisAdi(), "http://kullanici:sifre@odeme.kurum.local/health", "Sağlık adresi", "kullanıcı adı veya şifre"],
    [yeniServisAdi(), "http://169.254.169.254/latest/meta-data", "Sağlık adresi", "engellenen"],
    ["   ", saglikAdresi("/bos-ad"), "ServiceName", "boş olamaz"],
  ] as const) {
    // Önceki gönderim bitmeden doldurulan alanları, o gönderimin sonundaki form sıfırlaması siler.
    await expect(form.getByRole("button", { name: "Uygulama ekle" })).toBeEnabled();
    await form.getByLabel("ServiceName").fill(ad);
    await form.getByLabel("Sağlık adresi").fill(adres);
    await form.getByRole("button", { name: "Uygulama ekle" }).click();

    const alanOgesi = form.getByLabel(alan);
    await expect(alanOgesi).toHaveAttribute("aria-invalid", "true");
    await expect(alanOgesi).toHaveAccessibleDescription(new RegExp(mesaj));
  }
  await expect(page.getByText("Henüz izlenen uygulama yok")).toBeVisible();
});

test("AC-4 AC-9 Ayakta olan uygulamanın adresi değişince Bilinmiyor olur ve değiştiren görünür; çıkarma onay ister", async ({ page }) => {
  const ad = yeniServisAdi();
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumBekle(page, ad, "Ayakta");
  await expect(hucre(page, ad, 3)).not.toHaveText("—");

  await saglikAyarla(`/${ad}-yeni`, 200, 5_000);
  await satir(page, ad).getByRole("button", { name: `${ad}: Düzenle` }).click();
  const duzenle = page.getByRole("form", { name: `${ad}: sağlık adresini düzenle` });
  await duzenle.getByLabel("Sağlık adresi").fill(saglikAdresi(`/${ad}-yeni`));
  await duzenle.getByRole("button", { name: "Kaydet" }).click();

  await expect(hucre(page, ad, 1)).toHaveText(saglikAdresi(`/${ad}-yeni`));
  await expect(hucre(page, ad, 2)).toHaveText("Bilinmiyor");
  await expect(hucre(page, ad, 7)).toContainText("E2E Kullanıcısı");

  await satir(page, ad).getByRole("button", { name: `${ad}: İzlemeden çıkar` }).click();
  await satir(page, ad).getByRole("button", { name: "Vazgeç" }).click();
  await expect(satir(page, ad)).toHaveCount(1);

  await satir(page, ad).getByRole("button", { name: `${ad}: İzlemeden çıkar` }).click();
  await satir(page, ad).getByRole("button", { name: "Evet, çıkar" }).click();
  await expect(satir(page, ad)).toHaveCount(0);
  await expect(page.getByText("Henüz izlenen uygulama yok")).toBeVisible();
});

test("AC-5 AC-10 Down olan uygulama nedeniyle listenin en üstünde ve vurgulu görünür", async ({ page }) => {
  const ayakta = `a-${yeniServisAdi()}`;
  const down = `z-${yeniServisAdi()}`;
  await saglikAyarla(`/${down}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ayakta, saglikAdresi(`/${ayakta}`));
  await uygulamaEkleUi(page, down, saglikAdresi(`/${down}`));

  await durumBekle(page, down, "Down");

  const satirlar = page.getByRole("row");
  await expect(satirlar.nth(1).getByRole("cell").first()).toHaveText(down);
  await expect(hucre(page, down, 4)).toHaveText("HTTP 503");
  await expect(hucre(page, down, 5)).not.toHaveText("—");
  await expect(satir(page, down)).toHaveClass(/bg-red-50/);
  await expect(hucre(page, ayakta, 2)).toHaveText("Ayakta");
});
