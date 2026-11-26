import { expect, test, type Page } from "@playwright/test";
import { appUrl, girisFormunuDoldur, girisYap, izlemeleriTemizle, kayitEkle, kayitlarEkle, saatOnce, saglikAdresi, saglikAyarla, sql, sqlInBackground, uygulamaEkleUi, yeniServisAdi } from "./seed";

// Spec 0006 — dashboard ekranı: açılış (AC-1), görünüm (AC-2 … AC-5), ayrıntıya geçiş (AC-6), boş durumlar
// (AC-7), bölüm arızası (AC-8). Kesin sayılar Api testlerinde boş veritabanıyla kanıtlanır; burada veritabanı
// testler arasında paylaşıldığından benzersiz servis adlarıyla göreli kontroller yapılır (plan 0006, risk 3).

test.afterEach(izlemeleriTemizle);

function bolum(page: Page, baslik: string) {
  return page.getByRole("region", { name: baslik });
}

/** Benzersiz bir servis için çok sayıda hata ekler: paylaşılan veritabanında da "en çok" listelerine girer. */
async function cokHataEkle(servis: string, exceptionType: string, adet: number): Promise<void> {
  await sql(
    `INSERT INTO log_entries (id, service_name, level, message, exception_type, received_at)
     SELECT gen_random_uuid(), '${servis}', 'Error', 'dashboard ' || i, '${exceptionType}', now() - interval '5 minutes' - i * interval '1 millisecond'
     FROM generate_series(1, ${adet}) AS i`,
  );
}

test("AC-1 oturumsuz kullanıcı dashboard yerine girişe gider; girişten sonra ve kök adreste dashboard açılır", async ({ page }) => {
  await page.goto(appUrl("/dashboard"));
  await expect(page).toHaveURL((url) => url.pathname === "/giris");

  await page.goto(appUrl("/giris"));
  await girisFormunuDoldur(page);
  await expect(page).toHaveURL(appUrl("/dashboard"));
  await expect(page.getByRole("heading", { level: 1, name: "Dashboard" })).toBeVisible();

  await page.goto(appUrl("/"));
  await expect(page).toHaveURL(appUrl("/dashboard"));
  await expect(page.getByRole("navigation", { name: "Ana menü" }).getByRole("link", { name: "Loglar" })).toBeVisible();
});

test("AC-2 AC-3 AC-4 AC-6 bölümler pencereyi, sayıları ve listeye giden bağlantıları gösterir", async ({ page }) => {
  const servis = yeniServisAdi();
  const tur = `E2E.Dashboard${Date.now()}Exception`;
  await cokHataEkle(servis, tur, 400);
  await girisYap(page, "/dashboard");

  await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();
  await expect(page.getByText(/Yüklendi:/)).toBeVisible();
  for (const level of ["Critical", "Error", "Warning", "Info"]) {
    await expect(bolum(page, "Önem dereceleri").getByRole("link", { name: new RegExp(`^${level} [\\d.]+ kayıt`) })).toBeVisible();
  }

  const servisBaglantisi = bolum(page, "En çok hata üreten servisler").getByRole("link", { name: `${servis}: 400 hata, log listesinde aç` });
  await expect(servisBaglantisi).toBeVisible();
  const turBaglantisi = bolum(page, "En sık exception'lar").getByRole("link", { name: `${tur}: 400 kayıt, log listesinde aç` });
  await expect(turBaglantisi).toBeVisible();
  const errorBaglantisi = bolum(page, "Önem dereceleri").getByRole("link", { name: /^Error / });

  // Üç bağlantı da dashboard'un aynı 24 saatlik penceresini taşır (review: VERIFY G-2). Geri dönüşte dashboard
  // yeniden yüklenip pencere kayabildiğinden eşitlik, ilk yüklemedeki bağlantılarda denetlenir.
  const pencere = (adres: string) => {
    const q = new URL(adres, appUrl("/")).searchParams;
    return [q.get("from"), q.get("to")];
  };
  const servisPenceresi = pencere((await servisBaglantisi.getAttribute("href"))!);
  expect(Date.parse(servisPenceresi[1]!) - Date.parse(servisPenceresi[0]!)).toBe(24 * 60 * 60 * 1000);
  expect(pencere((await turBaglantisi.getAttribute("href"))!)).toEqual(servisPenceresi);
  expect(pencere((await errorBaglantisi.getAttribute("href"))!)).toEqual(servisPenceresi);

  // Servis sayısı: aynı pencere, servis ve Error + Critical filtresiyle liste.
  await servisBaglantisi.click();
  await expect(page).toHaveURL((url) => url.pathname === "/logs");
  const servisAdresi = new URL(page.url()).searchParams;
  expect(servisAdresi.get("service")).toBe(servis);
  expect(servisAdresi.getAll("level")).toEqual(["Error", "Critical"]);
  expect(pencere(page.url())).toEqual(servisPenceresi);
  await expect(page.locator("tbody tr")).toHaveCount(50);
  for (const satir of await page.locator("tbody tr").all()) {
    await expect(satir).toContainText(servis);
  }

  // Exception sayısı: aynı pencere ve exceptionType filtresi; formda da görünür.
  await page.goBack();
  await turBaglantisi.click();
  await expect(page).toHaveURL((url) => url.searchParams.get("exceptionType") === tur);
  expect(pencere(page.url())[0]).toBeTruthy();
  await expect(page.getByLabel("Exception türü")).toHaveValue(tur);
  await expect(page.locator("tbody tr").first()).toContainText(tur);

  // Derece sayısı: aynı pencere ve yalnızca o derece.
  await page.goBack();
  await errorBaglantisi.click();
  await expect(page).toHaveURL((url) => url.searchParams.getAll("level").join() === "Error");
  expect(pencere(page.url())[0]).toBeTruthy();
});

