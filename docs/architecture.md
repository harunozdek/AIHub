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
  > **FD-4 review ile uygulanır, otomatik testi yoktur** (spec 0001 self-critique). Diğer üçü bir
  > mimari testle assert edilir; FD-4 için güvenilir bir otomatik vekil bulunamadı ve yanlış bir
  > testin verdiği güven, testin hiç olmamasından kötü kabul edildi. Her review'da elle kontrol
  > edilir.

## Deliberately out of scope

Alarm/bildirim (e-posta, Slack, PagerDuty) · dağıtık izleme (tracing) ve metrik toplama · log
aramada tam metin arama motoru · mikroservislere ayrıştırma · çok kiracılı (multi-tenant)
izolasyon · mobil istemci.
