import { readFileSync } from "node:fs";
import { expect, test } from "@playwright/test";
import { E2E_KULLANICI } from "./kimlik";

// Spec 0004 ile korunan 0003/CB-1, CB-2 ve 0002/R-9: giriş yapılamazsa kullanıcı 500 değil genel bir hata
// görür; nedeni yalnızca sunucu loguna yazılır, şifre hiçbir log satırına girmez (plan 0004, test eşlemesi).

/** Log dosyası tüm koşu boyunca paylaşılır: yalnızca bu andan sonra eklenenler okunur (0003, R1-02). */
function buAndanSonrakiLog(): () => string {
  const dosya = process.env.E2E_AYARSIZ_LOG!;
  const baslangic = readFileSync(dosya, "utf8").length;
  return () => readFileSync(dosya, "utf8").slice(baslangic);
}

test("backend'e ulaşılamazsa giriş 500 vermez, genel hata gösterir, eksik ayar loglanır, şifre yazılmaz ve ayrıntı gösterilmez", async ({ page }) => {
  const sifre = `gizli-sifre-${Date.now()}`;
  const yeniLog = buAndanSonrakiLog();

  const yanit = await page.goto(new URL("/giris?returnTo=/logs", process.env.E2E_AYARSIZ_URL).href);
  expect(yanit?.status()).toBe(200);
  await page.getByLabel("Kullanıcı adı").fill(E2E_KULLANICI);
  await page.getByLabel("Şifre").fill(sifre);
  await page.getByRole("button", { name: "Giriş yap" }).click();

  await expect(page.getByRole("form", { name: "Giriş" }).getByRole("alert")).toHaveText("Giriş şu anda yapılamıyor. Lütfen biraz sonra yeniden deneyin.");
  await expect(page).toHaveURL((url) => url.pathname === "/giris");
  await expect.poll(yeniLog).toMatch(/Giriş hatası \(giris\): Error: API_BASE_URL tanımlı değil\./);
  expect(yeniLog()).not.toContain(sifre);
  for (const ayrinti of ["API_BASE_URL", "tanımlı değil", "Error"]) {
    await expect(page.locator("body")).not.toContainText(ayrinti);
  }
});