test("AC-5 izlenen uygulama sayıları ve Down olanlar görünür; özet izlenen uygulamalar ekranını açar", async ({ page }) => {
  await izlemeleriTemizle();
  const ayakta = yeniServisAdi();
  const down = yeniServisAdi();
  await saglikAyarla(`/${down}`, 503);
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ayakta, saglikAdresi(`/${ayakta}`));
  await uygulamaEkleUi(page, down, saglikAdresi(`/${down}`));

  const uygulamalar = bolum(page, "İzlenen uygulamalar");
  const ozet = uygulamalar.getByRole("link", { name: /Durum özeti, izlenen uygulamalar ekranında aç/ });
  // Sayılar tam metinle (VERIFY G-3): yalnızca bu testin iki uygulaması vardır, ikisi de kontrol edilmiştir.
  await expect
    .poll(async () => {
      await page.goto(appUrl("/dashboard"));
      // Kontrol bitmeden beklemeden döner; her denemede sayfa yeniden yüklenir.
      return ozet.getByRole("listitem").allTextContents();
    }, { timeout: 20_000 })
    .toEqual(["Ayakta: 1", "Down: 1", "Bilinmiyor: 0"]);

  await expect(uygulamalar.getByRole("list", { name: "Down olan uygulamalar" })).toContainText(down);
  await expect(uygulamalar.getByRole("listitem").filter({ hasText: down })).toContainText("zamanından beri");
  await expect(uygulamalar.getByRole("list", { name: "Down olan uygulamalar" })).not.toContainText(ayakta);

  await ozet.click();
  await expect(page).toHaveURL(appUrl("/izlenen-uygulamalar"));
});

/** ISO zamanı tamsayı mikrosaniyeye çevirir; Date yalnızca milisaniye taşıdığı için kesir ayrı okunur. */
function mikrosaniye(iso: string): bigint {
  const m = iso.match(/^(.*T\d\d:\d\d:\d\d)(?:\.(\d+))?(Z|[+-]\d\d:\d\d)$/);
  expect(m, `Beklenmeyen zaman biçimi: ${iso}`).not.toBeNull();
  return BigInt(Date.parse(m![1] + m![3])) * 1000n + BigInt((m![2] ?? "").padEnd(6, "0").slice(0, 6));
}

/** "14:37–15:37: Critical 2, Error 1.204, Warning 0, Info 30 kayıt, log listesinde aç" → derece sayıları. */
function dilimSayilari(ad: string): Record<string, number> {
  const eslesme = ad.match(/^\d\d:\d\d–\d\d:\d\d: Critical ([\d.]+), Error ([\d.]+), Warning ([\d.]+), Info ([\d.]+) kayıt, log listesinde aç$/);
  expect(eslesme, `Beklenmeyen dilim adı: ${ad}`).not.toBeNull();
  const [, critical, error, warning, info] = eslesme!.map((s) => s?.replaceAll(".", ""));
  return { Critical: Number(critical), Error: Number(error), Warning: Number(warning), Info: Number(info) };
}

// Spec 0007, CB-1 ve CB-2: 24 dilim, adında saat aralığı ve dört derecenin sayısı; derecelerin dilim toplamı derece
// sayısına eşit; bir dilim listeyi kendi aralığıyla açar ve satır sayısı dilimin toplamıdır. Paylaşılan veritabanında
// kesin sayılar Api testlerindedir; burada ekranın bu sayıları doğru taşıdığı sınanır.
test("0007 CB-1 CB-2 saatlik trend 24 dilim gösterir, toplamlar derecelerle tutar, dilim listeyi kendi aralığıyla açar", async ({ page }) => {
  const servis = yeniServisAdi();
  await kayitlarEkle(servis, 3, saatOnce(20.5));
  await girisYap(page, "/dashboard");

  const trend = bolum(page, "Son 24 saat");
  await expect(trend.getByRole("list", { name: "Dereceler" })).toHaveText(/Critical.*Error.*Warning.*Info/);
  const dilimler = trend.getByRole("list", { name: /^Saatlik dağılım/ }).getByRole("link");
  await expect(dilimler).toHaveCount(24);
  await expect(bolum(page, "Önem dereceleri").getByRole("link")).toHaveCount(4);

  const adlar = await dilimler.evaluateAll((l) => l.map((a) => a.getAttribute("aria-label")!));
  const toplamlar: Record<string, number> = { Critical: 0, Error: 0, Warning: 0, Info: 0 };
  for (const ad of adlar) {
    for (const [derece, sayi] of Object.entries(dilimSayilari(ad))) toplamlar[derece] += sayi;
  }
  for (const derece of ["Critical", "Error", "Warning", "Info"]) {
    const ad = await bolum(page, "Önem dereceleri").getByRole("link", { name: new RegExp(`^${derece} `) }).textContent();
    expect(toplamlar[derece], derece).toBe(Number(ad!.match(/[\d.]+/)![0].replaceAll(".", "")));
  }

  // VERIFY F-1: her dilimin bitişi bir sonrakinin başlangıcından tam 1 µs öncedir; sınırdaki kayıt iki dilimde
  // sayılmaz. İlk dilimin başından son dilimin sonuna tam 24 saattir.
  const sinirlar = await dilimler.evaluateAll((l) => l.map((a) => {
    const q = new URL(a.getAttribute("href")!, location.href).searchParams;
    return [q.get("from")!, q.get("to")!];
  }));
  for (let i = 0; i + 1 < sinirlar.length; i++) {
    expect(mikrosaniye(sinirlar[i + 1][0]) - mikrosaniye(sinirlar[i][1]), `dilim ${i} bitişi`).toBe(1n);
  }
  expect(mikrosaniye(sinirlar[23][1]) - mikrosaniye(sinirlar[0][0])).toBe(24n * 3600n * 1_000_000n);

  // Eklenen kayıtların dilimi en az 3 kayıt taşır; listede bir sayfaya sığan bir dilim seçilir.
  const toplam = await dilimler.evaluateAll((l) => l.map((a) => Number(a.getAttribute("data-toplam"))));
  const sira = toplam.findIndex((t) => t >= 3 && t <= 50);
  expect(sira, "bir sayfaya sığan dolu dilim").toBeGreaterThanOrEqual(0);
  const dilim = dilimler.nth(sira);
  const hedef = new URL((await dilim.getAttribute("href"))!, appUrl("/")).searchParams;
  await dilim.click();
  await expect(page).toHaveURL((url) => url.pathname === "/logs");
  const adres = new URL(page.url()).searchParams;
  expect([adres.get("from"), adres.get("to")]).toEqual([hedef.get("from"), hedef.get("to")]);
  expect(adres.getAll("level")).toEqual([]);
  await expect(page.locator("tbody tr")).toHaveCount(toplam[sira]);
});

