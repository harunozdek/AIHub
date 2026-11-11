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
