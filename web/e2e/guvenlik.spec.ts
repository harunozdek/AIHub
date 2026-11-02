import { expect, test } from "@playwright/test";
import { girisYap, kayitEkle, saatOnce, yeniServisAdi } from "./seed";

// Spec 0002 — AC-8: log içeriği saldırganın metnidir; her yerde düz metin olarak görünür.

const betik = `<script>window.__xss = "script"</script>`;
const resim = `<img src=x onerror="window.__xss = 'img'">`;
const baglanti = `<a href="javascript:window.__xss='link'">tıkla</a>`;

test("AC-8 betik ve HTML içeren kayıt listede ve ayrıntıda düz metin görünür, hiçbir betik çalışmaz", async ({ page }) => {
  const servis = yeniServisAdi();
  const id = await kayitEkle({
    servis,
    alinma: saatOnce(1),
    message: `${betik} ${resim} ${baglanti}`,
    exceptionType: resim,
    stackTrace: `${betik}\n${baglanti}`,
    metadata: { html: betik, link: "javascript:alert(1)", img: resim },
  });
  let diyalog = false;
  page.on("dialog", async (d) => {
    diyalog = true;
    await d.dismiss();
  });

  await girisYap(page, `/logs?service=${servis}`);
  const satir = page.locator("tbody tr").first();
  await expect(satir).toContainText(betik);
  await expect(satir).toContainText(resim);
  await yeniOgeYok();

  await satir.getByRole("link").click();
  await expect(page).toHaveURL(new RegExp(`/logs/${id}`));
  await expect(page.locator("dd").filter({ hasText: "tıkla" }).first()).toBeVisible();
  await expect(page.locator("main")).toContainText(`${betik} ${resim} ${baglanti}`);
  await expect(page.locator("main")).toContainText('"link": "javascript:alert(1)"');
  await yeniOgeYok();

  async function yeniOgeYok() {
    expect(await page.evaluate(() => (window as unknown as { __xss?: string }).__xss)).toBeUndefined();
    expect(diyalog).toBe(false);
    await expect(page.locator('main img, main a[href^="javascript:"], main script')).toHaveCount(0);
  }
});