// Spec 0007, CB-1 (review 1, F-3): 320 px genişlikte trend bölümü kendi kutusundan taşmaz; 24 dilim ve eksen
// etiketleri sığar. Sayfanın üst menüsünün dar ekranda taşması 0007'den önce de vardı (backlog).
test("0007 CB-1 dar ekranda saatlik trend kendi bölümünden yatay taşmaz", async ({ page }) => {
  await kayitEkle({ servis: yeniServisAdi(), alinma: saatOnce(2), level: "Warning", message: "dar-ekran" });
  await page.setViewportSize({ width: 320, height: 800 });
  await girisYap(page, "/dashboard");

  const trend = bolum(page, "Son 24 saat");
  await expect(trend.getByRole("list", { name: /^Saatlik dağılım/ }).getByRole("link")).toHaveCount(24);
  expect(await trend.evaluate((el) => el.scrollWidth - el.clientWidth), "bölüm içeriği yatay taşmaz").toBeLessThanOrEqual(0);
  const kutu = (await trend.boundingBox())!;
  for (const etiket of await trend.locator("time").all()) {
    const b = (await etiket.boundingBox())!;
    expect(b.x + b.width, "eksen etiketi bölümün sağ kenarını aşmaz").toBeLessThanOrEqual(kutu.x + kutu.width);
  }
});

// VERIFY F-2: spec revizyonu 1'in görsel öğeleri — ölçek (0, yarı, üst), üç ızgara çizgisi ve üzerine gelince ya da
// klavye odağında görünen ipucu. İpucu yalnızca görseldir (aria-hidden); aynı bilgi bağlantının adındadır.
test("0007 CB-1 trendde ölçek ve ızgara vardır, ipucu üzerine gelince ve klavye odağında görünür", async ({ page }) => {
  await kayitlarEkle(yeniServisAdi(), 4, saatOnce(2.5));
  await girisYap(page, "/dashboard");

  const trend = bolum(page, "Son 24 saat");
  const dilimler = trend.getByRole("list", { name: /^Saatlik dağılım/ }).getByRole("link");
  await expect(dilimler).toHaveCount(24);
  const toplamlar = await dilimler.evaluateAll((l) => l.map((a) => Number(a.getAttribute("data-toplam"))));

  const olcek = (await trend.locator('ol[aria-hidden="true"]').first().locator("li").allTextContents()).map((t) => Number(t.replaceAll(".", "").replace(",", ".")));
  expect(olcek).toHaveLength(3);
  expect(olcek[0]).toBe(0);
  expect(olcek[2]).toBe(olcek[1] * 2);
  expect(olcek[2], "üst değer en yüksek dilimden küçük değildir").toBeGreaterThanOrEqual(Math.max(...toplamlar));

  const cizgiler = trend.locator('div[aria-hidden="true"]').first().locator(":scope > span");
  await expect(cizgiler).toHaveCount(3);
  const y = await cizgiler.evaluateAll((l) => l.map((s) => s.getBoundingClientRect().top).sort((a, b) => a - b));
  expect(Math.abs(y[1] - y[0] - (y[2] - y[1])), "ızgara çizgileri eşit aralıklı").toBeLessThanOrEqual(1);

  const sira = toplamlar.findIndex((t, i) => i > 0 && t > 0);
  expect(sira, "dolu bir dilim").toBeGreaterThan(0);
  const dilim = dilimler.nth(sira);
  const ipucu = dilim.locator("xpath=..").locator('div[aria-hidden="true"]');
  const aralik = (await dilim.getAttribute("aria-label"))!.split(": ")[0];
  await expect(ipucu).toBeHidden();

  await dilim.hover();
  await expect(ipucu).toBeVisible();
  await expect(ipucu).toContainText(aralik);
  // Bileşen sayıyı tr-TR biçimiyle yazar (binlik ayırıcı); 1000 ve üstü toplamlar da eşleşsin (review 3, B-2).
  await expect(ipucu).toContainText(`Toplam ${new Intl.NumberFormat("tr-TR").format(toplamlar[sira])}`);

  await page.mouse.move(0, 0);
  await expect(ipucu).toBeHidden();
  await dilimler.nth(sira - 1).focus();
  await page.keyboard.press("Tab");
  await expect(dilim).toBeFocused();
  await expect(ipucu).toBeVisible();
});

// Spec 0007, CB-3: sayıların yanında orantılı çubuklar; sayılar ve bağlantılar yerinde kalır.
test("0007 CB-3 oran çubukları sayılarla orantılıdır", async ({ page }) => {
  await izlemeleriTemizle();
  const servis = yeniServisAdi();
  await cokHataEkle(servis, `E2E.Oran${Date.now()}Exception`, 30);
  const ayakta = yeniServisAdi();
  await girisYap(page, "/izlenen-uygulamalar");
  await uygulamaEkleUi(page, ayakta, saglikAdresi(`/${ayakta}`));
  await page.goto(appUrl("/dashboard"));

  // Önem dereceleri: her derecenin toplama oranı.
  // Bölümler akarak gelir; evaluateAll beklemediği için önce içerik beklenir.
  const dereceler = bolum(page, "Önem dereceleri").getByRole("listitem");
  await expect(dereceler).toHaveCount(4);
  const sayilar = await dereceler.evaluateAll((l) => l.map((li) => Number(li.querySelector("a")!.textContent!.match(/[\d.]+/)![0].replaceAll(".", ""))));
  const oranlar = await dereceler.evaluateAll((l) => l.map((li) => Number(li.querySelector("[data-oran]")!.getAttribute("data-oran"))));
  const tum = sayilar.reduce((a, b) => a + b, 0);
  expect(oranlar).toEqual(sayilar.map((s) => Math.round((s / tum) * 100)));

  // Servisler ve exception'lar: en büyük satır 100, sıra boyunca azalmaz.
  for (const baslik of ["En çok hata üreten servisler", "En sık exception'lar"]) {
    await expect(bolum(page, baslik).locator("tbody [data-oran]").first()).toBeAttached();
    const satirOranlari = await bolum(page, baslik).locator("tbody [data-oran]").evaluateAll((l) => l.map((e) => Number(e.getAttribute("data-oran"))));
    expect(satirOranlari[0], baslik).toBe(100);
    expect(satirOranlari, baslik).toEqual([...satirOranlari].sort((a, b) => b - a));
    await expect(bolum(page, baslik).getByRole("link").first()).toBeVisible();
  }

  // İzlenen uygulamalar: tek uygulama → yalnızca durumunun parçası, %100.
  const parcalar = bolum(page, "İzlenen uygulamalar").locator("[data-durum]");
  await expect(parcalar).toHaveCount(1);
  await expect(parcalar.first()).toHaveAttribute("data-oran", "100");
});

