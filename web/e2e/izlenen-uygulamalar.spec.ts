import { expect, test, type Locator, type Page } from "@playwright/test";
import {
  appUrl,
  ayaktaSatiri,
  downKarti,
  durumuBekle,
  girisYap,
  izlemeBolumu,
  izlemeleriTemizle,
  saglikAdresi,
  saglikAyarla,
  uygulamaDurumu,
  uygulamaEkleUi,
  uygulamaOgesi,
  yeniServisAdi,
} from "./seed";

/** Ekranın yoklama aralığı (Spec 0012, CB-6; `uygulama-listesi.tsx`'teki `YOKLAMA_MS`). */
const YOKLAMA_MS = 10_000;

/** Sayfanın `ms` boyunca harcadığı betik süresi (sn). Boştaki ekranda yeniden çizim döngüsü olmadığını gösterir. */
async function betikSuresi(page: Page, ms: number): Promise<number> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send("Performance.enable");
  const olc = async () => (await cdp.send("Performance.getMetrics")).metrics.find((m) => m.name === "ScriptDuration")!.value;
  const once = await olc();
  await page.waitForTimeout(ms);
  const sonra = await olc();
  await cdp.detach();
  return sonra - once;
}

// Spec 0005 — AC-1…AC-5, AC-9, AC-10: "İzlenen uygulamalar" ekranı; Spec 0012 — iki bölüm (Ayakta, Down), durum
// simgeleri, 10 sn'lik canlı güncelleme, ekleyen ve son değiştiren gösterilmez. Denetim aralığı e2e'de 1 sn
// (global-setup); ekranın yoklaması gerçek 10 sn'dir.

test.afterEach(izlemeleriTemizle);

/** Ayakta satırının sütunları: 0 durum, 1 ServiceName, 2 adres, 3 son denetim, 4 işlem. */
function hucre(page: Page, ad: string, sutun: number) {
  return ayaktaSatiri(page, ad).getByRole("cell").nth(sutun);
}

/** Down kartındaki bir alanın değeri (ör. "Son başarısızlık"). */
function downAlani(page: Page, ad: string, alan: string) {
  return downKarti(page, ad).locator("dt", { hasText: alan }).locator("xpath=following-sibling::dd[1]");
}

/** Bölümlerde ekleyen ve son değiştiren yazmaz (Spec 0012, CB-3; 0008 R-6 dahil). */
async function ekleyenYok(page: Page) {
  for (const bolum of [izlemeBolumu(page, "Ayakta"), izlemeBolumu(page, "Down")]) {
    await expect(bolum).not.toContainText("E2E Kullanıcısı");
    await expect(bolum).not.toContainText("Sistem");
    await expect(bolum).not.toContainText("Ekleyen");
    await expect(bolum).not.toContainText("Son değiştiren");
  }
}

test("AC-1 oturumsuz kullanıcı izlenen uygulamalar ekranı yerine giriş ekranına gider; durum ucu veri vermez", async ({ page }) => {
  await page.goto(appUrl("/izlenen-uygulamalar"));
  await expect(page).toHaveURL((url) => url.pathname === "/giris");

  // Spec 0012, PB-4: ekranın yokladığı uç da oturumsuz veri vermez (giriş ekranına yönlendirir).
  const yanit = await page.request.get(appUrl("/durum/uygulamalar"), { maxRedirects: 0 });
  expect([302, 303, 307, 401]).toContain(yanit.status());
  const govde = await yanit.text();
  expect(govde).not.toContain("serviceName");
  expect(govde).not.toContain("\"uygulamalar\"");
});

test("AC-2 eklenen uygulama Bilinmiyor durumunda Ayakta bölümünde görünür, ekleyen yazmaz; aynı ServiceName alan yanında reddedilir", async ({ page }) => {
  const ad = yeniServisAdi();
  // İlk denetimler zaman aşımına düşer: üç başarısızlığa kadar durum Bilinmiyor kalır.
  await saglikAyarla(`/${ad}`, 200, 5_000);
  await girisYap(page, "/izlenen-uygulamalar");

  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));

  const ayakta = izlemeBolumu(page, "Ayakta");
  for (const baslik of ["Durum", "ServiceName", "Sağlık adresi", "Son denetim"]) {
    await expect(ayakta.getByRole("columnheader", { name: baslik })).toBeVisible();
  }
  await expect(hucre(page, ad, 0)).toHaveText("Bilinmiyor");
  await ekleyenYok(page);

  const form = page.getByRole("form", { name: "Uygulama ekle" });
  await form.getByLabel("ServiceName").fill(ad);
  await form.getByLabel("Sağlık adresi").fill(saglikAdresi("/baska"));
  await form.getByRole("button", { name: "Uygulama ekle" }).click();
  await expect(form.getByLabel("ServiceName")).toHaveAttribute("aria-invalid", "true");
  await expect(form.getByLabel("ServiceName")).toHaveAccessibleDescription(/zaten izleniyor/);
  await expect(uygulamaOgesi(page, ad)).toHaveCount(1);
});

