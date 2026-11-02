import { expect, test } from "@playwright/test";
import { appUrl, girisFormunuDoldur, girisYap, kayitEkle, saatOnce, suresiDolmusOturum, yeniServisAdi } from "./seed";

// Spec 0002 — AC-1, AC-2: giriş zorunluluğu, süresi dolan oturum, iptal edilen giriş, çıkış.

test("AC-1 oturumsuz kullanıcı giriş akışına yönlendirilir ve log içeriği görmez", async ({ page }) => {
  const servis = yeniServisAdi();
  const isaret = `gizli-${servis}`;
  const id = await kayitEkle({ servis, alinma: saatOnce(1), message: isaret });

  for (const adres of [`/logs?service=${servis}`, `/logs/${id}`, "/"]) {
    await page.goto(appUrl(adres));

    await expect(page).toHaveURL((url) => url.origin === process.env.E2E_OIDC_ORIGIN);
    await expect(page.locator('input[name="username"]')).toBeVisible();
    await expect(page.locator("body")).not.toContainText(isaret);
  }
});

test("AC-2 süresi dolmuş oturumda yeniden giriş istenir ve aynı sayfaya aynı filtrelerle dönülür", async ({ page, context }) => {
  const servis = yeniServisAdi();
  const isaret = `kayit-${servis}`;
  await kayitEkle({ servis, alinma: saatOnce(1), level: "Error", message: isaret });
  await context.addCookies([{ name: "aihub_session", value: await suresiDolmusOturum(), url: appUrl("/") }]);
  const hedef = `/logs?service=${servis}&level=Error`;

  await page.goto(appUrl(hedef));
  await girisFormunuDoldur(page);

  await expect(page).toHaveURL(appUrl(hedef));
  await expect(page.getByRole("table")).toContainText(isaret);
  await expect(page.getByLabel("Servis")).toHaveValue(servis);
  await expect(page.getByRole("checkbox", { name: "Error" })).toBeChecked();
});

test("AC-2 iptal edilen giriş hata sayfası ve yeniden giriş seçeneği gösterir", async ({ page }) => {
  await page.goto(appUrl("/auth/giris?returnTo=/logs"));
  await expect(page).toHaveURL((url) => url.origin === process.env.E2E_OIDC_ORIGIN);
  // Kullanıcı sağlayıcıda girişi iptal eder: sağlayıcı geri dönüş adresine `error=access_denied` ile yönlendirir.
  const state = new URL(page.url()).searchParams.get("state")!;
  await page.goto(appUrl(`/auth/geri-donus?error=access_denied&state=${encodeURIComponent(state)}`));

  await expect(page.getByRole("heading", { name: "Giriş iptal edildi" })).toBeVisible();
  await page.getByRole("link", { name: "Yeniden giriş yap" }).click();
  await expect(page.locator('input[name="username"]')).toBeVisible();
});

test("AC-2 başarısız giriş hata sayfası gösterir ve oturum açmaz", async ({ page }) => {
  await page.goto(appUrl("/auth/giris?returnTo=/logs"));
  await expect(page).toHaveURL((url) => url.origin === process.env.E2E_OIDC_ORIGIN);
  await page.goto(appUrl("/auth/geri-donus?code=gecersiz-kod&state=yanlis-state"));

  await expect(page.getByRole("heading", { name: "Giriş yapılamadı" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Yeniden giriş yap" })).toBeVisible();
  await page.goto(appUrl("/logs"));
  await expect(page).toHaveURL((url) => url.origin === process.env.E2E_OIDC_ORIGIN);
});

test("AC-2 çıkıştan sonra geri tuşu log içeriği göstermez", async ({ page }) => {
  const servis = yeniServisAdi();
  const isaret = `kayit-${servis}`;
  await kayitEkle({ servis, alinma: saatOnce(1), message: isaret });
  await girisYap(page, `/logs?service=${servis}`);
  await expect(page.getByRole("table")).toContainText(isaret);

  await page.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(page.locator('input[name="username"]')).toBeVisible();
  await page.goBack();

  await page.waitForLoadState();
  await expect(page.locator("body")).not.toContainText(isaret);
  await expect(page).not.toHaveURL(appUrl(`/logs?service=${servis}`));
});

// Review 1, F-01: dönüş adresi yalnızca panelin kendi adresi olabilir. URL ayrıştırıcısı tab ve satır
// sonlarını sildiği için "/\t/site" gibi değerler başka siteye çözülür; hepsi listeye döner.
// Review 2, R2-01: dot-segment'ler normalize edilince yol "//site" olur; bu da başka siteye çözülür.
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
]) {
  test(`AC-2 girişten sonra dış adrese yönlendirilmez: ${JSON.stringify(kotu)}`, async ({ page }) => {
    await page.goto(appUrl(`/auth/giris?returnTo=${encodeURIComponent(kotu)}`));
    await girisFormunuDoldur(page);

    await expect(page).toHaveURL(appUrl("/logs"));
  });
}