// Spec 0006, States: her bölüm kendi yükleniyor durumunu gösterir; biri beklerken öteki dolar (R-9, VERIFY G-1).
test("veri gelene kadar her bölüm kendi yükleniyor durumunu gösterir, öteki bölüm beklemeden dolar", async ({ page }) => {
  await girisYap(page, "/dashboard");

  for (const [tablo, yuklenenler, dolu] of [
    [
      "log_entries",
      [
        ["Son 24 saat", "Saatlik dağılım yükleniyor…"],
        ["Önem dereceleri", "Önem dereceleri yükleniyor…"],
        ["En çok hata üreten servisler", "Servis özeti yükleniyor…"],
        ["En sık exception'lar", "Exception özeti yükleniyor…"],
      ],
      bolum(page, "İzlenen uygulamalar").getByText(/Henüz izlenen uygulama yok|Ayakta:/),
    ],
    ["monitored_apps", [["İzlenen uygulamalar", "İzlenen uygulamalar yükleniyor…"]], page.getByText(/^Son 24 saat:/)],
  ] as const) {
    // Tablo birkaç saniye kilitlenir: o bölümün sorgusu bekler (durumlar.spec.ts ile aynı desen).
    const kilit = sqlInBackground(`BEGIN; LOCK TABLE ${tablo} IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(5); COMMIT;`);
    await expect
      .poll(() => sql(`SELECT count(*) FROM pg_locks l JOIN pg_class c ON c.oid = l.relation WHERE c.relname = '${tablo}' AND l.mode = 'AccessExclusiveLock' AND l.granted`))
      .toBe("1");

    await page.goto(appUrl("/dashboard"), { waitUntil: "commit" });
    // Yükleniyor satırı kendi bölümünün içindedir: bölüm başlığıyla birlikte görünür (VERIFY triage, G-1).
    const satirlar = yuklenenler.map(([baslik, metin]) => bolum(page, baslik).getByRole("status").filter({ hasText: metin }));
    for (const satir of satirlar) {
      await expect(satir).toBeVisible();
    }
    // Öteki bölüm dolduğunda bu bölüm hâlâ yükleniyor olmalıdır; beklemeden denetlenir. İki bölüm birlikte
    // gelseydi dolu bölüm ancak yükleniyor satırları kalktıktan sonra görünürdü (R-9, review 4 L-1).
    await expect(dolu.first()).toBeVisible();
    for (const satir of satirlar) {
      expect(await satir.isVisible()).toBe(true);
    }
    await kilit;
    for (const satir of satirlar) {
      await expect(satir).toHaveCount(0);
    }
  }
});

test("AC-8 log verisi okunamazken log bölümleri hata gösterir, uygulama bölümü normal kalır; tersi de geçerlidir", async ({ page }) => {
  await girisYap(page, "/dashboard");
  const loglarHata = (p: Page) => bolum(p, "Önem dereceleri").getByRole("alert");
  const uygulamaHata = (p: Page) => bolum(p, "İzlenen uygulamalar").getByRole("alert");

  await sql("ALTER TABLE log_entries RENAME TO log_entries_e2e_ariza");
  try {
    await page.reload();
    await expect(loglarHata(page)).toContainText("Log sayıları şu anda okunamıyor.");
    // Spec 0007, CB-4: trend bölümü de log verisinden gelir ve aynı arızayı gösterir.
    await expect(bolum(page, "Son 24 saat").getByRole("alert")).toContainText("Saatlik dağılım şu anda okunamıyor.");
    await expect(bolum(page, "Son 24 saat").getByRole("button", { name: "Yeniden dene" })).toBeVisible();
    await expect(bolum(page, "En çok hata üreten servisler").getByRole("alert")).toBeVisible();
    await expect(bolum(page, "En sık exception'lar").getByRole("alert")).toBeVisible();
    await expect(uygulamaHata(page)).toHaveCount(0);
    await expect(bolum(page, "İzlenen uygulamalar").getByText(/Henüz izlenen uygulama yok|Ayakta:/)).toBeVisible();
  } finally {
    await sql("ALTER TABLE log_entries_e2e_ariza RENAME TO log_entries");
  }

  // "Yeniden dene" veriyi yeniden yükler.
  await loglarHata(page).getByRole("button", { name: "Yeniden dene" }).click();
  await expect(loglarHata(page)).toHaveCount(0);
  await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();

  await sql("ALTER TABLE monitored_apps RENAME TO monitored_apps_e2e_ariza");
  try {
    await page.getByRole("button", { name: "Yenile" }).click();
    await expect(uygulamaHata(page)).toContainText("İzlenen uygulamalar şu anda okunamıyor.");
    await expect(loglarHata(page)).toHaveCount(0);
    await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();
  } finally {
    await sql("ALTER TABLE monitored_apps_e2e_ariza RENAME TO monitored_apps");
  }

  await uygulamaHata(page).getByRole("button", { name: "Yeniden dene" }).click();
  await expect(uygulamaHata(page)).toHaveCount(0);
});

// AC-7: bu proje henüz hiç kayıt yokken koşar. Eklenen kayıt testin sonunda silinir; ardından koşan boş depo
// testleri etkilenmez.
test("@bos-depo AC-7 her bölüm kendi boş durumunu gösterir: hiç log yok, log var ama hata yok, uygulama yok", async ({ page }) => {
  await girisYap(page, "/dashboard");

  await expect(bolum(page, "Önem dereceleri").getByText("Son 24 saatte kayıt yok")).toBeVisible();
  await expect(bolum(page, "En çok hata üreten servisler").getByText("Son 24 saatte hata yok")).toBeVisible();
  await expect(bolum(page, "En sık exception'lar").getByText("Son 24 saatte exception yok")).toBeVisible();
  await expect(bolum(page, "İzlenen uygulamalar").getByText("Henüz izlenen uygulama yok")).toBeVisible();
  // Spec 0007, CB-4: hiç kayıt yokken grafik yerine mesaj.
  await expect(bolum(page, "Son 24 saat").getByText("Son 24 saatte kayıt yok")).toBeVisible();
  await expect(bolum(page, "Son 24 saat").getByRole("link")).toHaveCount(0);
  await expect(bolum(page, "İzlenen uygulamalar").getByRole("link", { name: "İzlenen uygulamalar ekranına git" })).toBeVisible();

  const servis = yeniServisAdi();
  await kayitEkle({ servis, alinma: saatOnce(1), level: "Info", message: "yalnizca-bilgi" });
  try {
    await page.reload();
    await expect(bolum(page, "Önem dereceleri").getByRole("link", { name: /^Info 1 kayıt/ })).toBeVisible();
    await expect(bolum(page, "En çok hata üreten servisler").getByText("Son 24 saatte hata yok")).toBeVisible();
    await expect(bolum(page, "En sık exception'lar").getByText("Son 24 saatte exception yok")).toBeVisible();
  } finally {
    await sql(`DELETE FROM log_entries WHERE service_name = '${servis}'`);
  }
});