test("AC-3 kural dışı adres ve boş ServiceName alan yanında mesajla reddedilir", async ({ page }) => {
  await girisYap(page, "/izlenen-uygulamalar");
  const form = page.getByRole("form", { name: "Uygulama ekle" });

  for (const [ad, adres, alan, mesaj] of [
    [yeniServisAdi(), "ftp://odeme.kurum.local/health", "Sağlık adresi", "http:// ya da https://"],
    [yeniServisAdi(), "/health", "Sağlık adresi", "http:// ya da https://"],
    [yeniServisAdi(), "http://kullanici:sifre@odeme.kurum.local/health", "Sağlık adresi", "kullanıcı adı veya şifre"],
    [yeniServisAdi(), "http://169.254.169.254/latest/meta-data", "Sağlık adresi", "engellenen"],
    ["   ", saglikAdresi("/bos-ad"), "ServiceName", "boş olamaz"],
  ] as const) {
    // Önceki gönderim bitmeden doldurulan alanları, o gönderimin sonundaki form sıfırlaması siler.
    await expect(form.getByRole("button", { name: "Uygulama ekle" })).toBeEnabled();
    await form.getByLabel("ServiceName").fill(ad);
    await form.getByLabel("Sağlık adresi").fill(adres);
    await form.getByRole("button", { name: "Uygulama ekle" }).click();

    const alanOgesi = form.getByLabel(alan);
    await expect(alanOgesi).toHaveAttribute("aria-invalid", "true");
    await expect(alanOgesi).toHaveAccessibleDescription(new RegExp(mesaj));
  }
  await expect(page.getByText("Henüz izlenen uygulama yok")).toBeVisible();
});

test("AC-4 AC-9 Ayakta olan uygulamanın adresi değişince Bilinmiyor olur; değiştiren yazmaz; çıkarma onay ister", async ({ page }) => {
  const ad = yeniServisAdi();
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, ad, "Ayakta");
  await expect(hucre(page, ad, 3)).not.toHaveText("—");

  await saglikAyarla(`/${ad}-yeni`, 200, 5_000);
  await ayaktaSatiri(page, ad).getByRole("button", { name: `${ad}: Düzenle` }).click();
  const duzenle = page.getByRole("form", { name: `${ad}: sağlık adresini düzenle` });
  await duzenle.getByLabel("Sağlık adresi").fill(saglikAdresi(`/${ad}-yeni`));
  await duzenle.getByRole("button", { name: "Kaydet" }).click();

  // Ekleme, düzenleme ve çıkarma listeye hemen yansır (Spec 0012, E-5): 10 sn'lik yoklama beklenmez.
  await expect(hucre(page, ad, 2)).toHaveText(saglikAdresi(`/${ad}-yeni`), { timeout: 5_000 });
  await expect(hucre(page, ad, 0)).toHaveText("Bilinmiyor");
  await ekleyenYok(page);

  await ayaktaSatiri(page, ad).getByRole("button", { name: `${ad}: İzlemeden çıkar` }).click();
  await ayaktaSatiri(page, ad).getByRole("button", { name: "Vazgeç" }).click();
  await expect(ayaktaSatiri(page, ad)).toHaveCount(1);

  await ayaktaSatiri(page, ad).getByRole("button", { name: `${ad}: İzlemeden çıkar` }).click();
  await ayaktaSatiri(page, ad).getByRole("button", { name: "Evet, çıkar" }).click();
  await expect(uygulamaOgesi(page, ad)).toHaveCount(0, { timeout: 5_000 });
  await expect(page.getByText("Henüz izlenen uygulama yok")).toBeVisible();
});

