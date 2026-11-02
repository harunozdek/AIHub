import { expect, test, type Page } from "@playwright/test";
import { appUrl, cikisYap, girisFormunuDoldur, girisYap, kullaniciEkleUi, sql, sqlInBackground, yeniKullaniciAdi } from "./seed";

// Spec 0004 — AC-7, AC-8, AC-11, R-11: kullanıcı ekleme, pasifleştirme ve kullanıcılar listesi.

const SIFRE = "baslangic-sifresi-123";

/** "Durum" sütunu; satır metni "Pasifleştir" düğmesini de içerdiği için durum yalnızca bu hücreden okunur. */
function durum(page: Page, ad: string) {
  return satir(page, ad).getByRole("cell").nth(2);
}

function satir(page: Page, ad: string) {
  return page.getByRole("row").filter({ has: page.getByRole("cell", { name: ad, exact: true }) });
}

async function girisDene(page: Page, ad: string, sifre: string) {
  await page.goto(appUrl("/giris"));
  await girisFormunuDoldur(page, ad, sifre);
}

test("AC-7 eklenen kullanıcı listede ekleyenle görünür ve başlangıç şifresiyle giriş yapar; AC-11 liste alanları", async ({ page }) => {
  const ad = yeniKullaniciAdi();
  await girisYap(page, "/kullanicilar");

  await kullaniciEkleUi(page, ad, SIFRE, "Yeni Kişi");

  for (const baslik of ["Kullanıcı adı", "Görünen ad", "Durum", "Son giriş", "Ekleyen", "Eklenme"]) {
    await expect(page.getByRole("columnheader", { name: baslik })).toBeVisible();
  }
  const yeni = satir(page, ad);
  await expect(yeni).toContainText("Yeni Kişi");
  await expect(durum(page, ad)).toHaveText("Aktif");
  await expect(yeni).toContainText("E2E Kullanıcısı");
  await expect(yeni.getByRole("cell").nth(3)).toHaveText("—");
  await expect(satir(page, "e2e-yonetici")).toContainText("Siz");

  await cikisYap(page);
  await girisDene(page, ad, SIFRE);
  await expect(page).toHaveURL(appUrl("/logs"));
});

test("AC-7 kurala uymayan alanlar ve alınmış kullanıcı adı alan yanında mesajla reddedilir", async ({ page }) => {
  const alinmis = yeniKullaniciAdi();
  await girisYap(page, "/kullanicilar");
  await kullaniciEkleUi(page, alinmis, SIFRE);
  const form = page.getByRole("form", { name: "Kullanıcı ekle" });

  for (const [ad, sifre, alan, mesaj] of [
    ["ab", SIFRE, "Kullanıcı adı", "3–64 karakter"],
    ["ayşe-yilmaz", SIFRE, "Kullanıcı adı", "yalnızca harf"],
    [yeniKullaniciAdi(), "kisa-sifre", "Başlangıç şifresi", "12–128 karakter"],
    [alinmis.toUpperCase(), SIFRE, "Kullanıcı adı", "zaten kullanılıyor"],
  ] as const) {
    await form.getByLabel("Kullanıcı adı").fill(ad);
    await form.getByLabel("Görünen ad").fill("x");
    await form.getByLabel("Başlangıç şifresi").fill(sifre);
    await form.getByRole("button", { name: "Kullanıcı ekle" }).click();

    const alanOgesi = form.getByLabel(alan);
    await expect(alanOgesi).toHaveAttribute("aria-invalid", "true");
    await expect(alanOgesi).toHaveAccessibleDescription(new RegExp(mesaj));
  }
  await expect(page.getByRole("cell", { name: "ab", exact: true })).toHaveCount(0);
});

test("AC-8 pasifleştirilen kullanıcı giriş yapamaz, açık oturumu sonraki işlemde biter, etkinleştirilince giriş yapar", async ({ page, browser }) => {
  const ad = yeniKullaniciAdi();
  await girisYap(page, "/kullanicilar");
  await kullaniciEkleUi(page, ad, SIFRE);

  const digerBaglam = await browser.newContext();
  const diger = await digerBaglam.newPage();
  await girisDene(diger, ad, SIFRE);
  await expect(diger).toHaveURL(appUrl("/logs"));

  await satir(page, ad).getByRole("button", { name: `${ad}: Pasifleştir` }).click();
  await satir(page, ad).getByRole("button", { name: "Evet, pasifleştir" }).click();
  // R-11 (review 1, F-03): durum hücresi pasifleştireni ve zamanını da gösterir.
  await expect(durum(page, ad)).toHaveText(/^Pasif\s*E2E Kullanıcısı, \S+/);

  await diger.goto(appUrl("/logs"));
  await expect(diger).toHaveURL((url) => url.pathname === "/giris");
  await girisFormunuDoldur(diger, ad, SIFRE);
  await expect(diger.getByRole("form", { name: "Giriş" }).getByRole("alert")).toHaveText("Kullanıcı adı veya şifre hatalı.");

  await satir(page, ad).getByRole("button", { name: `${ad}: Etkinleştir` }).click();
  await satir(page, ad).getByRole("button", { name: "Evet, etkinleştir" }).click();
  await expect(durum(page, ad)).toHaveText("Aktif");
  await girisDene(diger, ad, SIFRE);
  await expect(diger).toHaveURL(appUrl("/logs"));
  await digerBaglam.close();
});

test("AC-8 pasifleştirme onayından vazgeçilebilir; kullanıcı kendi satırında pasifleştirme görmez", async ({ page }) => {
  const ad = yeniKullaniciAdi();
  await girisYap(page, "/kullanicilar");
  await kullaniciEkleUi(page, ad, SIFRE);

  await satir(page, ad).getByRole("button", { name: `${ad}: Pasifleştir` }).click();
  await satir(page, ad).getByRole("button", { name: "Vazgeç" }).click();

  await expect(durum(page, ad)).toHaveText("Aktif");
  await expect(satir(page, "e2e-yonetici").getByRole("button")).toHaveCount(0);
});

test("AC-11 veri gelene kadar kullanıcılar ekranında yükleniyor durumu görünür", async ({ page }) => {
  await girisYap(page, "/kullanicilar");

  // Tablo birkaç saniye kilitlenir: oturum doğrulaması ve liste sorgusu bekler.
  const kilit = sqlInBackground("BEGIN; LOCK TABLE users IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(3); COMMIT;");
  await expect
    .poll(() => sql("SELECT count(*) FROM pg_locks l JOIN pg_class c ON c.oid = l.relation WHERE c.relname = 'users' AND l.mode = 'AccessExclusiveLock' AND l.granted"))
    .toBe("1");

  await page.goto(appUrl("/kullanicilar"), { waitUntil: "commit" });
  await expect(page.getByText("Kullanıcılar yükleniyor…")).toBeVisible();
  await kilit;
  await expect(page.getByRole("table")).toBeVisible();
});