// --- Spec 0011: zaman penceresi seçimi -------------------------------------------------------------------------------

const pencereSecimi = (p: Page) => p.getByLabel("Zaman penceresi");

/** Benzersiz bir servis için `saat` saat önceye `adet` Critical kayıt (1 ms arayla); exception türüyle. */
async function eskiHatalarEkle(servis: string, tur: string, adet: number, saat: number): Promise<void> {
  await sql(
    `INSERT INTO log_entries (id, service_name, level, message, exception_type, received_at)
     SELECT gen_random_uuid(), '${servis}', 'Critical', 'eski ' || i, '${tur}', now() - interval '${saat} hours' - i * interval '1 millisecond'
     FROM generate_series(1, ${adet}) AS i`,
  );
}

/** Bağlantının taşıdığı pencere (from, to) ve uzunluğu (ms). */
function baglantiPenceresi(adres: string): { from: string; to: string; uzunluk: number } {
  const q = new URL(adres, appUrl("/")).searchParams;
  const from = q.get("from")!;
  const to = q.get("to")!;
  return { from, to, uzunluk: Date.parse(to) - Date.parse(from) };
}

/** "Yüklendi:" satırındaki an: yüklenme anı (pencerenin sonu). */
async function yuklenmeAni(p: Page): Promise<string> {
  return (await p.getByText(/Yüklendi:/).locator("time").last().getAttribute("datetime"))!;
}

// CB-1: seçim adreste durur; yenileme, geri tuşu ve girişten dönüş aynı pencereyi getirir; tanımsız değer 24 saattir;
// "Yenile" seçimi korur ve yüklenme anı ilerler.
test("0011 CB-1 seçim adreste durur: yenileme, geri tuşu, tanımsız değer, Yenile ve girişten dönüş", async ({ page }) => {
  await girisYap(page, "/dashboard");
  await expect(pencereSecimi(page)).toHaveValue("24s");
  await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();

  await pencereSecimi(page).selectOption({ label: "Son 1 hafta" });
  await expect(page).toHaveURL((url) => url.pathname === "/dashboard" && url.searchParams.get("pencere") === "7g");
  await expect(page.getByText(/^Son 1 hafta:/)).toBeVisible();
  await expect(bolum(page, "Son 1 hafta")).toBeVisible();

  await page.reload();
  await expect(pencereSecimi(page)).toHaveValue("7g");
  await expect(page.getByText(/^Son 1 hafta:/)).toBeVisible();

  const once = await yuklenmeAni(page);
  await page.getByRole("button", { name: "Yenile" }).click();
  await expect.poll(() => yuklenmeAni(page)).not.toBe(once);
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "7g");
  await expect(page.getByText(/^Son 1 hafta:/)).toBeVisible();

  await pencereSecimi(page).selectOption({ label: "Son 3 ay" });
  await expect(page.getByText(/^Son 3 ay:/)).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "7g");
  await expect(page.getByText(/^Son 1 hafta:/)).toBeVisible();
  await expect(pencereSecimi(page)).toHaveValue("7g");

  for (const bozuk of ["?pencere=1y", "?pencere=aralik&baslangic=2026-02-30&bitis=2026-03-01", "?pencere=aralik"]) {
    await page.goto(appUrl(`/dashboard${bozuk}`));
    await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();
    await expect(pencereSecimi(page)).toHaveValue("24s");
  }

  // Girişsiz açılan seçimli adres, girişten sonra aynı seçimle döner.
  await page.context().clearCookies();
  await page.goto(appUrl("/dashboard?pencere=30g"));
  await girisFormunuDoldur(page);
  await expect(page).toHaveURL((url) => url.pathname === "/dashboard" && url.searchParams.get("pencere") === "30g");
  await expect(page.getByText(/^Son 1 ay:/)).toBeVisible();
});

// CB-2 ve CB-4: seçilen pencere log bölümlerine uygulanır (uygulama bölümü etkilenmez); bağlantılar seçilen pencereyi
// taşır ve listeyi aynı aralıkla açar; sayılar binlik ayırıcıyla yazılır. Kesin sayılar Api testlerindedir.
test("0011 CB-2 CB-4 seçilen pencere bölümlere uygulanır, bağlantılar pencereyi taşır ve listeyi açar", async ({ page }) => {
  const servis = yeniServisAdi();
  const tur = `E2E.Eski${Date.now()}Exception`;
  // 60 gün önce: 30 günlük pencerenin dışında, 90 günlüğün içinde. 1.000 kayıt: paylaşılan veritabanında "en çok"
  // listelerine girer.
  await eskiHatalarEkle(servis, tur, 1000, 60 * 24);
  await girisYap(page, "/dashboard?pencere=30g");

  await expect(page.getByText(/^Son 1 ay:/)).toBeVisible();
  await expect(bolum(page, "En çok hata üreten servisler").getByRole("link", { name: new RegExp(`^${servis}:`) })).toHaveCount(0);
  await expect(bolum(page, "En sık exception'lar").getByRole("link", { name: new RegExp(`^${tur}:`) })).toHaveCount(0);

  await pencereSecimi(page).selectOption({ label: "Son 3 ay" });
  const servisBaglantisi = bolum(page, "En çok hata üreten servisler").getByRole("link", { name: `${servis}: 1.000 hata, log listesinde aç` });
  await expect(servisBaglantisi).toBeVisible();
  await expect(bolum(page, "En sık exception'lar").getByRole("link", { name: `${tur}: 1.000 kayıt, log listesinde aç` })).toBeVisible();
  // Başlıklar pencereyi adıyla söyler; uygulama bölümü pencereden bağımsızdır.
  await expect(page.getByRole("heading", { name: "Önem dereceleri · Son 3 ay" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "İzlenen uygulamalar", exact: true })).toBeVisible();

  const servisPenceresi = baglantiPenceresi((await servisBaglantisi.getAttribute("href"))!);
  expect(servisPenceresi.uzunluk).toBe(90 * 24 * 3_600_000);
  const criticalBaglantisi = bolum(page, "Önem dereceleri").getByRole("link", { name: /^Critical / });
  expect(baglantiPenceresi((await criticalBaglantisi.getAttribute("href"))!)).toEqual(servisPenceresi);

  await servisBaglantisi.click();
  await expect(page).toHaveURL((url) => url.pathname === "/logs" && url.searchParams.get("service") === servis);
  expect(baglantiPenceresi(page.url())).toEqual(servisPenceresi);
  await expect(page.locator("tbody tr")).toHaveCount(50);
  await expect(page.locator("tbody tr").first()).toContainText(servis);
});