// Review 5, N5-1: aynı adresle kaydetmek formu kapatır; durum değişmez (AC-9). Son değiştirenin değişmediği Api
// testlerinde kanıtlanır; ekranda gösterilmez (Spec 0012, CB-3).
test("AC-9 Down olan uygulama aynı adresle kaydedilince form kapanır, Down korunur", async ({ page }) => {
  const ad = yeniServisAdi();
  await saglikAyarla(`/${ad}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, ad, "Down");

  await downKarti(page, ad).getByRole("button", { name: `${ad}: Düzenle` }).click();
  const duzenle = page.getByRole("form", { name: `${ad}: sağlık adresini düzenle` });
  await duzenle.getByRole("button", { name: "Kaydet" }).click();

  await expect(duzenle).toHaveCount(0);
  await expect(downKarti(page, ad).getByRole("button", { name: `${ad}: Düzenle` })).toBeVisible();
  await expect(downKarti(page, ad)).toBeVisible();
});

// Review 1 F-2: kayıt uygulamayı başka bölüme taşısa da form kapanır (kapanma satırın yaşamına bağlı değildir).
test("AC-9 0012 CB-6 Down olan uygulama yeni adresle kaydedilince form kapanır, uygulama Ayakta bölümüne geçer", async ({ page }) => {
  const ad = yeniServisAdi();
  await saglikAyarla(`/${ad}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, ad, "Down");

  await downKarti(page, ad).getByRole("button", { name: `${ad}: Düzenle` }).click();
  const duzenle = page.getByRole("form", { name: `${ad}: sağlık adresini düzenle` });
  await duzenle.getByLabel("Sağlık adresi").fill(saglikAdresi(`/${ad}-yeni`));
  await duzenle.getByRole("button", { name: "Kaydet" }).click();

  await expect(ayaktaSatiri(page, ad)).toBeVisible();
  await expect(duzenle).toHaveCount(0);
  await expect(ayaktaSatiri(page, ad).getByRole("button", { name: `${ad}: Düzenle` })).toBeVisible();
});

test("AC-5 Spec 0012 CB-2 CB-3 Down olan uygulama Down bölümünde nedeni ve zamanıyla görünür; en uzun süredir Down olan üstte", async ({ page }) => {
  const ayakta = `a-${yeniServisAdi()}`;
  const ilkDown = `z-${yeniServisAdi()}`;
  const sonrakiDown = `b-${yeniServisAdi()}`;
  await saglikAyarla(`/${ilkDown}`, 503);
  await saglikAyarla(`/${sonrakiDown}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ayakta, saglikAdresi(`/${ayakta}`));
  await uygulamaEkleUi(page, ilkDown, saglikAdresi(`/${ilkDown}`));
  await durumuBekle(page, ilkDown, "Down");
  await uygulamaEkleUi(page, sonrakiDown, saglikAdresi(`/${sonrakiDown}`));
  await durumuBekle(page, sonrakiDown, "Down");
  await durumuBekle(page, ayakta, "Ayakta");

  // Down bölümünde en uzun süredir Down olan üstte (ad sırası tersi olsa da).
  const kartlar = izlemeBolumu(page, "Down").getByRole("listitem");
  await expect(kartlar.first()).toContainText(ilkDown);
  await expect(kartlar.nth(1)).toContainText(sonrakiDown);
  await expect(downAlani(page, ilkDown, "Son başarısızlık")).toHaveText("HTTP 503");
  await expect(downAlani(page, ilkDown, "Down olduğu zaman")).not.toHaveText("—");
  await expect(downAlani(page, ilkDown, "Son denetim")).not.toHaveText("—");
  await expect(downAlani(page, ilkDown, "Sağlık adresi")).toHaveText(saglikAdresi(`/${ilkDown}`));
  await expect(downKarti(page, ilkDown)).toHaveClass(/bg-red-50/);
  await expect(hucre(page, ayakta, 0)).toHaveText("Ayakta");
  await expect(ayaktaSatiri(page, ilkDown)).toHaveCount(0);
  await ekleyenYok(page);
});

// --- Spec 0012 ---------------------------------------------------------------------------------------------------

// CB-1: her durumun simgesi biçimiyle ayrılır; durum metni yanında yazar; simge ekran okuyucudan gizlidir.
// CB-2: bölüm başlıkları sayıları yazar; Ayakta bölümünde Bilinmiyor da vardır, ServiceName sırasıyla.
test("0012 CB-1 CB-2 durum simgeleri metinle birlikte; Bilinmiyor Ayakta bölümünde; başlıklar sayıları yazar", async ({ page }) => {
  const up = `a-${yeniServisAdi()}`;
  const bilinmiyor = `b-${yeniServisAdi()}`;
  const down = `c-${yeniServisAdi()}`;
  await saglikAyarla(`/${bilinmiyor}`, 200, 5_000);
  await saglikAyarla(`/${down}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  for (const ad of [up, down]) await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, up, "Ayakta");
  await durumuBekle(page, down, "Down");
  // Yanıt vermeyen adres üç zaman aşımından (e2e'de ~3 sn) sonra Down olur: Bilinmiyor durumu eklenir eklenmez okunur.
  await uygulamaEkleUi(page, bilinmiyor, saglikAdresi(`/${bilinmiyor}`));
  await expect(hucre(page, bilinmiyor, 0)).toHaveText("Bilinmiyor");

  for (const [ad, durum] of [[up, "Up"], [bilinmiyor, "Unknown"]] as const) {
    const simge = hucre(page, ad, 0).locator("svg");
    await expect(simge).toHaveAttribute("data-durum", durum);
    await expect(simge).toHaveAttribute("aria-hidden", "true");
  }
  const downSimge = downKarti(page, down).locator("svg");
  await expect(downSimge).toHaveAttribute("data-durum", "Down");
  await expect(downSimge).toHaveAttribute("aria-hidden", "true");
  await expect(downKarti(page, down)).toContainText("Down");
  // Biçimler farklıdır: simgelerin çizimleri birbirinin aynısı değildir.
  const cizimler = await Promise.all([
    hucre(page, up, 0).locator("svg").innerHTML(),
    hucre(page, bilinmiyor, 0).locator("svg").innerHTML(),
    downSimge.innerHTML(),
  ]);
  expect(new Set(cizimler).size).toBe(3);

  // Durum hücresi ekran okuyucuda yalnızca metindir.
  await expect(hucre(page, up, 0)).toHaveAccessibleName("Ayakta");

  const ayaktaSayisi = await izlemeBolumu(page, "Ayakta").getByRole("row").count() - 1;
  const downSayisi = await izlemeBolumu(page, "Down").getByRole("listitem").count();
  await expect(izlemeBolumu(page, "Ayakta").getByRole("heading")).toHaveText(`Ayakta (${ayaktaSayisi})`);
  await expect(izlemeBolumu(page, "Down").getByRole("heading")).toHaveText(`Down (${downSayisi})`);

  // Ayakta bölümü ServiceName sırasıyla.
  const adlar = await izlemeBolumu(page, "Ayakta").locator("tbody tr td:nth-child(2)").allTextContents();
  expect(adlar).toEqual([...adlar].sort());
  expect(adlar.indexOf(up)).toBeLessThan(adlar.indexOf(bilinmiyor));
});

