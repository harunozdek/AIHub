import { expect, test } from "@playwright/test";
import { E2E_KULLANICI, E2E_SIFRE } from "./kimlik";
import { appUrl, girisFormunuDoldur, girisYap, kayitEkle, kullaniciEkleUi, saatOnce, suresiDolmusOturum, yeniKullaniciAdi, yeniServisAdi } from "./seed";

// Spec 0004 — AC-1, AC-2, AC-3, AC-10: giriş zorunluluğu, aynı sayfaya dönüş, belirsiz hata, süresi dolan
// oturum, çıkış. 0002'nin giriş testlerinin yerini alır (plan 0004, test eşlemesi).

const girisEkrani = (url: URL) => url.pathname === "/giris";

test("AC-1 oturumsuz kullanıcı giriş ekranına yönlendirilir ve log içeriği görmez", async ({ page }) => {
  const servis = yeniServisAdi();
  const isaret = `gizli-${servis}`;
  const id = await kayitEkle({ servis, alinma: saatOnce(1), message: isaret });

  for (const adres of [`/logs?service=${servis}`, `/logs/${id}`, "/", "/kullanicilar", "/sifre-degistir"]) {
    await page.goto(appUrl(adres));

    await expect(page).toHaveURL(girisEkrani);
    await expect(page.getByLabel("Kullanıcı adı")).toBeVisible();
    await expect(page.locator("body")).not.toContainText(isaret);
  }
});

test("AC-2 giriş yapan kullanıcı istediği sayfaya aynı filtrelerle döner, kullanıcı adı büyük-küçük harf duyarsızdır", async ({ page }) => {
  const servis = yeniServisAdi();
  const isaret = `kayit-${servis}`;
  await kayitEkle({ servis, alinma: saatOnce(1), level: "Error", message: isaret });
  const hedef = `/logs?service=${servis}&level=Error`;

  await page.goto(appUrl(hedef));
  await girisFormunuDoldur(page, E2E_KULLANICI.toUpperCase());

  await expect(page).toHaveURL(appUrl(hedef));
  await expect(page.getByRole("table")).toContainText(isaret);
  await expect(page.getByLabel("Servis")).toHaveValue(servis);
  await expect(page.getByRole("checkbox", { name: "Error" })).toBeChecked();
});

test("AC-10 süresi dolmuş oturumda giriş ekranı süre bilgisiyle gelir, girişten sonra aynı sayfaya dönülür", async ({ page, context }) => {
  const servis = yeniServisAdi();
  const isaret = `kayit-${servis}`;
  await kayitEkle({ servis, alinma: saatOnce(1), level: "Error", message: isaret });
  await context.addCookies([{ name: "aihub_session", value: await suresiDolmusOturum(), url: appUrl("/") }]);
  const hedef = `/logs?service=${servis}&level=Error`;

  await page.goto(appUrl(hedef));
  await expect(page.getByRole("status")).toContainText("Oturumunuzun süresi doldu");
  await girisFormunuDoldur(page);

  await expect(page).toHaveURL(appUrl(hedef));
  await expect(page.getByRole("table")).toContainText(isaret);
});

test("AC-3 yanlış şifre ve var olmayan kullanıcı adı aynı mesajı alır, oturum açılmaz", async ({ page }) => {
  const mesajlar: string[] = [];
  for (const [kullanici, sifre] of [
    [E2E_KULLANICI, "yanlis-sifre-123"],
    [`olmayan-${yeniServisAdi()}`, E2E_SIFRE],
  ]) {
    await page.goto(appUrl("/giris"));
    await girisFormunuDoldur(page, kullanici, sifre);

    const hata = page.getByRole("form", { name: "Giriş" }).getByRole("alert");
    await expect(hata).toBeVisible();
    mesajlar.push((await hata.textContent()) ?? "");
    await expect(page).toHaveURL(girisEkrani);
  }

  expect(mesajlar[0]).toBe("Kullanıcı adı veya şifre hatalı.");
  expect(mesajlar[1]).toBe(mesajlar[0]);
  await page.goto(appUrl("/logs"));
  await expect(page).toHaveURL(girisEkrani);
});

