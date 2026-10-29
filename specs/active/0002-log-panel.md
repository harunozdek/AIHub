# Spec 0002 — Log paneli (liste, filtre, ayrıntı)

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0002-plan.md`

## Intent
Kurum içindeki geliştiriciler ve operasyon ekibi, AIHub'a gelen logları tarayıcıdan görmek
istiyor. Bugün kayıtlar yalnızca veritabanında duruyor ve onlara bakmanın bir yolu yok. Başarı
şöyle tanımlanır: kurumsal hesabıyla giriş yapan bir kullanıcı, belirli bir servisin son
hatalarını birkaç tıkta bulur ve kaydın ayrıntısını (maskelenmiş Message, StackTrace, Metadata)
güvenle okur. Panel salt okunurdur. V1'de giriş yapan her kullanıcı tüm servislerin loglarını
görür; servis bazında yetki sonraki bir spec'e kalır (harun.ozdek, 2026-10-27). AI kök neden
analizi, API anahtarı yönetimi, retention, metin araması ve canlı akış bilerek kapsam dışında
bırakıldı.

## Requirements
- **R-1 Giriş zorunlu:** Panel yalnızca kurumsal kimlik sağlayıcısıyla (OIDC) giriş yapmış
  kullanıcıya açılır. Giriş yapmamış kullanıcı giriş akışına yönlendirilir ve hiçbir log
  içeriği görmez. Oturumun süresi dolduğunda kullanıcıdan yeniden giriş istenir; girişten sonra
  bulunduğu sayfaya döner. Kullanıcı istediğinde çıkış yapar; çıkıştan sonra tarayıcının geri
  tuşu log içeriği göstermez. Log okuma yalnızca doğrulanmış bir kullanıcı kimliğiyle yapılabilir;
  servis API anahtarıyla log okunamaz (`docs/security.md`, iki ayrı yüzey).
- **R-2 Liste:** Giriş yapan kullanıcı log kayıtlarını en yeni kayıt önce olacak şekilde, alınma
  zamanına göre sıralı görür. Her satırda alınma zamanı, ServiceName, LogLevel, Message'ın ilk
  satırı (en fazla 200 karakter; uzunsa kısaltıldığı belirtilir) ve varsa ExceptionType bulunur. Varsayılan görünüm son 24 saattir.
- **R-3 Filtre:** Kullanıcı listeyi ServiceName'e (tek servis), LogLevel'e (bir veya birden çok
  değer) ve alınma zamanı aralığına göre daraltır. Filtreler birlikte uygulanır. ServiceName
  seçenekleri, depoda en az bir kaydı olan servislerden oluşur. Etkin filtreler sayfa adresinde
  yer alır; adres paylaşıldığında aynı sonuç görünür.
- **R-4 Sayfalama:** Liste sayfa başına en fazla 50 kayıt gösterir; kullanıcı sonraki ve önceki
  sayfaya geçer. Sayfalar arasında gezinirken yeni kayıtlar gelse de hiçbir kayıt iki kez
  görünmez ve hiçbir kayıt atlanmaz. Liste, açıldığı andaki zaman penceresini gösterir: sonradan
  gelen kayıtlar o listenin sayfalarına girmez, liste yeniden açılınca en başta görünür
  (harun.ozdek, 2026-10-29).
- **R-5 Ayrıntı:** Kullanıcı bir satırı seçince kaydın tüm alanlarını görür: kayıt kimliği,
  ServiceName, LogLevel, Message'ın tamamı, ExceptionType, StackTrace, Metadata (okunur, girintili
  biçimde), servisin bildirdiği zaman ve AIHub'ın aldığı zaman. Zamanlar kullanıcının yerel
  saatinde, saat dilimi belirtilerek gösterilir. Ayrıntı sayfasının adresi paylaşılabilir.
- **R-6 Güvenli gösterim:** Log içeriği saldırganın kontrolündeki metindir. Message, StackTrace,
  ExceptionType, ServiceName ve Metadata her zaman düz metin olarak gösterilir; HTML veya betik
  olarak yorumlanmaz (`docs/security.md`, Input & output). Panel, depodaki maskelenmiş içeriği
  olduğu gibi gösterir.
- **R-7 Salt okunur:** Panel bir kaydı değiştiremez veya silemez (domain invariant).
- **R-8 Geçersiz filtre:** Bitişi başlangıcından önce olan veya 31 günden uzun bir zaman aralığı
  ve tanımsız bir LogLevel kabul edilmez. Kullanıcı nedenini okunabilir bir mesajla görür ve
  liste sorgusu yapılmaz.
- **R-9 Arıza:** Log verisi o an okunamıyorsa kullanıcı anlaşılır bir hata mesajı ve yeniden
  deneme seçeneği görür. Mesajda iç sistem ayrıntısı (bağlantı dizesi, yığın izi, sunucu adı)
  bulunmaz.

## User experience
- **Flow:**
  1. Kullanıcı panelin adresini açar.
  2. Giriş yapmamışsa kurumsal giriş sayfasına yönlendirilir. Girişten sonra log listesine döner.
  3. Liste son 24 saatin kayıtlarını, en yeni önce gösterir.
  4. Kullanıcı ServiceName, LogLevel ve zaman aralığı filtrelerini uygular. Gerekirse sayfalar
     arasında gezinir.
  5. Bir satırı seçer ve kaydın ayrıntısını açar. Ayrıntıdan listeye, filtreleri korunmuş olarak
     geri döner.
- **States:**
  - Giriş: giriş yapılmamış (yönlendirme) · giriş başarısız veya iptal edildi (hata sayfası ve
    "yeniden giriş yap") · oturum süresi doldu (yeniden giriş).
  - Liste: yükleniyor · hiç kayıt yok ("Henüz log kaydı yok") · filtre yok ama son 24 saatte
    kayıt yok ("Son 24 saatte kayıt yok" ve daha eski bir aralık seçme yolu) · filtreyle sonuç
    yok ("Bu filtrelere uyan kayıt yok" ve "filtreleri temizle") · geçersiz filtre (alan yanında
    mesaj) · hata (mesaj ve "yeniden dene") · başarı.
  - Ayrıntı: yükleniyor · kayıt bulunamadı (var olmayan veya bozuk kimlik) · hata (mesaj ve
    "yeniden dene") · başarı. Boş isteğe bağlı alanlar "—" olarak gösterilir.
  - Yetkisiz: V1'de giriş yapan her kullanıcı yetkilidir; ayrı bir "yetki yok" ekranı yoktur.
- **Design reference:** none — the flow above is the design.
- **Accessibility:**
  - Tüm akış (giriş sonrası filtre, sayfalama, satır seçimi, geri dönüş) yalnızca klavyeyle
    yapılabilir; odak her zaman görünürdür.
  - Her form alanının görünür bir etiketi vardır; hata mesajları ilgili alana bağlıdır.
  - LogLevel yalnızca renkle değil, metinle de belirtilir.
  - Kontrast ve yapı WCAG 2.1 AA düzeyindedir.

## Constraints & out of scope
- **Erişim (V1):** Giriş yapan her kurum kullanıcısı tüm servislerin loglarını görür. Rol veya
  servis bazında yetki yoktur (harun.ozdek, 2026-10-27).
- **Teknoloji:** Panel ADR 0001'deki yığınla yazılır: Next.js (App Router) + TypeScript +
  Tailwind. Vite SPA önerisi değerlendirildi ve reddedildi (R-10 kararı, harun.ozdek,
  2026-10-27).
- **Bağımlılık:** Liste ve ayrıntı, Spec 0001'in sakladığı kayıtları okur. Bu iş
  `feature/0001-log-ingestion` üzerine kuruludur; 0001 main'e girdikten sonra rebase edilir.
- **Performans:** 1 milyon kayıtlı bir depoda bir liste sayfası en geç 1 saniyede gelir.
- **Zaman aralığı:** Filtre, AIHub'ın kaydı aldığı zamana uygulanır; servisin bildirdiği zaman
  her kayıtta bulunmaz ve servise bağlıdır. En uzun aralık 31 gündür: retention henüz tanımlı
  değil ve sınırsız bir aralık depoyu zorlar.
- **Kimlik sağlayıcı:** Spec sağlayıcıdan bağımsızdır; authority ortam başına konfigürasyondur
  (`docs/security.md`). Testler yerel bir test kimlik sağlayıcısıyla koşar.
- **Numara:** Spec 0001 anahtar yönetimini "Spec 0002" olarak anar. Numara kuralı (en büyük + 1)
  gereği bu spec 0002'dir; anahtar yönetimi sıradaki boş numarayı alır. Spec 0001'deki ifade, 0001
  ship edilmeden önce "ayrı bir spec" olarak düzeltilir (harun.ozdek, 2026-10-27).
- **Split:** proposed "giriş + liste + filtre" (0002) ve "ayrıntı" (0003) olarak iki spec; kept as
  one — harun.ozdek, ayrıntısız liste tek başına doğrulanabilir bir değer üretmez.
- **Arayüz dili:** Türkçe.
- **Tarayıcılar:** güncel Chrome ve Edge.
- **Kapsam dışı:**
  - Message veya StackTrace içinde metin araması.
  - Servis veya rol bazında yetki.
  - Canlı akış ve otomatik yenileme.
  - Dışa aktarma (CSV, JSON).
  - AI kök neden analizi (BR-5).
  - API anahtarı yönetim ekranı.
  - Retention ve silme (BR-4).
  - Mobil cihaza özel düzen.

## Acceptance criteria
- [ ] AC-1 — Giriş yapmamış kullanıcı panelin herhangi bir sayfasını açınca giriş akışına
      yönlendirilir ve hiçbir log içeriği görmez. Kimlik doğrulaması olmadan veya yalnızca bir
      servis API anahtarıyla yapılan log okuma isteği reddedilir ve içerik döndürmez.
- [ ] AC-2 — Oturumu süresi dolmuş kullanıcı bir işlem yaptığında yeniden giriş istenir. Girişten
      sonra aynı sayfaya, aynı filtrelerle döner. Giriş başarısız olur veya iptal edilirse hata
      sayfası ve "yeniden giriş yap" seçeneği görünür. Çıkış yapan kullanıcı tarayıcının geri
      tuşuyla log içeriği göremez.
- [ ] AC-3 — Son 24 saatte 3 kaydı ve 25 saat önce 1 kaydı olan bir depoda kullanıcı listeyi
      açınca yalnızca son 3 kaydı, en yeni önce görür. Her satırda alınma zamanı, ServiceName,
      LogLevel, Message'ın ilk satırı ve varsa ExceptionType bulunur. 500 karakterlik tek satırlık
      bir Message ilk 200 karakteriyle ve kısaltıldığı belirtilerek gösterilir. 1 milyon kayıtlı
      bir depoda ilk sayfa en geç 1 saniyede gelir.
- [ ] AC-4 — ServiceName, LogLevel (birden çok değer) ve zaman aralığı filtreleri birlikte
      uygulanır; yalnızca hepsine uyan kayıtlar listelenir. Filtreli sayfanın adresi yeni bir
      sekmede açılınca aynı sonuç görünür. ServiceName seçenekleri yalnızca kaydı olan
      servislerdir.
- [ ] AC-5 — Filtreye uyan 120 kayıt 50, 50 ve 20 kayıtlık üç sayfada gösterilir. Kullanıcı 1.
      sayfadayken 5 yeni kayıt gelse de 2. ve 3. sayfada hiçbir kayıt iki kez görünmez ve hiçbir
      kayıt atlanmaz.
- [ ] AC-6 — Bitişi başlangıcından önce olan bir aralık, 31 günden uzun bir aralık ve tanımsız
      bir LogLevel kabul edilmez. Kullanıcı ilgili alanın yanında okunabilir bir mesaj görür.
      Tam 31 günlük aralık kabul edilir.
- [ ] AC-7 — Ayrıntı sayfası kaydın tüm alanlarını gösterir: kimlik, ServiceName, LogLevel,
      Message'ın tamamı, ExceptionType, StackTrace, girintili Metadata, servis zamanı ve alınma
      zamanı. Zamanlar saat dilimiyle gösterilir; boş isteğe bağlı alanlar "—" olarak görünür.
      Ayrıntıdan geri dönüldüğünde liste filtrelerini korur.
- [ ] AC-8 — Message, StackTrace, ExceptionType ve Metadata'sında `<script>`, `<img onerror=…>` ve
      `javascript:` bağlantısı bulunan bir kayıt listede ve ayrıntıda düz metin olarak görünür;
      hiçbir betik çalışmaz ve sayfaya yeni bir öğe eklenmez.
- [ ] AC-9 — Liste ve ayrıntı ekranlarının tüm durumları görünür: yükleniyor, hiç kayıt yok,
      filtre yokken son 24 saatte kayıt yok (daha eski aralık seçme yolu ile), filtreyle sonuç
      yok ("filtreleri temizle" ile), kayıt bulunamadı (var olmayan ve bozuk
      kimlik), okuma arızası ("yeniden dene" ile; mesajda bağlantı dizesi, yığın izi veya sunucu
      adı yok).
- [ ] AC-10 — Giriş sonrası tüm akış (filtre, sayfalama, ayrıntıya geçiş ve geri dönüş) yalnızca
      klavyeyle tamamlanır. Otomatik erişilebilirlik taraması liste ve ayrıntı sayfalarında WCAG
      2.1 AA ihlali bulmaz. LogLevel metinle de belirtilir.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise | |
| Regressions introduced | |
| Bugs escaped to production | |