// CB-2: geniş ekranda Ayakta 2/3 solda, Down 1/3 sağda; dar ekranda Down üstte, alt alta; bölümler ve içerik taşmaz
// (üst menünün dar ekranda taşması bu değişiklikten önce de vardı ve kapsam dışıdır: spec, BUILD sırasında R-10).
// CB-3: uzun adres bölümünden taşmaz.
test("0012 CB-2 CB-3 geniş ekranda 2/3 ve 1/3 yan yana; 320 px'te Down üstte, bölümler ve içerik taşmaz", async ({ page }) => {
  const up = yeniServisAdi();
  const down = yeniServisAdi();
  const uzunYol = `/${down}/${"uzun-yol-".repeat(30)}`;
  await saglikAyarla(uzunYol, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, up, saglikAdresi(`/${up}`));
  await uygulamaEkleUi(page, down, saglikAdresi(uzunYol));
  await durumuBekle(page, down, "Down");

  await page.setViewportSize({ width: 1280, height: 900 });
  const ayaktaKutu = (await izlemeBolumu(page, "Ayakta").boundingBox())!;
  const downKutu = (await izlemeBolumu(page, "Down").boundingBox())!;
  expect(ayaktaKutu.x).toBeLessThan(downKutu.x);
  expect(Math.abs(ayaktaKutu.y - downKutu.y)).toBeLessThan(2);
  expect(ayaktaKutu.width / downKutu.width).toBeGreaterThan(1.8);
  expect(ayaktaKutu.width / downKutu.width).toBeLessThan(2.3);

  await page.setViewportSize({ width: 320, height: 800 });
  const ayaktaDar = (await izlemeBolumu(page, "Ayakta").boundingBox())!;
  const downDar = (await izlemeBolumu(page, "Down").boundingBox())!;
  expect(downDar.y + downDar.height).toBeLessThanOrEqual(ayaktaDar.y);
  for (const bolum of ["Ayakta", "Down"] as const) {
    expect(await izlemeBolumu(page, bolum).evaluate((el) => el.scrollWidth - el.clientWidth), `${bolum} taşmaz`).toBeLessThanOrEqual(0);
  }
  // Review 1 F-4: dar ekranda Ayakta da karttır; tablo kendi kaydırma kutusunda 224 px kayıyordu. Kartın içi de
  // ölçülür; kaydırma kutusu bölüm ölçümünü sıfırlayabilir.
  await expect(izlemeBolumu(page, "Ayakta").getByRole("table")).toHaveCount(0);
  const ayaktaKarti = izlemeBolumu(page, "Ayakta").getByRole("listitem").filter({ has: page.getByText(up, { exact: true }) });
  await expect(ayaktaKarti.getByRole("button", { name: `${up}: Düzenle` })).toBeVisible();
  await expect(ayaktaKarti.locator("dt")).toHaveText(["Sağlık adresi", "Son denetim"]);
  for (const oge of [ayaktaKarti, ...(await izlemeBolumu(page, "Ayakta").locator("*").all())]) {
    expect(await oge.evaluate((el) => (el instanceof HTMLElement ? el.scrollWidth - el.clientWidth : 0)), "Ayakta bölümünde hiçbir öğe kaymaz").toBeLessThanOrEqual(0);
  }
  expect(await downKarti(page, down).evaluate((el) => el.scrollWidth - el.clientWidth), "uzun adres kartı taşırmaz").toBeLessThanOrEqual(0);
  expect(await page.locator("main").evaluate((el) => el.scrollWidth - el.clientWidth), "ekranın içeriği yatay taşmaz").toBeLessThanOrEqual(0);
});

