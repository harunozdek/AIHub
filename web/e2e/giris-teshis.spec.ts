import { randomUUID } from "node:crypto";
import { readFileSync } from "node:fs";
import { expect, test } from "@playwright/test";
import { appUrl } from "./seed";

// Spec 0003 — CB-1, CB-2, PB-2: giriş hatalarının nedeni sunucu loguna yazılır, kullanıcıya gösterilmez.

type LogDegiskeni = "E2E_WEB_LOG" | "E2E_AYARSIZ_LOG";

function log(degisken: LogDegiskeni): string {
  return readFileSync(process.env[degisken]!, "utf8");
}

/**
 * Log dosyası tüm koşu boyunca paylaşılır: yalnızca bu andan sonra eklenenler okunur, başka bir testin
 * satırı bu testin beklentisini karşılamaz (review 1, R1-02).
 */
function buAndanSonrakiLog(degisken: LogDegiskeni): () => string {
  const baslangic = log(degisken).length;
  return () => log(degisken).slice(baslangic);
}

test("CB-1 başarısız geri dönüşün nedeni sunucu loguna yazılır, kod ve state yazılmaz", async ({ page }) => {
  const kod = `sahte-kod-${randomUUID()}`;
  const state = `sahte-state-${randomUUID()}`;
  await page.goto(appUrl("/auth/giris?returnTo=/logs"));
  await expect(page).toHaveURL((url) => url.origin === process.env.E2E_OIDC_ORIGIN);
  const yeniLog = buAndanSonrakiLog("E2E_WEB_LOG");

  await page.goto(appUrl(`/auth/geri-donus?code=${kod}&state=${state}`));

  await expect(page.getByRole("heading", { name: "Giriş yapılamadı" })).toBeVisible();
  // Akış çerezi varken doğrulama openid-client'ta başarısız olur: catch dalı, hata kodu ve asıl neden
  // (R1-03). Sınıf adı production derlemesinde küçültülür (ör. "e0"), bu yüzden beklenmez.
  await expect.poll(yeniLog).toMatch(/Giriş hatası \(geri-donus\): \S+: .+ \[OAUTH_[A-Z_]+\] — neden: .+/);
  expect(yeniLog()).not.toContain(kod);
  expect(yeniLog()).not.toContain(state);
});

test("CB-1 giriş akışı çerezi yokken geri dönüşün nedeni loglanır", async ({ page }) => {
  const state = `cerezsiz-${randomUUID()}`;
  const yeniLog = buAndanSonrakiLog("E2E_WEB_LOG");

  await page.goto(appUrl(`/auth/geri-donus?code=x&state=${state}`));

  await expect(page.getByRole("heading", { name: "Giriş yapılamadı" })).toBeVisible();
  await expect.poll(yeniLog).toContain("Giriş hatası (geri-donus): giriş akışı çerezi yok");
  expect(yeniLog()).not.toContain(state);
});

test("CB-2 ayarlar eksikken giriş 500 vermez, hata sayfası açılır, eksik değişken loglanır; PB-2 ayrıntı gösterilmez", async ({ page }) => {
  const yeniLog = buAndanSonrakiLog("E2E_AYARSIZ_LOG");

  const yanit = await page.goto(new URL("/auth/giris?returnTo=/logs", process.env.E2E_AYARSIZ_URL).href);

  expect(yanit?.status()).toBe(200);
  await expect(page).toHaveURL((url) => url.pathname === "/giris-hatasi" && url.searchParams.get("neden") === "hata");
  await expect(page.getByRole("heading", { name: "Giriş yapılamadı" })).toBeVisible();
  await expect.poll(yeniLog).toMatch(/Giriş hatası \(giris\): Error: (APP_URL|OIDC_ISSUER) tanımlı değil/);
  for (const ayrinti of ["APP_URL", "OIDC_ISSUER", "tanımlı değil", "Error"]) {
    await expect(page.locator("body")).not.toContainText(ayrinti);
  }
});
