# Spec 0009 — Dashboard zaman penceresi seçimi (mini)

- Status: Approved
- Mode: lite
- Plan: `specs/plans/0009-plan.md`
- Source: sohbet isteği "dashboard sayfasına filtre koy son 24 saat 48 saat 1 hafta 2 hafta 1 ay 3 ay 6 ay 1 yil şeklinde" (harun.ozdek, 2026-11-11)
- Design: yok. Seçim kontrolü aşağıdaki kurallarla tarif edilir; doğrulama önce/sonra ekran görüntüleriyle yapılır.
- Supersedes: 0006 R-2 ("son 24 saat" sabit penceresi) ve "Constraints & out of scope" altındaki "Zaman aralığı seçimi" maddesi; 0007 "Out of scope" altındaki "Zaman aralığı seçimi, dilim boyunu değiştirme" maddesi; 0002 R-8'deki "31 günden uzun zaman aralığı kabul edilmez" sınırı (365 güne çıkar).
- Bağımlılık: 0007 (dashboard grafikleri ve saatlik trend). 0009'un build'i 0007 main'e merge edildikten sonra başlar.
- Clarify kararları ve onay (harun.ozdek, 2026-11-11): 0007 önce biter; log listesi sınırı 365 güne çıkar; uzun
  pencerelerde 10 milyon kayıt / 5 sn; uzunluklar sabit gün (1 ay = 30 gün, 1 yıl = 365 gün). Self-critique 1–4 önerilerle işlendi; spec onaylandı.

## Intent
Dashboard bugün yalnızca son 24 saati gösteriyor. Operasyon ekibi ve geliştiriciler, bir sorunun
dün mü başladığını, haftalık ya da aylık eğilimin nasıl olduğunu görmek için log listesine geçip
filtre denemek zorunda kalıyor. Bu değişiklikle kullanıcı dashboard'un zaman penceresini hazır
seçeneklerden (24 saat, 48 saat, 1 hafta, 2 hafta, 1 ay, 3 ay, 6 ay, 1 yıl) seçer; log bölümleri,
trend grafiği ve ayrıntıya geçiş bağlantıları seçilen pencereye göre çalışır. Serbest tarih aralığı,
karşılaştırma ve otomatik yenileme kapsam dışıdır.

## Changed behavior
- [ ] CB-1 — **Pencere seçimi:** Dashboard'da "Zaman penceresi" etiketli bir açılır liste vardır (self-critique 2). Seçenekler
  ve uzunlukları sabittir (takvim ayı değil):

  | Seçenek | Uzunluk |
  |---|---|
  | Son 24 saat | 24 saat (varsayılan) |
  | Son 48 saat | 48 saat |
  | Son 1 hafta | 7 gün |
  | Son 2 hafta | 14 gün |
  | Son 1 ay | 30 gün |
  | Son 3 ay | 90 gün |
  | Son 6 ay | 180 gün |
  | Son 1 yıl | 365 gün |

  - Pencere, verinin yüklendiği andan geriye seçilen uzunluğu kapsar (alınma zamanına göre, 0006 R-2 ile aynı).
  - Seçim değişince log verisi yeni pencereyle yeniden yüklenir; ekranda seçilen pencere ve yüklenme anı yazılıdır.
  - Seçim sayfa adresinde yer alır: sayfa yenilenince, adres paylaşılınca ve tarayıcının geri tuşuyla
    aynı pencere gelir. Adreste seçim yoksa ya da tanımsız bir değer varsa "Son 24 saat" kullanılır.
  - "Yenile" seçili pencereyi korur; pencerenin bitişi yeni yüklenme anı olur.
  - Giriş yapmamış kullanıcı seçimli bir dashboard adresini açarsa, girişten sonra aynı seçimle dashboard'a
    döner (self-critique 1).
- [ ] CB-2 — **Etkilenen bölümler:** Önem dereceleri, hata üreten servisler (ilk 10), sık exception'lar
  (ilk 5) ve trend grafiği seçilen pencereyi kapsar. Bölüm başlıkları ve boş durum mesajları seçilen
  pencereyi adıyla söyler (ör. "Son 1 haftada kayıt yok"). İzlenen uygulamalar bölümü anlık durumu
  gösterir ve seçimden etkilenmez. Sayılar binlik ayırıcıyla yazılır (ör. 1.234.567) (self-critique 3).