// CB-4: her bölümün kendi boş durumu; hiç uygulama yokken bugünkü boş durum.
test("0012 CB-4 boş bölümler: Down olan uygulama yok, Ayakta uygulama yok", async ({ page }) => {
  const up = yeniServisAdi();
  await girisYap(page, "/izlenen-uygulamalar");
  await expect(page.getByText("Henüz izlenen uygulama yok")).toBeVisible();
  await expect(izlemeBolumu(page, "Ayakta")).toHaveCount(0);

  await uygulamaEkleUi(page, up, saglikAdresi(`/${up}`));
  await durumuBekle(page, up, "Ayakta");
  const downBos = izlemeBolumu(page, "Down").getByText("Down olan uygulama yok");
  await expect(downBos).toBeVisible();
  await expect(izlemeBolumu(page, "Down").locator("svg[data-durum=Up]")).toBeVisible();
  await expect(izlemeBolumu(page, "Down").getByRole("heading")).toHaveText("Down (0)");

  await saglikAyarla(`/${up}`, 503);
  await durumuBekle(page, up, "Down");
  await expect(izlemeBolumu(page, "Ayakta").getByText("Ayakta uygulama yok")).toBeVisible();
  await expect(izlemeBolumu(page, "Ayakta").getByRole("heading")).toHaveText("Ayakta (0)");
});

