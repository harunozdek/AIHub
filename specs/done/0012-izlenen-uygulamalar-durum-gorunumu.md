# Spec 0012 — İzlenen uygulamalar: durum simgeleri, iki bölüm ve 10 saniyelik güncelleme (mini)

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0012-plan.md`
- Source: sohbet isteği "izlenen-uygulamalar sayfasında uygulama ayaktaysa görsel olarak yeşil aktif oldugunu gösteren bir icon koy down oldugunda da bununla ilgili bir icon koy ekranı 3/2 si deaktifler gözüksün 3/1 de ise down olan uygulamalar gözüksun uygulama her 10 sn de bir health bilgisini kontrol ederek ekranda durumları güncellensin aktif servislerin listelendiği yerde ekleyen, son değiştiren bilgisini kaldır" (harun.ozdek, 2026-11-26); triage: CHANGE (0005'in ekranı, denetim aralığı ve yoklama aralığı değişir).
- Design: yok. Tasarım bağlantısı verilmedi; mevcut panel stili (0005, 0006, 0007) kullanılır, görsel uydurulmaz. Doğrulama önce/sonra ekran görüntüleriyle yapılır (harun.ozdek, 2026-11-26).
- Supersedes:
  - 0005 R-4 "30 saniyede bir" denetim aralığı ve "15 saniyeden kısa olmaz" alt sınırı.
  - 0005 R-7 ve AC-8 "15 saniyede bir yeniden okur" ve "15 saniye içinde".
  - 0005 AC-7 "en geç iki dakika içinde".
  - 0005 R-8'in ekranda gösterilen bilgiler listesi; "ekleyen" ve "son değiştiren" ekrandan kalkar.
  - 0005 "Liste" durumu ve AC-10'daki "Down satırları listenin en üstündedir": Down olanlar artık kendi bölümünde.
  - 0005 "Sınırlar" altındaki "Denetim aralığı (30 sn) … panelin yoklama aralığı (15 sn)".
  - 0005 AC-2 ve AC-9'daki "ekleyen ve zamanla görünür" ve "son değiştiren kullanıcı ve zaman listede görünür" ifadeleri.
  - 0008 R-6 ve kabul kriterindeki "ekleyen 'Sistem' görünür" ifadesi: AIHub'ın kendi kayıtlarında da ekleyen ekranda gösterilmez; "Sistem" olarak saklanmaya devam eder (BUILD sırasında R-10, harun.ozdek, 2026-11-26).
- Clarify kararları (harun.ozdek, 2026-11-26), S-1…S-7 önerilerle kabul edildi:
  - **S-1:** "2/3 deaktifler" isteği "2/3 ayaktakiler" olarak okundu.
  - **S-2:** Bilinmiyor durumundakiler Ayakta bölümünde gösterilir.
  - **S-3:** Denetim de, ekran da, uyarı da 10 saniyede bir çalışır.
  - **S-4:** "Ekleyen" ve "son değiştiren" iki bölümden de kalkar; bilgi saklanmaya devam eder.
  - **S-5:** Her bölümün sütunları CB-3'teki gibidir.
  - **S-6:** Her bölümün kendi boş durumu vardır.
  - **S-7:** Otomatik güncelleme açık formu bozmaz; durum değişiklikleri duyurulur; okunamazsa "durum güncellenemedi" notu gösterilir.
- Self-critique kararları (harun.ozdek, 2026-11-26), E-1…E-6 önerilerle kabul edildi:
  - **E-1:** Spec tek tutulur (bkz. Out of scope, Split).
  - **E-2:** 200 uygulamada en kötü durum plan aşamasında ölçülür; kural tutmazsa R-08.
  - **E-3:** Bölüm başlıklarında uygulama sayısı yazar.
  - **E-4:** Yalnızca ekran açıkken olan değişiklikler duyurulur, ilk açılış duyurulmaz.
  - **E-5:** Ekleme ve izlemeden çıkarma listeye hemen yansır.
  - **E-6:** Down uyarısı bu ekranda da görünmeye devam eder.
- Spec onayı: harun.ozdek, 2026-11-26.

## Intent
Kullanıcı izlenen uygulamalar ekranında hangi uygulamanın ayakta, hangisinin down olduğunu bugün
tablodaki bir metinden okuyor. Sorunlu uygulamayı bir bakışta görmek istiyor.

Bu değişiklikle her uygulamanın durumu bir simgeyle de gösterilir. Ekran iki bölüme ayrılır: geniş
bölümde ayaktaki uygulamalar, dar bölümde down olanlar listelenir. AIHub uygulamaları 10 saniyede bir
denetler ve ekran sayfa yenilenmeden 10 saniyede bir güncellenir. Listeden "ekleyen" ve "son
değiştiren" bilgisi kalkar.

## Changed behavior
- [x] CB-1 — **Durum simgesi:** Her uygulamanın durum metninin yanında bir simge vardır.
  - Ayakta: yeşil, "aktif" anlamı taşıyan bir simge (onay işareti).
  - Down: kırmızı, sorun anlamı taşıyan bir simge (çarpı ya da ünlem).
  - Bilinmiyor: gri, belirsizlik anlamı taşıyan bir simge (soru işareti).

  Simgeler biçimleriyle de birbirinden ayrılır; renk tek ayırt edici değildir. Durum metni
  (Ayakta / Down / Bilinmiyor) her zaman yazılıdır. Simge, metnin yerine geçmez ve ekran okuyucuda
  metin bir kez okunur.
- [x] CB-2 — **İki bölüm:** Ekranda en az bir izlenen uygulama varsa liste iki başlıklı bölüm olarak
  görünür: "Ayakta" ve "Down".
  - **Geniş ekran** (masaüstü): bölümler yan yanadır. "Ayakta" bölümü genişliğin 2/3'ünü, "Down"
    bölümü 1/3'ünü kaplar.
  - **Dar ekran** (telefon): bölümler alt alta gelir; "Down" bölümü üsttedir. 320 px genişlikte
    bölümler ve içerikleri (uzun adresler dahil) taşmaz. Panelin üst menüsünün dar ekranda taşması
    bu değişiklikten önce de vardır ve kapsam dışıdır (BUILD sırasında R-10, harun.ozdek, 2026-11-26).
    Sayfa dar ekranda ilk açılırken, tarayıcıdaki kod yüklenene kadar Ayakta bölümü bir an tablo olarak
    görünebilir; yavaş bağlantıda bu kısa süre içinde tablo kendi içinde kayabilir. Kod yüklenince kart
    olur ve taşmaz (Review 2 B-2, kabul edilen sapma; Review 3 B3-1, harun.ozdek, 2026-11-26).
  - **"Ayakta" bölümü:** Ayakta ve Bilinmiyor durumundaki uygulamalar, ServiceName'e göre.
  - **"Down" bölümü:** Yalnızca Down durumundaki uygulamalar, en uzun süredir Down olan üstte.
  - Her bölümün başlığında içindeki uygulama sayısı yazar (ör. "Ayakta (12)", "Down (2)").
  - "Uygulama ekle" formu bölümlerin üstünde, bugünkü yerinde kalır.
- [x] CB-3 — **Gösterilen bilgiler:**
  - **"Ayakta" bölümünde her uygulama için:** simge ve durum metni, ServiceName, sağlık adresi, son
    denetim zamanı, "Düzenle" ve "İzlemeden çıkar".
  - **"Down" bölümünde her uygulama için:** simge ve durum metni, ServiceName, sağlık adresi, Down
    olduğu zaman, son başarısızlığın nedeni, son denetim zamanı, "Düzenle" ve "İzlemeden çıkar".
  - Hiçbir bölümde "ekleyen" ve "son değiştiren" bilgisi (kullanıcı ve zaman) gösterilmez. Bu bilgi
    saklanmaya devam eder; yalnızca ekrandan kalkar.
  - Zamanlar bugünkü gibi yerel saatle ve saat dilimiyle yazılır. Uzun adresler bölümün dışına
    taşmaz.
- [x] CB-4 — **Bölümlerin boş durumları:**
  - Down uygulama yoksa "Down" bölümünde yeşil onay simgesiyle "Down olan uygulama yok" yazar.
  - Ayakta ya da Bilinmiyor uygulama yoksa "Ayakta" bölümünde "Ayakta uygulama yok" yazar.
  - Hiç izlenen uygulama yoksa iki bölüm de görünmez; bugünkü "Henüz izlenen uygulama yok" durumu
    görünür.
- [x] CB-5 — **10 saniyelik denetim:** AIHub her izlenen uygulamanın adresine 10 saniyede bir istek
  gönderir.
  - Aynı adres için iki denetim arası, denetimler gecikse de 5 saniyeden kısa olmaz.
  - Adres değişince ilk denetim hemen yapılır.
  - Zaman aşımı (5 sn) ve Down eşiği (art arda 3 başarısız denetim) değişmez.
  - Duran bir uygulama, durduktan sonra en geç 1 dakika içinde panelde Down görünür.
  - 200 uygulama izlenirken her uygulama yine her aralıkta bir kez denetlenir. Birden çok AIHub
    örneği çalışsa da bir aralıkta en fazla bir denetim sayılır.
- [x] CB-6 — **Ekranın kendiliğinden güncellenmesi:** "İzlenen uygulamalar" ekranı açıkken durumlar
  sayfa yenilenmeden 10 saniyede bir güncellenir.
  - Bir uygulama Down olunca 10 saniye içinde "Down" bölümüne geçer; Ayakta olunca "Ayakta"
    bölümüne döner.
  - Simge, durum, son denetim zamanı, neden, Down olduğu zaman ve bölüm sayıları da güncellenir.
  - Her ekrandaki Down uyarısı (0005 R-7) da 10 saniyede bir güncellenir: uygulama Down olunca ya da
    düzelince 10 saniye içinde uyarıya girer ya da uyarıdan çıkar.
  - **Kullanıcının elindeki iş korunur:**
    - Açık "Uygulama ekle" ya da "Düzenle" formu, içine yazılanlar, odak ve hata mesajları güncelleme
      sırasında kaybolmaz.
    - Düzenlenen uygulama güncelleme sırasında başka bölüme geçerse form açık kalır ve girilen değer
      korunur.
    - "İzlemeden çıkar" onayı açıkken onay kapanmaz.
  - Ekleme, düzenleme ve izlemeden çıkarma listeye hemen yansır; 10 saniyelik güncelleme beklenmez
    (E-5).
  - **Durum okunamazsa** ekran son bilinen hâli gösterir ve "Durum güncellenemedi" notu belirir.
    Okuma yeniden başarılı olunca not kalkar. Bu durumda ilk açılıştaki hata ekranı ("yeniden dene")
    gösterilmez.
- [x] CB-7 — **Erişilebilirlik:**
  - Bir uygulama Down olduğunda ya da Down'dan çıktığında değişiklik ekran okuyucuya duyurulur (ör.
    "demo-odeme Down oldu", "demo-odeme Ayakta"). Değişiklik yokken her güncellemede duyuru yapılmaz;
    ekranın ilk açılışında mevcut durumlar duyurulmaz (E-4).
  - Bölüm başlıkları başlık olarak okunur; bölümler adlarıyla (Ayakta, Down) gezilir.
  - Simgelerle birlikte ekranda WCAG 2.1 AA ihlali yoktur: simgeler metin dışı içerik için 3:1
    karşıtlık sağlar, açık ve koyu temada.

## Preserved behavior
- [x] PB-1 — 0005'in ekleme, doğrulama, düzenleme ve izlemeden çıkarma davranışı aynen geçerlidir:
  - AC-2: ekleme ve aynı ServiceName reddi.
  - AC-3: adres kuralları.
  - AC-9: adres değişince Bilinmiyor durumuna dönüş, izlemeden çıkarma onayı, aynı adresle
    kaydetmenin durumu ve sayacı korunması.
  - AC-11: 200 uygulama sınırı.
  - AC-12: bu işlemlerin yalnızca klavyeyle tamamlanması.

  "Ekleyen ve zaman" ile "son değiştiren" bilgisinin ekranda görünmesi bu maddelerden çıkar (CB-3).
  Bilgi saklanmaya devam eder.
- [x] PB-2 — 0005'in denetim ve durum kuralları 10 saniyelik aralıkla aynen geçerlidir:
  - 2xx başarı, başarısızlık türleri ve nedenleri (AC-4, AC-5, R-6).
  - Art arda 3 başarısız denetimle Down; tek başarıyla Ayakta (AC-6).
  - Denetim panelde oturum açık olmadan da sürer.
  - Durumlar AIHub yeniden başlatılınca korunur (AC-7).
- [x] PB-3 — Down uyarısı 0005 R-7 / AC-8'deki gibi çalışır, yalnız yoklama aralığı 10 saniyedir:
  - Oturum açıkken her ekranda görünür, giriş ekranında görünmez.
  - Down uygulamaların adlarını ve ne zamandan beri Down olduklarını listeler.
  - Uyarıdaki ad "İzlenen uygulamalar" ekranına götürür.
  - Okunamazsa son bilinen durumu "durum güncellenemedi" notuyla gösterir.
- [x] PB-4 — 0005 AC-1 ve AC-10'un korunan kısmı: oturumsuz kullanıcı ekranı göremez ve değişiklik
  yapamaz. Ekranın ilk açılışında yükleniyor ve hata ("yeniden dene") durumları bugünkü gibidir.
- [x] PB-5 — Dashboard'un "İzlenen uygulamalar" bölümü (0006 AC-5) ve AIHub'ın kendi sağlık denetimi
  (0008) değişmez; yalnızca 0008 R-6'nın "Sistem" görünümü kalkar (Supersedes). 0008'in kaydı da bu ekranda diğer
  uygulamalar gibi simgesi ve bölümüyle görünür; ekleyeni "Sistem" olarak saklanır.

## Out of scope
- Denetim aralığını, zaman aşımını ya da Down eşiğini ekrandan değiştirmek; uygulama başına farklı
  aralık.
- Durum geçmişi, uptime yüzdesi, grafik.
- Panel dışı bildirim (e-posta, Teams, SMS).
- Dashboard'un "İzlenen uygulamalar" bölümünün görünümü.
- "Ekleyen" ve "son değiştiren" bilgisini başka bir yerde (ör. ayrıntı ekranı) göstermek.
- Bölümlerin genişliğini kullanıcının değiştirmesi; arama, filtre, sıralama seçimi.
- Ekran kapalıyken ya da sekme arka plandayken güncelleme davranışı için ayrı kural. Güncelleme
  ekran açıkken 10 saniyede birdir; tarayıcının arka plan sekmeleri yavaşlatması kabul edilir.
- Panelin üst menüsünün 320 px'te yatay taşması (her sayfada, 0007'den beri bilinen; ayrı bir CHANGE).
- Down uyarısını bu ekranda gizlemek (E-6): uyarı bugünkü gibi bu ekranda da görünür.
- **Split:** önerildi — 0012a ekran düzeni (CB-1…CB-4, CB-7), 0012b 10 saniyelik denetim ve canlı güncelleme
  (CB-5, CB-6); tek tutuldu — harun.ozdek, ikisi aynı ekranda aynı isteğe ("durumu bir bakışta ve güncel gör")
  hizmet eder, ayrı teslimde yarım deneyim çıkar (E-1).

## Definition of Done
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots)
- [x] Spec moved to `specs/done/` (immutable there)
