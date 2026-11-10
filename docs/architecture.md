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
- **`tools/` (Spec 0006):** Üretim yayınına girmeyen geliştirme araçları. `tools/AIHub.DemoSeed`,
  Api gibi bir composition root'tur: Application ve Infrastructure'a bağlanır. Yalnızca komutta açıkça
  verilen `Development` ortamında ve yerel bir PostgreSQL'e yazar; bilerek launchSettings'i yoktur. Kayıtları `IngestLogHandler`'dan (doğrulama, maskeleme) geçirir;
  alınma zamanını geriye tarihlemek için HTTP ve Redis'i atlayıp doğrudan depoya yazar.
- **Sağlık denetimi (Spec 0005, ADR 0003):** Backend'deki ayrı bir `BackgroundService`
  (`HealthCheckWorker`) izlenen uygulamaların sağlık adreslerini 30 saniyede bir dener. Backend'in
  dışarıya, kullanıcının verdiği adrese istek gönderdiği tek yer burasıdır.
  - İstek `IHealthProbe` üzerinden gider; engel denetimi bağlantı anında yapılır (`docs/security.md`).
  - "Bir aralıkta bir kez" kuralı veritabanında sağlanır: zamanı gelen satırlar
    `FOR UPDATE SKIP LOCKED` ile sahiplenilir. Birden çok AIHub örneği aynı uygulamayı aralık başına
    bir kez denetler.
  - Durum PostgreSQL'dedir; worker bellekte durum tutmaz.
  - İzlenen uygulamaların uçları `/api/monitored-apps` altındadır. `/api/panel/*` yalnızca GET
    kabul eder, bu yüzden yazma uçları ayrı bir gruptadır.
  - Panelin Down uyarısı 15 saniyede bir panelin kendi route handler'ını (`/durum/down`) yoklar;
    backend'e o gider.
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