// CB-6: sayfa yenilenmeden bölüm değişimi; açık düzenleme formu, girilen değer ve odak, açık onay korunur.
// CB-7: Down'a giriş ve çıkış duyurulur; ilk açılışta duyuru yok.
test("0012 CB-6 CB-7 durum sayfa yenilenmeden güncellenir, açık form ve onay korunur, değişiklik duyurulur", async ({ page }) => {
  const duzenlenen = `a-${yeniServisAdi()}`;
  const onaylanan = `b-${yeniServisAdi()}`;
  const zatenDown = `c-${yeniServisAdi()}`;
  await saglikAyarla(`/${zatenDown}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  for (const ad of [duzenlenen, onaylanan, zatenDown]) await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, duzenlenen, "Ayakta");
  await durumuBekle(page, onaylanan, "Ayakta");
  await durumuBekle(page, zatenDown, "Down");

  // İlk açılışta mevcut durumlar duyurulmaz (E-4).
  await page.reload();
  const duyuru = page.locator("p.sr-only[aria-live=polite]");
  await expect(downKarti(page, zatenDown)).toBeVisible();
  await expect(duyuru).toHaveText("");
  await page.evaluate(() => ((window as unknown as { yenilenmedi: boolean }).yenilenmedi = true));

  // Düzenleme formu açık ve yazılmış; öteki uygulamanın çıkarma onayı açık.
  await ayaktaSatiri(page, onaylanan).getByRole("button", { name: `${onaylanan}: İzlemeden çıkar` }).click();
  await ayaktaSatiri(page, duzenlenen).getByRole("button", { name: `${duzenlenen}: Düzenle` }).click();
  const taslak = saglikAdresi(`/${duzenlenen}-taslak`);
  const alan = page.getByRole("form", { name: `${duzenlenen}: sağlık adresini düzenle` }).getByLabel("Sağlık adresi");
  await alan.fill(taslak);
  await expect(alan).toBeFocused();

  await saglikAyarla(`/${duzenlenen}`, 503);
  await saglikAyarla(`/${onaylanan}`, 503);
  await expect(downKarti(page, duzenlenen)).toBeVisible({ timeout: 25_000 });
  await expect(downKarti(page, onaylanan)).toBeVisible({ timeout: 25_000 });

  // Bölüm değişti; form açık, değer ve odak yerinde; onay açık.
  const tasinanAlan = downKarti(page, duzenlenen).getByRole("form", { name: `${duzenlenen}: sağlık adresini düzenle` }).getByLabel("Sağlık adresi");
  await expect(tasinanAlan).toHaveValue(taslak);
  await expect(tasinanAlan).toBeFocused();
  await expect(downKarti(page, onaylanan).getByRole("button", { name: "Evet, çıkar" })).toBeVisible();
  await expect(duyuru).toContainText(`${duzenlenen} Down oldu`);
  await expect(duyuru).toContainText(`${onaylanan} Down oldu`);
  await expect(izlemeBolumu(page, "Down").getByRole("heading")).toHaveText(/^Down \(\d+\)$/);

  // Down'dan çıkış da duyurulur; uygulama Ayakta bölümüne döner.
  await downKarti(page, duzenlenen).getByRole("button", { name: "Vazgeç" }).click();
  await saglikAyarla(`/${duzenlenen}`, 200);
  await expect(ayaktaSatiri(page, duzenlenen)).toBeVisible({ timeout: 25_000 });
  await expect(duyuru).toContainText(`${duzenlenen} Ayakta`);

  expect(await page.evaluate(() => (window as unknown as { yenilenmedi?: boolean }).yenilenmedi)).toBe(true);
});

// CB-6: durum okunamazsa son bilinen hâl "Durum güncellenemedi" notuyla görünür; okuma düzelince not kalkar.
test("0012 CB-6 durum okunamazsa son bilinen liste notla görünür, düzelince not kalkar", async ({ page }) => {
  const ad = yeniServisAdi();
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, ad, "Ayakta");

  await page.route("**/durum/uygulamalar", (route) => route.fulfill({ status: 503, body: "{}" }));
  const not = page.getByText("Durum güncellenemedi; son bilinen durum gösteriliyor.");
  await expect(not).toBeVisible({ timeout: 15_000 });
  await expect(ayaktaSatiri(page, ad)).toBeVisible();
  await expect(page.getByText("İzlenen uygulamalar şu anda okunamıyor.")).toHaveCount(0);

  await page.unroute("**/durum/uygulamalar");
  await expect(not).toHaveCount(0, { timeout: 15_000 });
});

// Review 1 F-3, F-5: ekran durumu tam 10 sn'de bir okur. Tarayıcı saati ileri sarılır; okumaların anı tarayıcının
// (sahte) saatiyle kaydedilir, art arda iki okuma arası tam 10 sn'dir. Değişiklik yokken, yoklamadan sonra da duyuru
// yoktur; Down'dan çıkış sonraki okumada görünür ve duyurulur.
test("0012 CB-6 CB-7 ekran durumu 10 sn'de bir okur; değişiklik yokken yoklamadan sonra da duyuru yok", async ({ page }) => {
  await page.clock.install();
  await page.addInitScript(() => {
    const w = window as unknown as { __yoklamalar: number[] };
    w.__yoklamalar = [];
    const asil = window.fetch.bind(window);
    window.fetch = (girdi, ayar) => {
      const adres = typeof girdi === "string" ? girdi : girdi instanceof URL ? girdi.href : girdi.url;
      if (new URL(adres, location.href).pathname === "/durum/uygulamalar") w.__yoklamalar.push(Date.now());
      return asil(girdi, ayar);
    };
  });
  const ad = yeniServisAdi();
  await saglikAyarla(`/${ad}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await expect
    .poll(async () => {
      await page.reload();
      return uygulamaDurumu(page, ad);
    }, { timeout: 20_000 })
    .toBe("Down");

  const duyuru = page.locator("p.sr-only[aria-live=polite]");
  const yoklamaUrl = (u: string) => new URL(u).pathname === "/durum/uygulamalar";
  for (let i = 0; i < 3; i++) {
    const yanit = page.waitForResponse((r) => yoklamaUrl(r.url()));
    await page.clock.runFor(YOKLAMA_MS);
    await yanit;
  }
  const anlar = await page.evaluate(() => (window as unknown as { __yoklamalar: number[] }).__yoklamalar);
  expect(anlar.length).toBeGreaterThanOrEqual(3);
  // Saat kendiliğinden de akar; o sırada tetiklenen okumanın anı birkaç ms kayabilir (ölçülen 9.993 ms).
  for (const [i, an] of anlar.slice(1).entries()) {
    expect(Math.abs(an - anlar[i] - YOKLAMA_MS), `${i + 1}. ve ${i + 2}. okuma arası 10 sn`).toBeLessThan(100);
  }

  // Okumalar geldi, durum değişmedi: duyuru yok.
  await page.waitForTimeout(500);
  await expect(downKarti(page, ad)).toBeVisible();
  await expect(duyuru).toHaveText("");

  await saglikAyarla(`/${ad}`, 200);
  await expect
    .poll(async () => {
      const sonraki = page.waitForResponse((r) => yoklamaUrl(r.url()));
      await page.clock.runFor(YOKLAMA_MS);
      await sonraki;
      return ayaktaSatiri(page, ad).count();
    }, { timeout: 20_000, intervals: [500] })
    .toBe(1);
  await expect(duyuru).toContainText(`${ad} Ayakta`);
});

// Review 1 F-1: düzenleme formundaki hata bölüm değişince korunur; hata kaydedildikten sonra ekran boşta yeniden
// çizim yapmaz.
test("0012 CB-6 düzenleme formundaki hata bölüm değişince korunur; ekran boşta yeniden çizim yapmaz", async ({ page }) => {
  const ad = yeniServisAdi();
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, ad, "Ayakta");

  await ayaktaSatiri(page, ad).getByRole("button", { name: `${ad}: Düzenle` }).click();
  const formAdi = `${ad}: sağlık adresini düzenle`;
  const form = page.getByRole("form", { name: formAdi });
  await form.getByLabel("Sağlık adresi").fill("ftp://x");
  await form.getByRole("button", { name: "Kaydet" }).click();
  await expect(form.getByLabel("Sağlık adresi")).toHaveAttribute("aria-invalid", "true");
  const hata = (await form.locator("p[id$=-hata]").textContent())!;
  expect(hata).not.toBe("");

  expect(await betikSuresi(page, 1_000), "boştaki ekran betik çalıştırmaz").toBeLessThan(0.1);

  await saglikAyarla(`/${ad}`, 503);
  const tasinan = downKarti(page, ad).getByRole("form", { name: formAdi });
  await expect(tasinan).toBeVisible({ timeout: 25_000 });
  await expect(tasinan.getByLabel("Sağlık adresi")).toHaveAttribute("aria-invalid", "true");
  await expect(tasinan.locator("p[id$=-hata]")).toHaveText(hata);
  await expect(tasinan.getByLabel("Sağlık adresi")).toHaveValue("ftp://x");
});