// CB-4, uzun pencerede dilim bağlantısı (Review 1, F-4): 90 günlük pencerede bir dilim, log listesini o dilimin kendi
// from/to değerleriyle açar.
test("0011 CB-4 uzun pencerede dilim bağlantısı listeyi dilimin aralığıyla açar", async ({ page }) => {
  // 61,5 gün önce: dilimler yüklenme anından 3 günlük adımlarla geriye sayılır; kayıt bir dilimin ortasındadır.
  await eskiHatalarEkle(yeniServisAdi(), `E2E.Dilim${Date.now()}Exception`, 3, 61.5 * 24);
  await girisYap(page, "/dashboard?pencere=90g");

  const dilimler = bolum(page, "Son 3 ay").getByRole("list", { name: /^Dağılım/ }).getByRole("link");
  await expect(dilimler).toHaveCount(30);
  const an = Date.now() - 61.5 * 24 * 3_600_000;
  const hrefler = await dilimler.evaluateAll((l) => l.map((e) => e.getAttribute("href")!));
  const sira = hrefler.findIndex((h) => {
    const p = baglantiPenceresi(h);
    return Date.parse(p.from) <= an && an <= Date.parse(p.to);
  });
  expect(sira, "kaydın dilimi").toBeGreaterThanOrEqual(0);
  const dilim = baglantiPenceresi(hrefler[sira]);
  expect(dilim.uzunluk, "3 günlük dilim; to, bir sonrakinin başından 1 µs önce").toBe(3 * 24 * 3_600_000 - 1);

  await dilimler.nth(sira).click();
  await expect(page).toHaveURL((url) => url.pathname === "/logs");
  expect(baglantiPenceresi(page.url())).toEqual(dilim);
  await expect(page.locator("tbody tr").first()).toBeVisible();
});

// PB-3, seçili pencereyle "Yeniden dene" (Review 1, F-4): arızadan sonra yeniden yükleme seçili pencereyi korur.
test("0011 PB-3 seçili pencerede arızadan sonra Yeniden dene pencereyi korur", async ({ page }) => {
  await girisYap(page, "/dashboard?pencere=7g");
  const loglarHata = bolum(page, "Önem dereceleri").getByRole("alert");

  await sql("ALTER TABLE log_entries RENAME TO log_entries_e2e_ariza");
  try {
    await page.reload();
    await expect(loglarHata).toContainText("Log sayıları şu anda okunamıyor.");
    await expect(pencereSecimi(page)).toHaveValue("7g");
  } finally {
    await sql("ALTER TABLE log_entries_e2e_ariza RENAME TO log_entries");
  }

  await loglarHata.getByRole("button", { name: "Yeniden dene" }).click();
  await expect(loglarHata).toHaveCount(0);
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "7g");
  await expect(page.getByRole("heading", { name: "Önem dereceleri · Son 1 hafta" })).toBeVisible();
  await expect(page.getByText(/^Son 1 hafta:/)).toBeVisible();
  await expect(pencereSecimi(page)).toHaveValue("7g");
});

/** "25.11 14:00–30.11 14:00: Critical 2, Error 1.204, …" ya da "14:00–15:00: …" → derece sayıları. */
function dilimDereceleri(ad: string): Record<string, number> {
  const eslesme = ad.match(/: Critical ([\d.]+), Error ([\d.]+), Warning ([\d.]+), Info ([\d.]+) kayıt, log listesinde aç$/);
  expect(eslesme, `Beklenmeyen dilim adı: ${ad}`).not.toBeNull();
  const [, critical, error, warning, info] = eslesme!.map((s) => s?.replaceAll(".", ""));
  return { Critical: Number(critical), Error: Number(error), Warning: Number(warning), Info: Number(info) };
}

// CB-3: dilim sayıları tablodandır; bir günden kısa dilimde eksen saati, bir gün ve üstünde tarihi gösterir; uzun
// pencerede dilimin adı tarih ve saat yazar; dilim toplamı derece sayısına eşittir; dar ekranda yatay kayma yoktur.
test("0011 CB-3 dilimler: 48 saatte 24 saatli, 1 yılda 73 tarihli; toplamlar tutar ve dar ekranda taşmaz", async ({ page }) => {
  await kayitEkle({ servis: yeniServisAdi(), alinma: saatOnce(30), level: "Warning", message: "dilim" });
  await girisYap(page, "/dashboard?pencere=48s");

  const ikiGun = bolum(page, "Son 48 saat");
  const ikiGunDilimleri = ikiGun.getByRole("list", { name: /^Dağılım/ }).getByRole("link");
  await expect(ikiGunDilimleri).toHaveCount(24);
  await expect(ikiGunDilimleri.first()).toHaveAccessibleName(/^\d\d\.\d\d \d\d:\d\d–\d\d\.\d\d \d\d:\d\d: Critical /);
  await expect(ikiGun.locator("ol[aria-hidden] time").first()).toHaveText(/^\d\d:\d\d$/);

  await page.setViewportSize({ width: 320, height: 800 });
  await pencereSecimi(page).selectOption({ label: "Son 1 yıl" });
  const yil = bolum(page, "Son 1 yıl");
  const yilDilimleri = yil.getByRole("list", { name: /^Dağılım/ }).getByRole("link");
  await expect(yilDilimleri).toHaveCount(73);
  await expect(yil.locator("ol[aria-hidden] time").first()).toHaveText(/^\d\d\.\d\d$/);
  // En fazla 8 etiket; son etiket aralığına sığmayan etiket çizilmez (73 dilim, her 10'da bir: 7 etiket).
  await expect(yil.locator("ol[aria-hidden] time")).toHaveCount(Math.floor(73 / Math.ceil(73 / 8)));
  expect(await yil.evaluate((el) => el.scrollWidth - el.clientWidth), "bölüm içeriği yatay taşmaz").toBeLessThanOrEqual(0);

  const toplamlar: Record<string, number> = { Critical: 0, Error: 0, Warning: 0, Info: 0 };
  for (const ad of await yilDilimleri.evaluateAll((l) => l.map((e) => e.getAttribute("aria-label")!))) {
    for (const [derece, sayi] of Object.entries(dilimDereceleri(ad))) toplamlar[derece] += sayi;
  }
  for (const derece of Object.keys(toplamlar)) {
    const ad = (await bolum(page, "Önem dereceleri").getByRole("link", { name: new RegExp(`^${derece} `) }).textContent())!;
    expect(toplamlar[derece], derece).toBe(Number(ad.match(/([\d.]+)/)![1].replaceAll(".", "")));
  }
});

