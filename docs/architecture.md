# Architecture

> **Bootstrap'te dolduruldu, 2026-10-26'da ADR 0001 ile revize edildi.** Burada *karar verilmiş*
> mimari yazılıdır, hedeflenen değil. Ajanlar plan yapmadan önce bu dosyayı okur; buradaki
> belirsizlik belirsiz koda dönüşür.

## System overview

AIHub, kurum içindeki Java ve .NET Core servislerinden gelen logları ve exception raporlarını tek
merkezde toplayan, depolayan ve AI desteğiyle kök neden analizi üreten bir gözlemlenebilirlik
platformudur. Mimari stil **Clean Architecture**: bağımlılıklar içe doğru akar
(`Api` → `Application` → `Domain`; `Infrastructure` dışarıda, yalnızca DI ile bağlanır). Sebep:
log ingestion'ın değişmez iş kuralları, değişken altyapıdan (PostgreSQL, Redis, AI sağlayıcısı)
bağımsız olarak test edilebilir kalmalı — ADR 0001.

Teslim iki parçadır, tek depoda: **.NET 10 backend** (`src/`) ve **Next.js yönetim paneli**
(`web/`). Tek süreçli backend; panel ayrı çalışır ve backend'i HTTP üzerinden tüketir.

## Modules / components and ownership

| Module | Single responsibility | Owns |
|---|---|---|
| `src/AIHub.Domain` | Entity'ler, değer tipleri, iş kuralları — çerçevesiz, bağımlılıksız | LogEntry ve LogLevel'ın anlamı, domain invariant'ları |
| `src/AIHub.Application` | Kullanım senaryoları ve dış dünya arayüzleri (`IAiAnalysisService`, repository arayüzleri) | İş akışı kararları: ingestion işleme, analiz tetikleme |
| `src/AIHub.Infrastructure` | Arayüzlerin somut uygulamaları: EF Core + PostgreSQL, Redis Stream, AI sağlayıcı istemcisi | Kalıcılık şeması, migration'lar, dış sistem erişimi |
| `src/AIHub.Api` | HTTP yüzeyi: uç noktalar, kimlik doğrulama, global exception middleware, DI kablolaması | Dışa açık istek/yanıt sözleşmesi |
| `web/` | Next.js (App Router) + TypeScript + Tailwind yönetim paneli | Panel arayüzü ve istemci tarafı durumu |
| `tests/` | xUnit test projeleri; `src/` yapısını aynalar | — |

## Communication rules

- Bağımlılık yönü **içe doğru**: `Api` → `Application` → `Domain`. `Infrastructure` yalnızca
  `Application`'ın arayüzlerini uygular ve `Api`'de DI ile bağlanır.
- **Log ingestion asenkrondur:** `POST /api/logs` isteği doğrular, logu **Redis Stream**'e yazar ve
  `202 Accepted` döner. Ayrı bir tüketici (`BackgroundService`) stream'den okuyup PostgreSQL'e
  yazar ve gerektiğinde AI analizini tetikler. Sebep: süreç çökerse kuyruktaki kayıt kaybolmaz ve
  yazma gecikmesi log gönderen mikroservise yansımaz.
- `web/` → `Api`: yalnızca dışa açık HTTP sözleşmesi üzerinden; panelin veritabanına doğrudan
  erişimi yoktur.
- **Panel okuma yüzeyi (Spec 0002):** `GET /api/panel/logs`, `/api/panel/logs/{id}` ve
  `/api/panel/services` salt okunurdur. Okuma tarafı yazma deposundan ayrı bir arayüzle
  (`ILogEntryReader`) çalışır. Testlerin kanıtladığı: panel yüzeyindeki her uç yalnızca GET kabul
  eder (Api testi) ve okuma arayüzünde yazma metodu yoktur (mimari test). Okuma deposunun gövdesinin
  `DbContext` üzerinden yazmadığı testle değil, review'da denetlenir.
  Liste `(received_at, id)` üzerinde keyset sayfalanır; yeni kayıtlar sonraki sayfaları kaydırmaz.