- [ ] CB-3 — **Trend dilimleri:** Trend grafiği pencereyi, başlangıcından itibaren eşit dilimlere böler:

  | Pencere | Dilim | Dilim sayısı |
  |---|---|---|
  | 24 saat | 1 saat | 24 |
  | 48 saat | 2 saat | 24 |
  | 7 gün | 6 saat | 28 |
  | 14 gün | 12 saat | 28 |
  | 30 gün | 1 gün | 30 |
  | 90 gün | 3 gün | 30 |
  | 180 gün | 6 gün | 30 |
  | 365 gün | 5 gün | 73 |

  - Bir derecenin bütün dilimlerdeki sayılarının toplamı, "Önem dereceleri" bölümündeki sayıya eşittir.
  - Her dilimin başlangıç ve bitişi metinde (ekran okuyucu ve ipucu) yazılıdır. Bir günden kısa
    dilimlerde saat, bir gün ve daha uzun dilimlerde tarih gösterilir.
  - Eksen etiketleri ekranı yatay kaydırmaz; her seçenekte etiketler okunur aralıkla seyreltilir.
- [ ] CB-4 — **Ayrıntıya geçiş:** Derece, servis, exception sayıları ve trend dilimleri, log listesini
  seçilen pencerenin (ya da dilimin) başlangıç ve bitişiyle açar; listedeki kayıt sayısı tıklanan sayıya
  eşittir. Log listesi bunun için en fazla 365 günlük aralığı kabul eder; 365 günden uzun aralık 0002
  R-8'deki gibi okunabilir bir mesajla reddedilir.
- [ ] CB-5 — **Performans:** Pencerede 10 milyon kayıt varken (seçenek ne olursa olsun) dashboard'un
  log verisi en geç 5 saniyede gelir. Yüklenirken her log bölümü kendi yükleniyor durumunu gösterir ve
  seçim kontrolü kullanılabilir kalır; yeni seçim önceki yüklemenin sonucunu ekrana getirmez. Ölçümün nasıl
  yapılacağına plan karar verir (self-critique 4).
- [ ] CB-6 — **Erişilebilirlik:** Seçim kontrolü etiketlidir, yalnızca klavyeyle değiştirilir, odak
  görünürdür; pencere değişince ekran okuyucu yeni pencereyi duyar. Otomatik erişilebilirlik denetimi
  her seçenekte WCAG 2.1 AA ihlali bulmaz.

## Preserved behavior
- [ ] PB-1 — "Son 24 saat" seçiliyken (varsayılan) dashboard 0006 ve 0007'deki gibi davranır: aynı
  sayılar, sıralamalar, 24 saatlik dilimler, bağlantılar, boş, hata ve yükleniyor durumları.
- [ ] PB-2 — 0006 AC-12 aynen geçerlidir: son 24 saatte 1 milyon kayıt varken "Son 24 saat"
  penceresinde log verisi en geç 2 saniyede gelir.
- [ ] PB-3 — Log ve uygulama bölümlerinin arızaları birbirinden bağımsızdır (0006 AC-8); "Yeniden dene"
  seçili pencereyle yeniden yükler.
- [ ] PB-4 — Log listesinin 365 güne kadar olan aralıklardaki davranışı (filtreler, sayfalama, sıralama,
  bitişi başlangıcından önce olan aralığın reddi) 0002'deki gibidir.

## Out of scope
- Serbest tarih aralığı (başlangıç ve bitişi kullanıcının seçmesi), takvim ayı/yılı pencereleri.
- Önceki dönemle karşılaştırma ("geçen haftaya göre").
- Seçimin kullanıcıya özel saklanması (bir sonraki girişte hatırlanması); seçim yalnızca adreste durur.
- İzlenen uygulamaların geçmiş durumu ve uptime.
- Otomatik yenileme, canlı akış.
- Retention: saklama süresinden eski kayıtlar zaten silindiği için pencere yalnızca saklanan kayıtları
  sayar; ekranda ayrıca uyarı gösterilmez.

## Definition of Done
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots)
- [ ] Spec moved to `specs/done/` (immutable there)