// Review 1 F-6: bölüm değişince odak, imleç yerinde geri gelir; kullanıcı satırdan ayrıldıysa odak geri çekilmez.
test("0012 CB-6 odak imleç yerinde geri gelir; satırdan ayrılan kullanıcının odağı çekilmez", async ({ page }) => {
  const yazan = `a-${yeniServisAdi()}`;
  const ayrilan = `b-${yeniServisAdi()}`;
  await girisYap(page, "/izlenen-uygulamalar");
  for (const ad of [yazan, ayrilan]) await uygulamaEkleUi(page, ad, saglikAdresi(`/${ad}`));
  await durumuBekle(page, yazan, "Ayakta");
  await durumuBekle(page, ayrilan, "Ayakta");
  const alan = (bolum: Locator, ad: string) => bolum.getByRole("form", { name: `${ad}: sağlık adresini düzenle` }).getByLabel("Sağlık adresi");

  // İmleç adresin ortasında.
  await ayaktaSatiri(page, yazan).getByRole("button", { name: `${yazan}: Düzenle` }).click();
  await alan(ayaktaSatiri(page, yazan), yazan).fill("http://ornek.test/yol");
  await alan(ayaktaSatiri(page, yazan), yazan).press("Home");
  for (let i = 0; i < 7; i++) await alan(ayaktaSatiri(page, yazan), yazan).press("ArrowRight");
  await saglikAyarla(`/${yazan}`, 503);
  const tasinan = alan(downKarti(page, yazan), yazan);
  await expect(tasinan).toBeVisible({ timeout: 25_000 });
  await expect(tasinan).toBeFocused();
  expect(await tasinan.evaluate((el: HTMLInputElement) => [el.selectionStart, el.selectionEnd])).toEqual([7, 7]);
  await downKarti(page, yazan).getByRole("button", { name: "Vazgeç" }).click();

  // Form açık bırakılıp boş bir yere tıklanır: odak satırdan ayrılmıştır, bölüm değişince geri çekilmez.
  await ayaktaSatiri(page, ayrilan).getByRole("button", { name: `${ayrilan}: Düzenle` }).click();
  await alan(ayaktaSatiri(page, ayrilan), ayrilan).fill("http://ornek.test/b");
  await page.getByRole("heading", { level: 1 }).click();
  await expect(alan(ayaktaSatiri(page, ayrilan), ayrilan)).not.toBeFocused();
  await saglikAyarla(`/${ayrilan}`, 503);
  await expect(alan(downKarti(page, ayrilan), ayrilan)).toBeVisible({ timeout: 25_000 });
  await expect(alan(downKarti(page, ayrilan), ayrilan)).not.toBeFocused();
});

