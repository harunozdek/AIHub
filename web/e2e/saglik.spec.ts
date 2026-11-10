import { execFileSync } from "node:child_process";
import { expect, test, type Page } from "@playwright/test";
import { appUrl, DOCKER_CLI_TIMEOUT_MS, girisYap, izlemeleriTemizle, sql } from "./seed";

// Spec 0008 — panelin sağlık adresi (AC-3, AC-4) ve AIHub'ın kendi kayıtları (AC-5, AC-9).

const IC_AYRINTILAR = ["API_BASE_URL", "SESSION_SECRET", "localhost", "Error", "   at ", "next"];

test("0008 AC-3 AC-4 panel sağlık adresi girişsiz Healthy döner; backend yokken de; ayrıntı ve önbellek yok", async ({ request }) => {
  for (const kok of [appUrl("/"), process.env.E2E_AYARSIZ_URL!]) {
    const yanit = await request.get(new URL("/health", kok).toString(), { maxRedirects: 0 });
    expect(yanit.status(), kok).toBe(200);
    const govde = await yanit.text();
    expect(govde).toBe("Healthy");
    for (const ic of IC_AYRINTILAR) expect(govde).not.toContain(ic);
    expect(yanit.headers()["cache-control"]).toContain("no-store");
  }
});

test("0008 AC-3 panelin diğer adresleri girişsiz istekte girişe yönlendirmeye devam eder", async ({ request }) => {
  for (const yol of ["/dashboard", "/health/x", "/healthz", "/logs"]) {
    const yanit = await request.get(appUrl(yol), { maxRedirects: 0 });
    expect(yanit.status(), yol).toBe(307);
    expect(new URL(yanit.headers()["location"], appUrl("/")).pathname, yol).toBe("/giris");
  }
});

/**
 * AIHub'ın kendi iki kaydı, gerçek sağlık adresleriyle. Açılışta eklenmeleri (AC-5…AC-8) Api testlerinde gerçek
 * veritabanıyla kanıtlanır; e2e backend'i kendi kaydını açılışta eklemez, çünkü diğer e2e testleri listeyi boş
 * varsayar ve her test sonunda temizler (plan 0008, amendment). Burada kayıtlar açılıştaki gibi "Sistem"
 * kimliğiyle eklenir ve uçtan uca davranış sınanır.
 */
async function kendiKayitlariniEkle(): Promise<void> {
  await izlemeleriTemizle();
  for (const [ad, adres] of [["AIHub.Api", `${process.env.E2E_API_URL}/health`], ["AIHub.Panel", appUrl("/health")]]) {
    await sql(`INSERT INTO monitored_apps (id, service_name, health_url, status, consecutive_failures, next_check_at, created_at, created_by)
      VALUES (gen_random_uuid(), '${ad}', '${adres}', 'Unknown', 0, now(), now(), '00000000-0000-0000-0000-000000000000')`);
  }
}

function durum(page: Page, ad: string) {
  return page.getByRole("row").filter({ hasText: ad }).getByRole("cell").nth(2);
}

async function durumBekle(page: Page, ad: string, beklenen: string): Promise<void> {
  await expect
    .poll(async () => {
      await page.reload();
      return durum(page, ad).textContent();
    }, { timeout: 30_000 })
    .toBe(beklenen);
}

test.describe("0008 AIHub'ın kendi kayıtları", () => {
  test.afterEach(izlemeleriTemizle);

  test("AC-5 AC-9 kendi kayıtları Sistem tarafından eklenmiş görünür ve Ayakta olur; Redis erişilemezken AIHub.Api Down olur, düzelince Ayakta", async ({ page }) => {
    await kendiKayitlariniEkle();
    await girisYap(page, "/izlenen-uygulamalar");

    for (const ad of ["AIHub.Api", "AIHub.Panel"]) {
      await durumBekle(page, ad, "Ayakta");
      await expect(page.getByRole("row").filter({ hasText: ad })).toContainText("Sistem");
    }

    execFileSync("docker", ["pause", process.env.E2E_REDIS_CONTAINER!], { timeout: DOCKER_CLI_TIMEOUT_MS });
    try {
      await durumBekle(page, "AIHub.Api", "Down");
      await expect(durum(page, "AIHub.Panel")).toHaveText("Ayakta");
      await page.goto(appUrl("/dashboard"));
      await expect(page.getByRole("alert").filter({ hasText: "Down" })).toContainText("AIHub.Api");
    } finally {
      execFileSync("docker", ["unpause", process.env.E2E_REDIS_CONTAINER!], { timeout: DOCKER_CLI_TIMEOUT_MS });
    }

    await page.goto(appUrl("/izlenen-uygulamalar"));
    await durumBekle(page, "AIHub.Api", "Ayakta");
  });
});
