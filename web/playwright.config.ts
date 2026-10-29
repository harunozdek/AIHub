import { defineConfig, devices } from "@playwright/test";

/**
 * e2e (docs/testing.md): tek worker, headless chromium. Uygulamanın ve container'ların yaşam döngüsü
 * yalnızca globalSetup/globalTeardown'dadır. Adresler setup'ta boş portlardan seçilir ve testlere
 * ortam değişkeniyle geçer.
 *
 * Projeler sırayla koşar: "bos-depo" henüz hiç kayıt yokken, "ariza" en sonda (PostgreSQL'i durdurur).
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  forbidOnly: true,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [["list"]],
  globalSetup: "./e2e/global-setup.ts",
  globalTeardown: "./e2e/global-teardown.ts",
  use: { ...devices["Desktop Chrome"], headless: true, locale: "tr-TR", timezoneId: "Europe/Istanbul" },
  projects: [
    { name: "bos-depo", grep: /@bos-depo/ },
    { name: "panel", grepInvert: /@bos-depo|@ariza/, dependencies: ["bos-depo"] },
    { name: "ariza", grep: /@ariza/, dependencies: ["panel"] },
  ],
});