// Review 2 B-2: 320 px'te doğrudan açılan ekran da kart gösterir ve kaymaz. Sunucu ekranı bilmez, geniş varsayar;
// kart hidrasyondan hemen sonra gelir (o ana kadar tablo görünebilir; plan 0012, fix-2 sapması). Dar ekranda
// durum yardımcıları kartı okur.
test("0012 CB-2 320 px'te doğrudan açılan ekranda Ayakta kart olur ve kaymaz", async ({ page }) => {
  const up = yeniServisAdi();
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, up, saglikAdresi(`/${up}`));
  await page.setViewportSize({ width: 320, height: 800 });
  await page.goto(appUrl("/izlenen-uygulamalar"));

  await expect(ayaktaSatiri(page, up)).toBeVisible();
  await expect(izlemeBolumu(page, "Ayakta").getByRole("table")).toHaveCount(0);
  await durumuBekle(page, up, "Ayakta");
  for (const oge of [izlemeBolumu(page, "Ayakta"), ...(await izlemeBolumu(page, "Ayakta").locator("*").all())]) {
    expect(await oge.evaluate((el) => (el instanceof HTMLElement ? el.scrollWidth - el.clientWidth : 0)), "Ayakta bölümünde hiçbir öğe kaymaz").toBeLessThanOrEqual(0);
  }
});

// Review 2 B-1: kayıt uygulamayı başka bölüme taşırken form, uygulama eski adresiyle eski bölümde dururken kapanmaz
// (kapanma ile yeni veri aynı commit'te). Her commit'ten sonra DOM gözlenir.
test("0012 CB-6 kaydedilen form, eski adresli kart görünürken kapanmaz", async ({ page }) => {
  const ad = yeniServisAdi();
  const eski = saglikAdresi(`/${ad}`);
  await saglikAyarla(`/${ad}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ad, eski);
  await durumuBekle(page, ad, "Down");

  await downKarti(page, ad).getByRole("button", { name: `${ad}: Düzenle` }).click();
  const duzenle = page.getByRole("form", { name: `${ad}: sağlık adresini düzenle` });
  await duzenle.getByLabel("Sağlık adresi").fill(saglikAdresi(`/${ad}-yeni`));
  await page.evaluate(
    ([ad, eski]) => {
      const w = window as unknown as { __anlar: { form: boolean; eskiKart: boolean }[] };
      w.__anlar = [];
      const kaydet = () => {
        const form = !!document.querySelector(`form[aria-label="${ad}: sağlık adresini düzenle"]`);
        const eskiKart = [...document.querySelectorAll('section[aria-label="Down"] li')].some(
          (li) => li.textContent?.includes(ad) && [...li.querySelectorAll("dd")].some((dd) => dd.textContent?.trim() === eski),
        );
        w.__anlar.push({ form, eskiKart });
      };
      new MutationObserver(kaydet).observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true });
    },
    [ad, eski],
  );
  await duzenle.getByRole("button", { name: "Kaydet" }).click();
  await expect(ayaktaSatiri(page, ad)).toBeVisible();
  await expect(duzenle).toHaveCount(0);

  const anlar = await page.evaluate(() => (window as unknown as { __anlar: { form: boolean; eskiKart: boolean }[] }).__anlar);
  expect(anlar.length).toBeGreaterThan(0);
  expect(anlar.filter((a) => !a.form && a.eskiKart), "form kapanmış ama eski adresli Down kartı duruyor").toEqual([]);
});
