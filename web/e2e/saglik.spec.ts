import { expect, test } from "@playwright/test";
import { appUrl } from "./seed";

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
