import { expect, test } from "@playwright/test";
import { appUrl, cikisYap, girisFormunuDoldur, girisYap, kullaniciEkleUi, yeniKullaniciAdi } from "./seed";

// Spec 0004 — AC-9: kullanıcı mevcut şifresini doğrulayarak kendi şifresini değiştirir.

const ESKI = "baslangic-sifresi-123";
const YENI = "yepyeni-sifre-4567";

test("AC-9 şifre değişince bu oturum sürer, diğer oturum düşer, eski şifre kabul edilmez", async ({ page, browser }) => {
  const ad = yeniKullaniciAdi();
  await girisYap(page, "/kullanicilar");
  await kullaniciEkleUi(page, ad, ESKI);
  await cikisYap(page);

  await page.goto(appUrl("/sifre-degistir"));
  await girisFormunuDoldur(page, ad, ESKI);
  const digerBaglam = await browser.newContext();
  const diger = await digerBaglam.newPage();
  await diger.goto(appUrl("/logs"));
  await girisFormunuDoldur(diger, ad, ESKI);
  await expect(diger).toHaveURL(appUrl("/logs"));

  const form = page.getByRole("form", { name: "Şifre değiştir" });
  await form.getByLabel("Mevcut şifre").fill(ESKI);
  await form.getByLabel("Yeni şifre").fill(YENI);
  await form.getByRole("button", { name: "Şifreyi değiştir" }).click();
  await expect(form.getByText("Şifreniz değiştirildi.")).toBeVisible();

  await page.goto(appUrl("/logs"));
  await expect(page).toHaveURL(appUrl("/logs"));
  await diger.goto(appUrl("/logs"));
  await expect(diger).toHaveURL((url) => url.pathname === "/giris");
  await digerBaglam.close();

  await cikisYap(page);
  await girisFormunuDoldur(page, ad, ESKI);
  await expect(page.getByRole("form", { name: "Giriş" }).getByRole("alert")).toHaveText("Kullanıcı adı veya şifre hatalı.");
  await girisFormunuDoldur(page, ad, YENI);
  await expect(page).toHaveURL(appUrl("/dashboard"));
});

test("AC-9 mevcut şifre yanlışsa veya yeni şifre kurala uymuyorsa değişmez ve neden alan yanında gösterilir", async ({ page }) => {
  const ad = yeniKullaniciAdi();
  await girisYap(page, "/kullanicilar");
  await kullaniciEkleUi(page, ad, ESKI);
  await cikisYap(page);
  await page.goto(appUrl("/sifre-degistir"));
  await girisFormunuDoldur(page, ad, ESKI);
  const form = page.getByRole("form", { name: "Şifre değiştir" });

  for (const [mevcut, yeni, alan, mesaj] of [
    ["yanlis-sifre-123", YENI, "Mevcut şifre", "Mevcut şifre hatalı"],
    [ESKI, "kisa", "Yeni şifre", "12–128 karakter"],
  ] as const) {
    await form.getByLabel("Mevcut şifre").fill(mevcut);
    await form.getByLabel("Yeni şifre").fill(yeni);
    await form.getByRole("button", { name: "Şifreyi değiştir" }).click();

    await expect(form.getByLabel(alan)).toHaveAttribute("aria-invalid", "true");
    await expect(form.getByLabel(alan)).toHaveAccessibleDescription(new RegExp(mesaj));
  }

  await cikisYap(page);
  await girisFormunuDoldur(page, ad, ESKI);
  await expect(page).toHaveURL(appUrl("/dashboard"));
});
