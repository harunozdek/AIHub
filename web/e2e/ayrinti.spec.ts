import { expect, test } from "@playwright/test";
import { appUrl, girisYap, kayitEkle, saatOnce, yeniServisAdi } from "./seed";

// Spec 0002 — AC-7: ayrıntı sayfası, yerel saat ve saat dilimi, boş alanlar, filtreli listeye dönüş.

const istanbul = (tarih: Date) =>
  new Intl.DateTimeFormat("tr-TR", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    fractionalSecondDigits: 3,
    timeZoneName: "short",
    timeZone: "Europe/Istanbul",
  }).format(tarih);

test("AC-7 ayrıntı tüm alanları gösterir, geri dönüşte liste filtreleri korunur", async ({ page }) => {
  const servis = yeniServisAdi();
  const alinma = new Date(Math.floor(saatOnce(1).getTime() / 1000) * 1000 + 123);
  const kaynak = new Date(alinma.getTime() - 61_000);
  const id = kayitEkle({
    servis,
    alinma,
    level: "Critical",
    message: "Tahsilat başarısız\nikinci satır",
    exceptionType: "System.InvalidOperationException",
    stackTrace: "at Odeme.Tahsil()\n   at Program.Main()",
    metadata: { siparisNo: 42, kanal: { ad: "web" } },
    kaynakZamani: kaynak,
  });
  const liste = `/logs?service=${servis}&level=Critical`;
  await girisYap(page, liste);

  await page.locator("tbody tr").first().getByRole("link").click();

  await expect(page).toHaveURL(appUrl(`/logs/${id}?donus=${encodeURIComponent(liste)}`));
  const alan = (etiket: string) => page.locator("dt").filter({ hasText: new RegExp(`^${etiket}$`) }).locator("xpath=following-sibling::dd[1]");
  await expect(alan("Kayıt kimliği")).toHaveText(id);
  await expect(alan("Servis")).toHaveText(servis);
  await expect(alan("Seviye")).toHaveText("Critical");
  await expect(alan("Mesaj").locator("pre")).toHaveText("Tahsilat başarısız\nikinci satır");
  await expect(alan("Exception türü")).toHaveText("System.InvalidOperationException");
  await expect(alan("Yığın izi").locator("pre")).toHaveText("at Odeme.Tahsil()\n   at Program.Main()");
  // jsonb anahtar sırasını kendi kuralıyla saklar; içerik ve girinti doğrulanır, sıra değil.
  const metadata = await alan("Metadata").locator("pre").innerText();
  expect(JSON.parse(metadata)).toEqual({ siparisNo: 42, kanal: { ad: "web" } });
  expect(metadata).toContain('\n  "siparisNo": 42');
  expect(metadata).toContain('\n    "ad": "web"');
  await expect(alan("Alınma zamanı")).toHaveText(istanbul(alinma));
  await expect(alan("Servis zamanı")).toHaveText(istanbul(kaynak));
  await expect(alan("Alınma zamanı")).toContainText("GMT+3");

  await page.getByRole("link", { name: "← Listeye dön" }).click();
  await expect(page).toHaveURL(appUrl(liste));
  await expect(page.getByLabel("Servis")).toHaveValue(servis);
  await expect(page.getByRole("checkbox", { name: "Critical" })).toBeChecked();
  await expect(page.locator("tbody tr")).toHaveCount(1);
});

test("AC-7 boş isteğe bağlı alanlar '—' olarak görünür ve ayrıntı adresi paylaşılabilir", async ({ page }) => {
  const id = kayitEkle({ servis: yeniServisAdi(), alinma: saatOnce(1) });

  await girisYap(page, `/logs/${id}`);

  for (const etiket of ["Servis zamanı", "Exception türü", "Yığın izi", "Metadata"]) {
    await expect(page.locator("dt", { hasText: etiket }).locator("xpath=following-sibling::dd[1]")).toHaveText("—");
  }
});
