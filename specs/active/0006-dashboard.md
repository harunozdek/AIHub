# Spec 0006 — Dashboard (genel durum ekranı) ve geliştirme için örnek veri

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0006-plan.md`

## Intent
Geliştiriciler ve operasyon ekibi, panele girdiklerinde kurumun genel durumunu tek bakışta görmek
istiyor. Bugün bunun için log listesinde filtre denemek ve izlenen uygulamalar ekranına ayrıca
bakmak gerekiyor. Başarı şöyle tanımlanır: giriş yapan kullanıcı ilk ekranda son 24 saatteki log
ve hata sayılarını, en çok hata üreten servisleri, en sık exception türlerini ve izlenen
uygulamaların durumunu görür. Herhangi bir sayıdan, o sayıyı oluşturan kayıtların listesine tek
tıkla geçer. Ayrıca geliştiriciler, boş bir yerel ortamda dashboard'u ve log panelini anlamlı
veriyle görebilmek için tek komutla örnek veri yükler. Zaman aralığı seçimi, otomatik yenileme,
uptime yüzdesi ve AI kök neden analizi bilerek kapsam dışıdır.

## Requirements
- **R-1 Giriş ve açılış ekranı:** Dashboard yalnızca giriş yapmış kullanıcıya açılır (0004).
  Girişten sonra ve panelin kök adresi açıldığında kullanıcı dashboard'a gelir; log listesi
  menüden erişilebilir kalır.
- **R-2 Zaman penceresi:** Dashboard'daki bütün log sayıları, ekranın yüklendiği andan geriye
  son 24 saati kapsar (alınma zamanına göre). Ekranda bu pencere ve verinin yüklendiği an yazılır.
- **R-3 Önem dereceleri:** Dört LogLevel'in (Critical, Error, Warning, Info) her biri için son 24
  saatteki kayıt sayısı gösterilir. Kaydı olmayan derece 0 olarak görünür.
- **R-4 Hata üreten servisler:** Son 24 saatte Error veya Critical kaydı olan servisler, bu
  kayıtların sayısına göre çoktan aza sıralanır; en fazla 10 servis gösterilir. Eşitlikte
  ServiceName alfabetik sıraya girer.
- **R-5 Sık exception'lar:** Son 24 saatte ExceptionType taşıyan kayıtlar türe göre sayılır; en
  sık 5 tür, sayılarıyla ve her türün son görüldüğü zamanla gösterilir. Eşitlikte son görülen
  önce gelir.
  - Kabulde boş ya da yalnızca boşluk olan ExceptionType "exception yok" sayılır; diğerleri baştan ve
    sondan kırpılarak saklanır. Sayım ile listenin süzmesi böylece aynı değeri görür. Bu değişiklikten
    önce saklanmış kayıtlar değişmez (LogEntry değişmezliği); dashboard yalnızca son 24 saati
    kapsadığından olası fark en geç 24 saatte kaybolur (review 1 triage, F-2; harun.ozdek, 2026-11-10).
- **R-6 İzlenen uygulamalar:** Ayakta, Down ve Bilinmiyor durumundaki izlenen uygulama sayıları
  gösterilir (0005). Down olan uygulamalar adları ve ne zamandan beri Down oldukları ile
  listelenir.
- **R-7 Ayrıntıya geçiş:** R-3, R-4 ve R-5'teki her sayı, aynı 24 saatlik pencere ve ilgili
  filtrelerle (derece, servis, exception türü) log listesini açar. R-6'daki durum özeti izlenen
  uygulamalar ekranını açar.
  - Log listesi bunun için ExceptionType filtresi kazanır: tam eşleşme, tek değer; diğer
    filtrelerle birlikte uygulanır ve sayfa adresinde yer alır (0002 R-3 ile aynı kurallar).
- **R-8 Yenileme:** Veri ekran açılırken bir kez yüklenir. Kullanıcı "Yenile" ile veriyi ve
  pencereyi yeniden yükler. Otomatik yenileme yoktur.
- **R-9 Arıza:** Log verisi okunamazsa log bölümleri hata mesajı ve "yeniden dene" gösterir.
  İzlenen uygulama verisi okunamazsa yalnızca o bölüm hata gösterir; bir bölümün arızası diğerini
  boşaltmaz.
- **R-10 Örnek veri (yalnızca geliştirme):**
  - Geliştirici tek komutla yerel ortama örnek log kayıtları yükler: en az 6 servis, dört
    LogLevel'in hepsi, ExceptionType ve StackTrace taşıyan kayıtlar, son 24 saate yayılmış alınma
    zamanları. R-3 ile R-6 arasındaki bölümlerin hiçbiri bu veriyle boş kalmaz.
  - Örnek kayıtlar gerçek kayıtlarla aynı doğrulama ve maskeleme kurallarından geçer; alınma
    zamanları geriye tarihlenir. Gerçek kabul yolu alınma zamanını her zaman "şimdi" yaptığı için
    HTTP ve kuyruk katmanları atlanır, iş kuralları atlanmaz (plan 0006, risk 1; harun.ozdek,
    2026-11-09).
  - Geliştirici komutu çalışan yerel ortamda ek bir kurulum (ör. elle API anahtarı oluşturma)
    yapmadan çalıştırır.
  - Örnek servislerin adı `demo-` ile başlar; gerçek veriyle karışmaz.
  - Komut her çalıştığında yeni kayıtlar ekler, mevcut veriyi silmez veya değiştirmez.
  - Komut üretim ortamında çalışmaz; denendiğinde hiçbir kayıt yazmadan nedenini söyleyerek durur.
    Development ortamı komutta açıkça verilir; PostgreSQL sunucusu yerel değilse de durur (review 1
    triage, F-1; harun.ozdek, 2026-11-10).

## User experience
- **Flow:**
  1. Kullanıcı giriş yapar ya da panelin kök adresini açar; dashboard açılır.
  2. Dört bölümü görür: önem dereceleri, hata üreten servisler, sık exception'lar, izlenen
     uygulamalar.
  3. Bir sayıya tıklar; ilgili filtrelerle log listesi (ya da izlenen uygulamalar ekranı) açılır.
     Tarayıcının geri tuşuyla dashboard'a döner.
  4. "Yenile" ile güncel veriyi alır.
- **States:** her bölüm için ayrı ayrı:
  - yükleniyor;
  - hiç veri yok (log bölümleri: "Son 24 saatte kayıt yok"; servis ve exception bölümleri:
    "Son 24 saatte hata yok" / "Son 24 saatte exception yok"; uygulama bölümü: "Henüz izlenen
    uygulama yok" ve izlenen uygulamalar ekranına bağlantı);
  - hata (mesaj ve "yeniden dene");
  - başarı.
  - Yetkisiz: giriş yapmamış kullanıcı girişe yönlendirilir; V1'de giriş yapan her kullanıcı
    yetkilidir.
- **Design reference:** none — the flow above is the design.
- **Accessibility:**
  - Bütün sayılar ve bağlantılar yalnızca klavyeyle erişilebilir; odak her zaman görünürdür.
  - Sayılar ve durumlar yalnızca renkle değil, metinle de belirtilir.
  - Her bölümün bir başlığı vardır; ekran okuyucu bölümleri başlıklardan tanır.
  - Kontrast ve yapı WCAG 2.1 AA düzeyindedir.

## Constraints & out of scope
- **Bağımlılık:** R-6, 0005'in (sağlık izleme) ship edilmesine bağlıdır. 0006'nın build'i 0005
  main'e merge edildikten sonra başlar.
- **Erişim (V1):** Giriş yapan her kullanıcı bütün servislerin sayılarını görür (0002 ile aynı).
- **Performans:** Dashboard, son 24 saatte 1 milyon log kaydı varken en geç 2 saniyede yüklenir.
- **Kapsam dışı:**
  - Zaman aralığı seçimi (1 saat, 7 gün vb.) ve karşılaştırma ("düne göre").
  - Grafikler ve zaman içindeki trend.
  - Otomatik yenileme, canlı akış.
  - Uptime yüzdesi ve geçmiş durumlar.
  - AI kök neden analizi.
  - Kullanıcıya özel dashboard, bölüm ekleme veya gizleme.
  - Örnek veriyi silme komutu; izlenen uygulamalar için örnek veri (adresleri gerçek olmadığı
    için hepsi Down olur ve her ekranda uyarı çıkar).
- **Split:** Önerilmedi. Tek kullanıcı yeteneği var (genel durumu görmek); örnek veri bunun
  geliştirme desteğidir ve kendi başına bir kullanıcı yeteneği değildir.

## Acceptance criteria
- [ ] AC-1 — Giriş yapmamış kullanıcı dashboard adresini açınca girişe yönlendirilir; girişten
  sonra dashboard'a gelir. Panelin kök adresi giriş yapmış kullanıcıyı dashboard'a götürür.
- [ ] AC-2 — Dört LogLevel'in son 24 saatteki sayıları doğrudur; 24 saatten eski kayıtlar
  sayılmaz; kaydı olmayan derece 0 görünür. Pencere ve yüklenme anı ekranda yazılıdır.
- [ ] AC-3 — Hata üreten servisler Error + Critical sayısına göre çoktan aza, eşitlikte
  alfabetik sıralıdır; 11 servis varken 10'u görünür; yalnızca Info/Warning kaydı olan servis
  listede yoktur.
- [ ] AC-4 — En sık 5 exception türü sayıları ve son görülme zamanlarıyla görünür; eşitlikte son
  görülen önce gelir; ExceptionType'ı olmayan kayıtlar sayılmaz.
- [ ] AC-5 — İzlenen uygulama sayıları (Ayakta, Down, Bilinmiyor) doğrudur; Down olanlar adları
  ve Down oldukları zamanla listelenir.
- [ ] AC-6 — Bir derece, servis ya da exception sayısına tıklamak log listesini aynı pencere ve
  filtrelerle açar; listede yalnızca bu filtrelere uyan kayıtlar vardır ve kayıt sayısı tıklanan
  sayıya eşittir (gerekirse sayfalar boyunca sayılır). Log listesinin ExceptionType filtresi
  adresten gelir, diğer filtrelerle birlikte uygulanır. Durum özeti izlenen uygulamalar ekranını
  açar.
- [ ] AC-7 — Her bölümün boş durumu kendi mesajıyla görünür (hiç log yok; log var ama hata yok;
  izlenen uygulama yok).
- [ ] AC-8 — Log verisi okunamazken log bölümleri hata ve "yeniden dene" gösterir, uygulama
  bölümü normal görünür; tersi de geçerlidir. "Yeniden dene" ve "Yenile" veriyi yeniden yükler.
- [ ] AC-9 — Ekranın tamamı yalnızca klavyeyle kullanılabilir; durumlar metinle de belirtilir;
  otomatik erişilebilirlik denetimi WCAG 2.1 AA ihlali bulmaz.
- [ ] AC-10 — Örnek veri komutu, elle anahtar oluşturmadan, boş bir yerel ortamda çalışınca R-10'daki dağılım oluşur ve
  dashboard'un hiçbir log bölümü boş kalmaz; ikinci çalıştırma kayıt ekler, mevcutları değiştirmez.
- [ ] AC-11 — Örnek veri komutu üretim ortamı yapılandırmasıyla çalıştırılınca hiçbir kayıt
  yazmadan nedenini söyleyerek hata koduyla biter.
- [ ] AC-12 — Son 24 saatte 1 milyon kayıt varken dashboard'un log verisi en geç 2 saniyede gelir.

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