- **Dashboard (Spec 0006):** `GET /api/panel/dashboard` aynı okuma yüzeyindedir ve aynı
  `ILogEntryReader` üzerinden son 24 saatin üç özetini döner: derece sayıları, en çok hata üreten
  servisler, en sık exception türleri. Derece ve servis özetleri
  `(received_at) INCLUDE (level, service_name)` kapsayan indeksiyle tabloya dokunmadan hesaplanır.
  `exception_type` bu indekse bilerek girmez: 512 karakterlik çok baytlı bir değer `service_name` ile
  birlikte btree satır sınırını aşıp kaydı yazılamaz hale getirebilirdi. Dashboard'un uygulama bölümü
  ayrı bir çağrıyla `/api/monitored-apps`'ten gelir; iki bölüm birbirinin arızasından etkilenmez.
  - **Saatlik trend (Spec 0007):** Aynı yanıt, pencerenin 24 eşit dilimini (`hourly`) derece sayılarıyla
    taşır. Dilim sınırlarını sunucu hesaplar: pencere sonu milisaniyeye kırpılır, dilim sonu bir sonraki
    dilimin başlangıcından 1 µs öncedir. Böylece dilimin `from`/`to`'su log listesine olduğu gibi
    taşındığında aynı kayıtlar görünür. Dilim SQL'de tamsayı mikrosaniyeyle, aynı kapsayan indeksle
    hesaplanır. Panel grafikleri kütüphanesiz, CSS ile çizer; yeni bir bağımlılık yoktur.
  - **Zaman penceresi (Spec 0011):** Uç nokta `?window=<24h|48h|7d|14d|30d|90d|180d|365d>` (yoksa
    `24h`) ya da serbest aralık `?from=&to=` (UTC ISO) alır; tanımsız pencere ve geçersiz aralık 400'dür,
    API sessizce düzeltmez. Pencere tablosu (uzunluk, dilim boyu, dilim sayısı) tek yerde,
    `Application/Dashboard/DashboardWindow`'dadır; serbest aralığın dilimi, uzunluğu aşmayan en kısa hazır
    pencereninkidir ve son dilim aralığın sonunda kesilir. Yanıt `hourly` yerine `slices` taşır; derece
    sayıları dilimlerin toplamıdır. Aynı yanıt penceredeki en yeni 10 hata ve 10 uyarıyı
    (`recentErrors`, `recentWarnings`; mesajın ilk satırının 120 karakteri) da taşır.
    - Handler'ın okumaları eşzamanlı çalışır; `LogEntryReader` her okuma için fabrikadan kendi bağlamını açar.
      Bu yüzden bir dashboard isteği aynı anda **5 bağlantı** kullanır (servisler, exception'lar, dilimler, son
      hatalar, son uyarılar). Bağlantı dizesinde havuz ayarı yoktur: Npgsql'in varsayılan üst sınırı 100'dür
      (PostgreSQL'in varsayılan `max_connections` değeri de 100). Kurum içi kullanımda yeterli varsayılır;
      kabaca 20 eşzamanlı dashboard isteği havuzu doldurur ve öteki okumalar bekler. Kullanıcı sayısı artarsa
      havuz boyutu (`Maximum Pool Size`) ve `max_connections` birlikte büyütülür ya da handler'ın eşzamanlılığı
      sınırlanır (Review 1, F-6).
    - Uzun pencerede taramayı küçültmek için üç kısmi indeks: `received_at INCLUDE exception_type WHERE
      exception_type IS NOT NULL` (exception özeti; `service_name` taşımadığı için satır sınırının altında),
      `(received_at, id) WHERE level IN ('Error','Critical')` ve `… WHERE level = 'Warning'` (son kayıtlar).
      Derece filtresi SQL'e sabit olarak yazılır; dizi parametresiyle PostgreSQL kısmi indeksi kullanamazdı.
    - Log listesinin aralık sınırı 365 gün + 1 saattir (yaz saatli dilimde 365 yerel gün); dashboard'un
      açtığı her aralığı kabul eder.
    - Panel seçimi adreste tutar (`?pencere=7g`, `?pencere=aralik&baslangic=…&bitis=…`; yalnızca tarihler).
      Serbest aralığın yerel gün sınırlarını sunucu, tarayıcının yazdığı `aihub_tz` çerezindeki IANA saat
      dilimiyle hesaplar (`web/src/lib/dashboard-pencere.ts`); çerez yoksa veri istenmez, çerez yazılıp
      sayfa bir kez yenilenir.
- **`tools/` (Spec 0006):** Üretim yayınına girmeyen geliştirme araçları. `tools/AIHub.DemoSeed`,
  Api gibi bir composition root'tur: Application ve Infrastructure'a bağlanır. Yalnızca komutta açıkça
  verilen `Development` ortamında ve yerel bir PostgreSQL'e yazar; bilerek launchSettings'i yoktur. Kayıtları `IngestLogHandler`'dan (doğrulama, maskeleme) geçirir;
  alınma zamanını geriye tarihlemek için HTTP ve Redis'i atlayıp doğrudan depoya yazar.
- **Sağlık denetimi (Spec 0005, ADR 0003):** Backend'deki ayrı bir `BackgroundService`
  (`HealthCheckWorker`) izlenen uygulamaların sağlık adreslerini 10 saniyede bir dener (Spec 0012; 0005'te
  30 sn). Worker 2 sn'de bir uyanır; iki denetim arası en az yarım aralıktır (5 sn). Backend'in
  dışarıya, kullanıcının verdiği adrese istek gönderdiği tek yer burasıdır.
  - İstek `IHealthProbe` üzerinden gider; engel denetimi bağlantı anında yapılır (`docs/security.md`).
  - "Bir aralıkta bir kez" kuralı veritabanında sağlanır: zamanı gelen satırlar
    `FOR UPDATE SKIP LOCKED` ile sahiplenilir. Birden çok AIHub örneği aynı uygulamayı aralık başına
    bir kez denetler.
  - Durum PostgreSQL'dedir; worker bellekte durum tutmaz.
  - İzlenen uygulamaların uçları `/api/monitored-apps` altındadır. `/api/panel/*` yalnızca GET
    kabul eder, bu yüzden yazma uçları ayrı bir gruptadır.
  - Panelin Down uyarısı 10 saniyede bir panelin kendi route handler'ını (`/durum/down`) yoklar;
    backend'e o gider.
  - "İzlenen uygulamalar" ekranı (Spec 0012) ilk veriyi sunucuda okur, sonra 10 saniyede bir ve sekme
    yeniden görünür olunca `/durum/uygulamalar`'ı yoklar. Bu uç ekranın gösterdiği alanları döner; ekleyen ve
    son değiştiren dönmez. Satırdaki iş (açık form, taslak, hata, odak ve imleç) liste düzeyinde uygulama
    kimliğine bağlı tutulur: uygulama Ayakta ile Down bölümü arasında yer değiştirince satır yeniden kurulsa da iş
    korunur. Kaydetme ve çıkarmanın sonucu (form kapanır, hata yazılır) işlemin kendisinde satırın işine yazılır,
    satırın bir effect'ine bırakılmaz: işlem sürerken satır başka bölüme taşınabilir. Bir işlemden sonra sunucunun
    verisi hemen gösterilir; ondan önce başlamış yoklamalar ve istek sırasına göre eski kalan yanıtlar atılır.
    Geniş ekranda Ayakta tablo, dar ekranda ('md' altı) kart olur; ikisi aynı anda DOM'a konmaz.
  - Yük (Spec 0012, plan risk 2): 200 uygulamanın yarısı 5 sn zaman aşımındayken her uygulama her 10 sn'lik
    aralıkta bir kez denetlenir (`IzlemeTuruPerformansTestleri`).
- **Kendi sağlık denetimi (Spec 0008):** Backend `/health` ile PostgreSQL ve Redis'i denetler: ikisi
  paralel, her biri 2 sn ile sınırlı; bağlantı kurma ve kapatma dahil iş arka planda koşar, süre dolunca
  denetim beklemeden başarısız olur. Panel `/health` yalnızca kendisini bildirir.
  - Açılışta `SelfMonitoring:ApiHealthUrl` ve `SelfMonitoring:PanelHealthUrl` verilmişse bu adresler
    `AIHub.Api` ve `AIHub.Panel` olarak "Sistem" tarafından izlenen uygulamalara bir kez eklenir.
    "Bir kez eklendi" işareti `self_monitoring_registrations` tablosundadır; silinen kayıt geri gelmez.
  - Panel kaydını backend'in worker'ı denetler: panel adresine backend'in makinesinden erişilebilmelidir.
  - Sınır: backend tamamen çökerse ya da PostgreSQL erişilemezse Down kaydedilemez ve gösterilemez
    (durum PostgreSQL'dedir); panel 0005'in "durum güncellenemedi" notunu gösterir. Tam çöküş için
    dışarıdan izleme ayrı bir iştir.
- **Panel BFF desenindedir:** Giriş (server action) ve backend çağrıları Next.js sunucusunda yürür
  (server action, middleware, server component). Şifre doğrulaması ve oturum token'ının imzası
  backend'dedir (ADR 0002). Tarayıcı backend'i hiç çağırmaz ve token görmez;
  token'lar şifreli, `httpOnly` bir oturum çerezinde kalır (`docs/security.md`).

## Forbidden dependencies (make them testable)

Dördü de **bağlayıcı** ve bir mimari testle assert edilebilecek biçimde yazılmıştır:

- **FD-1:** `AIHub.Domain` hiçbir katmana bağımlı değildir — ne `Application`, ne
  `Infrastructure`, ne `Api` referansı tutar; dış NuGet bağımlılığı da almaz.
- **FD-2:** `AIHub.Application`, `AIHub.Infrastructure`'ı referans etmez. Arayüzü `Application`
  tanımlar, uygulamasını `Infrastructure` verir, bağlamayı `Api` yapar.
- **FD-3:** EF Core türleri (`DbContext`, `DbSet`, migration tipleri) yalnızca
  `AIHub.Infrastructure` içinde geçer; `Application` ve `Api` bunları görmez.
- **FD-4:** `AIHub.Api` iş mantığı içermez — yalnızca HTTP eşleme, istek doğrulama ve DI
  kablolaması yapar; her karar `Application`'da kalır.

## Deliberately out of scope

Panel dışı alarm/bildirim (e-posta, Slack, Teams, PagerDuty; panel içi Down uyarısı Spec 0005 ile kapsamdadır) · dağıtık izleme (tracing) ve metrik toplama · log
aramada tam metin arama motoru · mikroservislere ayrıştırma · çok kiracılı (multi-tenant)
izolasyon · mobil istemci.