// CB-5: seçim değişince log bölümleri kendi yükleniyor durumunu gösterir; seçim kontrolü kullanılabilir kalır ve önceki
// pencerenin sonucu ekrana gelmez.
test("0011 CB-5 yeni seçimde yükleniyor görünür, kontrol kullanılabilir kalır, eski sonuç gelmez", async ({ page }) => {
  await girisYap(page, "/dashboard");
  await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();

  const kilit = sqlInBackground("BEGIN; LOCK TABLE log_entries IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(5); COMMIT;");
  await expect
    .poll(() => sql("SELECT count(*) FROM pg_locks l JOIN pg_class c ON c.oid = l.relation WHERE c.relname = 'log_entries' AND l.mode = 'AccessExclusiveLock' AND l.granted"))
    .toBe("1");

  await pencereSecimi(page).selectOption({ label: "Son 2 hafta" });
  await expect(bolum(page, "Önem dereceleri").getByRole("status").filter({ hasText: "Önem dereceleri yükleniyor…" })).toBeVisible();
  await expect(bolum(page, "Son hatalar").getByRole("status").filter({ hasText: "Son hatalar yükleniyor…" })).toBeVisible();
  await expect(page.getByText(/^Son 24 saat:/)).toHaveCount(0);
  await expect(pencereSecimi(page)).toBeEnabled();

  // Yükleme sürerken başka bir seçim yapılır; ekrana yalnızca son seçimin sonucu gelir.
  await pencereSecimi(page).selectOption({ label: "Son 6 ay" });
  await kilit;
  await expect(page.getByText(/^Son 6 ay:/)).toBeVisible();
  await expect(page.getByText(/^Son 2 hafta:/)).toHaveCount(0);
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "180g");
});

/** Europe/Istanbul'da (testlerin saat dilimi) bugünden `gun` gün önce, `YYYY-MM-DD`. */
function istanbulGunu(gun: number): string {
  return new Intl.DateTimeFormat("sv-SE", { timeZone: "Europe/Istanbul" }).format(new Date(Date.now() - gun * 86_400_000));
}

// CB-7: serbest aralık. Varsayılan alanlar, uygulanan aralığın adreste yalnızca tarihlerle durması, yerel gün
// sınırları, hatalı aralıkta mesaj ve verinin değişmemesi, başlıkların tarihlerle yazılması, Yenile'nin aralığı
// koruması.
test("0011 CB-7 serbest tarih aralığı: varsayılanlar, yerel gün sınırları, hatalı aralıkta mesaj, adres ve Yenile", async ({ page }) => {
  const servis = yeniServisAdi();
  await eskiHatalarEkle(servis, `E2E.Aralik${Date.now()}Exception`, 1000, 10 * 24);
  await girisYap(page, "/dashboard");

  await pencereSecimi(page).selectOption({ label: "Tarih aralığı seç" });
  const baslangic = page.getByLabel("Başlangıç tarihi");
  const bitis = page.getByLabel("Bitiş tarihi");
  await expect(baslangic).toHaveValue(istanbulGunu(7));
  await expect(bitis).toHaveValue(istanbulGunu(0));
  // Aralık uygulanmadan veri değişmez.
  await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();

  // Hatalı aralıklar uygulanmaz: mesaj alanın yanında, veri ve adres değişmez.
  for (const [b, s, alan, mesaj] of [
    [istanbulGunu(3), istanbulGunu(5), bitis, "Bitiş tarihi başlangıç tarihinden önce olamaz."],
    [istanbulGunu(400), istanbulGunu(0), bitis, "Tarih aralığı en fazla 365 gün olabilir."],
    [istanbulGunu(-2), istanbulGunu(-1), baslangic, "Başlangıç tarihi gelecekte olamaz."],
  ] as const) {
    await baslangic.fill(b);
    await bitis.fill(s);
    await page.getByRole("button", { name: "Uygula" }).click();
    await expect(alan).toHaveAccessibleDescription(mesaj);
    await expect(alan).toHaveAttribute("aria-invalid", "true");
    await expect(page).toHaveURL(appUrl("/dashboard"));
    await expect(page.getByText(/^Son 24 saat:/)).toBeVisible();
  }

  const bas = istanbulGunu(12);
  const son = istanbulGunu(8);
  await baslangic.fill(bas);
  await bitis.fill(son);
  await page.getByRole("button", { name: "Uygula" }).click();
  await expect(page).toHaveURL((url) => url.searchParams.get("pencere") === "aralik" && url.searchParams.get("baslangic") === bas && url.searchParams.get("bitis") === son && !url.searchParams.has("from"));
  const servisBaglantisi = bolum(page, "En çok hata üreten servisler").getByRole("link", { name: `${servis}: 1.000 hata, log listesinde aç` });
  await expect(servisBaglantisi).toBeVisible();

  // Gün sınırları Europe/Istanbul'a göredir (UTC+3): başlangıç günü 00:00; bitiş gününün sonu dahil, ertesi gün hariç.
  const pencere = baglantiPenceresi((await servisBaglantisi.getAttribute("href"))!);
  expect(new Date(pencere.from).toISOString()).toBe(new Date(`${bas}T00:00:00+03:00`).toISOString());
  expect(Date.parse(pencere.to)).toBe(Date.parse(`${son}T00:00:00+03:00`) + 86_400_000 - 1);
  // 5 gün → 6 saatlik dilimler (7 günlük satır): 20 dilim.
  await expect(page.getByRole("list", { name: /^Dağılım/ }).getByRole("link")).toHaveCount(20);
  await expect(page.getByRole("heading", { name: /^Önem dereceleri · \d+ .+ – \d+ .+ \d{4}$/ })).toBeVisible();

  // Yenileme ve "Yenile" aralığı korur.
  await page.reload();
  await expect(page.getByLabel("Başlangıç tarihi")).toHaveValue(bas);
  await expect(servisBaglantisi).toBeVisible();
  await page.getByRole("button", { name: "Yenile" }).click();
  await expect(page).toHaveURL((url) => url.searchParams.get("baslangic") === bas && url.searchParams.get("bitis") === son);
  await expect(servisBaglantisi).toBeVisible();
});