// Review 1, F-04: plan eşlemesi pasif ve kilitli hesabı da sayar; ikisi doğru şifreyle de aynı mesajı alır.
test("AC-3 pasif ve kilitli hesap doğru şifreyle de aynı mesajı alır, oturum açılmaz", async ({ page, browser }) => {
  const sifre = "baslangic-sifresi-123";
  const pasif = yeniKullaniciAdi();
  const kilitli = yeniKullaniciAdi();
  const yoneticiBaglam = await browser.newContext();
  const yonetici = await yoneticiBaglam.newPage();
  await girisYap(yonetici, "/kullanicilar");
  await kullaniciEkleUi(yonetici, pasif, sifre);
  await kullaniciEkleUi(yonetici, kilitli, sifre);
  await yonetici.getByRole("button", { name: `${pasif}: Pasifleştir` }).click();
  await yonetici.getByRole("button", { name: "Evet, pasifleştir" }).click();
  await expect(yonetici.getByRole("button", { name: `${pasif}: Etkinleştir` })).toBeVisible();
  await yoneticiBaglam.close();

  const hata = page.getByRole("form", { name: "Giriş" }).getByRole("alert");
  // Art arda 5 yanlış deneme hesabı kilitler (AC-4).
  for (let i = 0; i < 5; i++) {
    await page.goto(appUrl("/giris"));
    await girisFormunuDoldur(page, kilitli, "yanlis-sifre-123");
    await expect(hata).toHaveText("Kullanıcı adı veya şifre hatalı.");
  }

  for (const kullanici of [pasif, kilitli]) {
    await page.goto(appUrl("/giris"));
    await girisFormunuDoldur(page, kullanici, sifre);

    await expect(hata).toHaveText("Kullanıcı adı veya şifre hatalı.");
    await expect(page).toHaveURL(girisEkrani);
  }
  await page.goto(appUrl("/logs"));
  await expect(page).toHaveURL(girisEkrani);
});

test("AC-10 çıkıştan sonra bilgi görünür ve geri tuşu log içeriği göstermez", async ({ page }) => {
  const servis = yeniServisAdi();
  const isaret = `kayit-${servis}`;
  await kayitEkle({ servis, alinma: saatOnce(1), message: isaret });
  await girisYap(page, `/logs?service=${servis}`);
  await expect(page.getByRole("table")).toContainText(isaret);

  await page.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(page.getByRole("status")).toContainText("Çıkış yaptınız.");
  await page.goBack();

  await page.waitForLoadState();
  await expect(page.locator("body")).not.toContainText(isaret);
  await expect(page).not.toHaveURL(appUrl(`/logs?service=${servis}`));
});

// 0002 review 1, F-01: dönüş adresi yalnızca panelin kendi adresi olabilir. URL ayrıştırıcısı tab ve satır
// sonlarını sildiği için "/\t/site" gibi değerler başka siteye çözülür; hepsi listeye döner.
// 0002 review 2, R2-01: dot-segment'ler normalize edilince yol "//site" olur; bu da başka siteye çözülür.
// 0004 review 2, R2-01: oturumu kapatan `/auth/` rotaları da dönüş adresi olamaz.
for (const kotu of [
  "/\t/evil.invalid",
  "/\n/evil.invalid",
  "/\r/evil.invalid",
  "/\\evil.invalid",
  "//evil.invalid",
  "https://evil.invalid/logs",
  "logs",
  "/.//evil.invalid",
  "/..//evil.invalid",
  "/%2e//evil.invalid",
  "/%2E%2E//evil.invalid/x",
  "/a/..//evil.invalid",
  "/auth/oturum-dustu",
  "/auth/cikis",
]) {
  test(`AC-2 girişten sonra dış veya oturum kapatan adrese yönlendirilmez: ${JSON.stringify(kotu)}`, async ({ page }) => {
    await page.goto(appUrl(`/giris?returnTo=${encodeURIComponent(kotu)}`));
    await girisFormunuDoldur(page);

    await expect(page).toHaveURL(appUrl("/dashboard"));
  });
}
