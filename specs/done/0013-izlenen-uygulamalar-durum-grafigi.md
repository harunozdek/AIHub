# Spec 0013 — İzlenen uygulamalar: durum dağılımı grafiği (mini)

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0013-plan.md`
- Source: sohbet isteği "izlenen-uygulamalar bir tane dashboard ekle pie tipinde kaç uygulama ayakta kaç uygulama down gözüksün pasta dilimi olarak" (harun.ozdek, 2026-11-26); triage: CHANGE (0012'nin ekranına bir öğe eklenir, mevcut davranış değişmez).
- Design: yok. Tasarım bağlantısı verilmedi; mevcut panel stili kullanılır: 0007'deki "Önem dereceleri" halka grafiği ve 0012'deki durum simgeleri ve renkleri. Görsel uydurulmaz; doğrulama önce/sonra ekran görüntüleriyle yapılır (S-7).
- Supersedes: 0012 "Out of scope" altındaki "Durum geçmişi, uptime yüzdesi, grafik" maddesinin "grafik" kısmı, yalnızca anlık durum dağılımı için. Durum geçmişi ve uptime kapsam dışı kalır.
- Clarify kararları (harun.ozdek, 2026-11-26), S-1…S-7 önerilerle kabul edildi:
  - **S-1:** Grafik "İzlenen uygulamalar" ekranında, "Uygulama ekle" formunun altında, Ayakta ve Down bölümlerinin üstünde.
  - **S-2:** Üç dilim: Ayakta, Down ve Bilinmiyor. Bilinmiyor dilimi ayrıdır; Ayakta bölümünün başlığı Bilinmiyorları saymaya devam eder.
  - **S-3:** 0007'deki gibi halka (ortası boş pasta) görünümü, ortasında toplam uygulama sayısı.
  - **S-4:** Yanında açıklama: her durum için simge, ad, sayı ve yüzde; dilimlerin üzerine metin yazılmaz.
  - **S-5:** Grafik listeyle aynı veriden, aynı anda güncellenir.
  - **S-6:** Hiç uygulama yokken grafik yok; tek durumda tam halka; dilimlere tıklanmaz; dar ekranda taşmaz.
  - **S-7:** Tasarım bağlantısı yok.

## Intent
İzlenen uygulamalar ekranını açan operasyon ekibi, kaç uygulamanın ayakta ve kaç uygulamanın down
olduğunu bugün bölüm başlıklarındaki sayılardan okuyor; genel durumu bir bakışta görmek istiyor. Bu
değişiklikle ekrana, izlenen uygulamaların durumlara göre dağılımını gösteren bir pasta (halka)
grafik eklenir. Her dilimin sayısı ve oranı metinle de yazılır ve grafik liste gibi sayfa
yenilenmeden güncellenir. Durum geçmişi, zaman içindeki grafik ve dilimden süzme yapılmaz.

## Changed behavior
- [x] CB-1 — **Durum dağılımı grafiği:** "İzlenen uygulamalar" ekranında, en az bir izlenen uygulama
  varsa, "Durum dağılımı" başlıklı bir bölüm vardır.
  - Bölüm "Uygulama ekle" formunun altında, Ayakta ve Down bölümlerinin üstündedir.
  - Bölümde bir halka (ortası boş pasta) grafik vardır. Grafiğin dilimleri izlenen uygulamaların
    durumlara göre dağılımıdır: Ayakta (yeşil), Down (kırmızı), Bilinmiyor (gri). Renkler 0012'deki
    durum simgeleriyle aynıdır.
  - Her dilimin büyüklüğü, o durumdaki uygulama sayısının toplama oranıdır.
  - Halkanın ortasında toplam izlenen uygulama sayısı ve "uygulama" kelimesi yazar (ör. "13 uygulama").
  - Sayısı 0 olan durumun dilimi çizilmez.
  - Tüm uygulamalar aynı durumdaysa grafik o durumun renginde tam bir halkadır.
- [x] CB-2 — **Açıklama:** Grafiğin yanında üç durum her zaman şu sırayla listelenir: Ayakta, Down,
  Bilinmiyor.
  - Her satırda durumun simgesi (0012'deki), adı, uygulama sayısı ve toplama oranı tam sayıya
    yuvarlanmış yüzde olarak yazar (ör. "Ayakta 10 (%77)", "Down 3 (%23)", "Bilinmiyor 0 (%0)").
  - Yuvarlama yüzdelerin toplamını %100'den saptırabilir; her yüzde kendi başına yuvarlanır.
  - Sayılar bölüm başlıklarıyla tutarlıdır: Down sayısı "Down (n)" başlığına, Ayakta ve
    Bilinmiyor sayılarının toplamı "Ayakta (n)" başlığına eşittir.
  - Grafik bir sayının yerini almaz; dilimler tıklanmaz.
- [x] CB-3 — **Güncelleme:** Grafik ve açıklama, ekrandaki listeyle aynı veriden ve aynı anda güncellenir.
  - Liste 10 saniyede bir güncellendiğinde (0012 CB-6) grafik de güncellenir.
  - Ekleme, düzenleme ve izlemeden çıkarmadan hemen sonra grafik de değişir.
  - Grafik, açıklama ve bölüm başlıklarındaki sayılar hiçbir an birbiriyle çelişmez.
  - Durum okunamazsa grafik son bilinen hâli gösterir; 0012'deki "Durum güncellenemedi" notu grafik
    için de geçerlidir.
  - Grafiğin güncellenmesi açık formu, girilen değeri ve odağı bozmaz (0012 CB-6).
- [x] CB-4 — **Boş durum ve dar ekran:**
  - Hiç izlenen uygulama yoksa grafik bölümü gösterilmez; bugünkü "Henüz izlenen uygulama yok"
    durumu aynen kalır.
  - Dar ekranda (320 px) grafik bölümü Ayakta ve Down bölümlerinin üstünde durur; grafik ve
    açıklama taşmaz, alt alta gelebilir.
- [x] CB-5 — **Erişilebilirlik:**
  - Ekran okuyucu grafiği tek bir özet olarak okur (ör. "Durum dağılımı: 13 uygulama; Ayakta 10,
    Down 3, Bilinmiyor 0"); dilimler ayrıca okunmaz, bilgi açıklamada metin olarak vardır.
  - Durumlar yalnızca renkle ayrılmaz: açıklamada simge ve ad vardır.
  - Dilimler arasında zemin renginde boşluk vardır; her dilim zemine karşı metin dışı içerik için 3:1
    karşıtlık sağlar, açık ve koyu temada; otomatik erişilebilirlik denetimi WCAG 2.1 AA ihlali bulmaz.
    (Revizyon 1, harun.ozdek, 2026-11-26, plan 0013 risk 1: "komşu dilime karşı 3:1" simge renkleriyle
    (CB-1) ve üç komşu dilimle sağlanamıyordu.)
  - Grafik değişiklikleri ayrıca duyurulmaz; Down'a giriş ve çıkış duyurusu 0012 CB-7'deki gibi kalır.

## Preserved behavior
- [x] PB-1 — 0012'nin ekran davranışı aynen geçerlidir: durum simgeleri, Ayakta ve Down bölümleri
  (genişlik oranları, dar ekranda sıra, başlıklardaki sayılar, sıralama), gösterilen bilgiler ve boş
  durumlar (0012 CB-1…CB-4).
- [x] PB-2 — 0012'nin canlı güncellemesi ve erişilebilirliği aynen geçerlidir: 10 saniyelik
  güncelleme, açık form, değer, hata, odak ve onayın korunması, "Durum güncellenemedi" notu, Down'a
  giriş ve çıkış duyuruları (0012 CB-6, CB-7).
- [x] PB-3 — Ekleme, düzenleme, izlemeden çıkarma, denetim kuralları, Down uyarısı, yetki,
  yükleniyor ve hata durumları 0012 PB-1…PB-4'teki gibidir.
- [x] PB-4 — Dashboard ve 0008 değişmez: dashboard'un "İzlenen uygulamalar" bölümü ve oran çubuğu
  (0006 AC-5, 0007 CB-3) ve AIHub'ın kendi sağlık denetimi (0008) bugünkü gibidir.

## Out of scope
- Dashboard'a (ana sayfa) ayrıca bir durum grafiği eklemek; dashboard'daki oran çubuğunu değiştirmek.
- Durum geçmişi, uptime yüzdesi, zaman içindeki grafik.
- Dilime tıklayarak süzme, bölüme kaydırma ya da başka bir etkileşim.
- Grafiğin rengini, türünü ya da yerini kullanıcının değiştirmesi.
- Panelin üst menüsünün 320 px'te yatay taşması (0012'deki gibi ayrı bir CHANGE).

## Definition of Done
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots)
- [x] Spec moved to `specs/done/` (immutable there)