// CB-7, adres paylaşılınca (Review 1, F-2): saat dilimi çerezi olmayan ilk ziyarette, kullanıcının yerel "bugün"ünü
// gösteren aralık 24 saate düşmez; çerez yazılır, sayfa yenilenir ve aralık kullanıcının dilimiyle açılır. Tarayıcı
// UTC+14'tedir: UTC saati 10:00'dan sonra yerel gün UTC gününün önündedir ve düzeltmesiz sürüm burada 24 saati gösterir.
test("0011 CB-7 çerezsiz ilk ziyarette paylaşılan bugün aralığı kullanıcının diliminde açılır", async ({ browser }) => {
  const baglam = await browser.newContext({ locale: "tr-TR", timezoneId: "Etc/GMT-14" });
  try {
    const page = await baglam.newPage();
    const bugun = new Intl.DateTimeFormat("sv-SE", { timeZone: "Etc/GMT-14" }).format(new Date());
    await girisYap(page, `/dashboard?pencere=aralik&baslangic=${bugun}&bitis=${bugun}`);

    const [y, m, d] = bugun.split("-").map(Number);
    const ad = `${d} ${["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"][m - 1]} ${y}`;
    await expect(page.getByRole("heading", { name: `Önem dereceleri · ${ad}` })).toBeVisible();
    await expect(page.getByLabel("Zaman penceresi")).toHaveValue("aralik");
    await expect(page).toHaveURL((url) => url.searchParams.get("baslangic") === bugun && url.searchParams.get("bitis") === bugun);
    expect((await baglam.cookies()).find((c) => c.name === "aihub_tz")?.value).toBe(encodeURIComponent("Etc/GMT-14"));
  } finally {
    await baglam.close();
  }
});

// CB-8: son hatalar ve son uyarılar ayrı bölümlerde; satır ayrıntıyı açar; "Tümünü gör" listeyi pencere ve derecelerle
// açar; mesaj düz metindir (HTML yorumlanmaz).
test("0011 CB-8 son hatalar ve son uyarılar: en yeni üstte, ayrıntıya geçiş, Tümünü gör, düz metin", async ({ page }) => {
  const servis = yeniServisAdi();
  const isaret = `e2e-${Date.now()}`;
  await kayitEkle({ servis, alinma: new Date(Date.now() - 2_000), level: "Critical", message: `<b>${isaret}-kritik</b>\nikinci satır` });
  await kayitEkle({ servis, alinma: new Date(Date.now() - 1_000), level: "Warning", message: `${isaret}-uyari ${"x".repeat(150)}` });
  await girisYap(page, "/dashboard");

  const hatalar = bolum(page, "Son hatalar");
  const uyarilar = bolum(page, "Son uyarılar");
  const hataSatiri = hatalar.getByRole("link", { name: new RegExp(`${isaret}-kritik`) });
  await expect(hataSatiri).toBeVisible();
  await expect(hataSatiri).toContainText(`<b>${isaret}-kritik</b>…`);
  await expect(hatalar.locator("b")).toHaveCount(0);
  await expect(hataSatiri).toContainText("Critical");
  await expect(hataSatiri).toContainText(servis);
  expect(await hatalar.getByRole("listitem").count()).toBeLessThanOrEqual(10);
  await expect(uyarilar.getByRole("link", { name: new RegExp(`${isaret}-kritik`) })).toHaveCount(0);

  const uyariSatiri = uyarilar.getByRole("link", { name: new RegExp(`${isaret}-uyari`) });
  await expect(uyariSatiri).toBeVisible();
  const onizleme = (await uyariSatiri.locator("span").last().textContent())!;
  expect(onizleme.endsWith("…")).toBe(true);
  expect(onizleme.length).toBe(121);

  // "Tümünü gör": aynı pencere, Error + Critical.
  const tumu = hatalar.getByRole("link", { name: /^Tümünü gör/ });
  const pencere = baglantiPenceresi((await tumu.getAttribute("href"))!);
  expect(pencere.uzunluk).toBe(24 * 3_600_000);
  await tumu.click();
  await expect(page).toHaveURL((url) => url.pathname === "/logs" && url.searchParams.getAll("level").join() === "Error,Critical");

  // Satır kaydın ayrıntısını açar.
  await page.goBack();
  await hataSatiri.click();
  await expect(page).toHaveURL((url) => url.pathname.startsWith("/logs/"));
  await expect(page.getByText(`<b>${isaret}-kritik</b>`)).toBeVisible();
});

// CB-2, CB-7, CB-8 boş durumlar: pencereyi adıyla söyler. Bu proje henüz hiç kayıt yokken koşar.
test("@bos-depo 0011 boş durumlar seçilen pencereyi ve aralığı adıyla söyler", async ({ page }) => {
  await girisYap(page, "/dashboard?pencere=365g");
  await expect(bolum(page, "Önem dereceleri").getByText("Son 1 yılda kayıt yok")).toBeVisible();
  await expect(bolum(page, "En çok hata üreten servisler").getByText("Son 1 yılda hata yok")).toBeVisible();
  await expect(bolum(page, "Son hatalar").getByText("Son 1 yılda hata yok")).toBeVisible();
  await expect(bolum(page, "Son uyarılar").getByText("Son 1 yılda uyarı yok")).toBeVisible();
  await expect(bolum(page, "Son 1 yıl").getByText("Son 1 yılda kayıt yok")).toBeVisible();

  await page.goto(appUrl("/dashboard?pencere=aralik&baslangic=2026-11-01&bitis=2026-11-15"));
  await expect(bolum(page, "Son uyarılar").getByText("1 Kasım – 15 Kasım 2026 arasında uyarı yok")).toBeVisible();
  await expect(bolum(page, "Önem dereceleri").getByText("1 Kasım – 15 Kasım 2026 arasında kayıt yok")).toBeVisible();
});
