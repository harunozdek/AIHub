# Spec 0007 — Dashboard grafikleri ve saatlik trend (mini)

- Status: Approved
- Mode: lite
- Plan: `specs/plans/0007-plan.md`
- Source: sohbet isteği "dashboard ekranını görsel olarak iyileştir chartlar grafikler ekle" (harun.ozdek, 2026-11-10); triage: CHANGE (harun.ozdek, 2026-11-10)
- Design: yok. Görünüm aşağıdaki kurallarla tarif edilir; doğrulama önce/sonra ekran görüntüleriyle yapılır (harun.ozdek, 2026-11-10). Dilimler pencere başlangıcına göredir (spec onayı).
- Supersedes: 0006, "Constraints & out of scope" altındaki "Grafikler ve zaman içindeki trend" maddesi. 0006'nın hiçbir kabul kriteri değişmez.

## Intent
Dashboard bugün yalnızca sayılar ve tablolar gösteriyor. Kullanıcı son 24 saatte hataların ne
zaman arttığını göremiyor; sayıların birbirine oranını da kafasında hesaplıyor. Bu değişiklikle
dashboard son 24 saatin saatlik dağılımını bir grafikle gösterir. Mevcut sayıların yanına
oranlarını gösteren çubuklar eklenir. Kullanıcı bir saate tıklayarak o saatin kayıtlarına geçer.
Sayılar, bağlantılar ve bölümlerin bugünkü davranışı aynen korunur.

## Changed behavior
- [ ] CB-1 — **Saatlik trend:** Dashboard'da "Son 24 saat" başlıklı yeni bir bölüm var.
  - Dashboard penceresi, başlangıcından itibaren 24 eşit, birer saatlik dilime bölünür.
  - Her dilim için dört LogLevel'in kayıt sayısı, derecelere göre yığılmış tek bir çubukla gösterilir.
  - Kaydı olmayan dilim 0 görünür.
  - Bir derecenin 24 dilimdeki sayılarının toplamı, "Önem dereceleri" bölümündeki sayıya eşittir.
  - Her dilimin başlangıç saati yerel saatle yazılıdır.
- [ ] CB-2 — **Trendden ayrıntıya:** Bir dilime tıklanınca log listesi o dilimin başlangıç ve bitiş
  zamanıyla açılır. Listedeki kayıt sayısı dilimin toplamına eşittir; gerekirse sayfalar boyunca sayılır.
- [ ] CB-3 — **Oran çubukları:** Sayıların yanında, sayıyla orantılı yatay çubuklar görünür:
  - önem derecelerinde her derecenin toplama oranı;
  - hata üreten servislerde ve sık exception'larda her satırın listedeki en büyük değere oranı;
  - izlenen uygulamalarda Ayakta/Down/Bilinmiyor oranı.

  Sayılar ve bağlantılar yerinde kalır; çubuk bir sayının yerini almaz.
- [ ] CB-4 — **Trend bölümünün durumları:**
  - Son 24 saatte hiç kayıt yoksa grafik yerine "Son 24 saatte kayıt yok" yazar.
  - Log verisi yüklenirken trend bölümü kendi başlığı ve yükleniyor satırıyla görünür.
  - Log verisi okunamazsa trend bölümü de hata mesajı ve "yeniden dene" gösterir.
  - Trend bölümünün arızası izlenen uygulamalar bölümünü etkilemez.
- [ ] CB-5 — **Erişilebilirlik:**
  - Grafikteki her dilim ve her oran çubuğu metinle de okunur: ekran okuyucu her dilimin saatini
    ve derece sayılarını duyar.
  - Dereceler yalnızca renkle ayrılmaz; açıklama (legend) ve metin vardır.
  - Dilimler yalnızca klavyeyle seçilip açılır ve odak görünürdür.
  - Otomatik erişilebilirlik denetimi WCAG 2.1 AA ihlali bulmaz.

## Preserved behavior
- [ ] PB-1 — 0006 AC-2…AC-6 aynen geçerlidir: derece sayıları, hata üreten servisler (ilk 10,
  sıralama), sık exception'lar (ilk 5, son görülme), uygulama sayıları ve Down listesi ile
  sayılardan listeye giden bağlantılar. Bunlar aynı pencere ve filtrelerle çalışır.
- [ ] PB-2 — 0006 AC-7 ve AC-8 aynen geçerlidir:
  - her bölümün boş durumu;
  - log ve uygulama arızalarının birbirinden bağımsız olması;
  - "Yeniden dene" ve "Yenile";
  - her bölümün kendi yükleniyor durumu.
- [ ] PB-3 — 0006 AC-12 trend dahil geçerlidir: son 24 saatte 1 milyon kayıt varken dashboard'un
  log verisi en geç 2 saniyede gelir.
- [ ] PB-4 — 0006 AC-1 ve AC-9 aynen geçerlidir: girişten sonra dashboard açılır; ekranın tamamı
  klavyeyle kullanılır; WCAG 2.1 AA.

## Out of scope
- Zaman aralığı seçimi (1 saat, 7 gün vb.), dilim boyunu değiştirme, karşılaştırma ("düne göre").
- Servis ya da exception türü bazında ayrı trend grafikleri.
- İzlenen uygulamaların geçmiş durum ve uptime grafikleri (0006'da kapsam dışı; değişmedi).
- Otomatik yenileme, canlı akış, grafiği dışa aktarma ya da indirme.
- Kullanıcıya özel dashboard, bölüm ekleme, gizleme ya da yeniden sıralama.
- Tasarımın genel yenilenmesi (menü, renk teması, diğer ekranlar).

## Definition of Done
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots)
- [ ] Spec moved to `specs/done/` (immutable there)
