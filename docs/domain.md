# Domain

> **Bootstrap'te dolduruldu, 2026-10-26'da ADR 0001 ile revize edildi.** İş, insanlar ve ajanlar
> arasındaki ortak dil. Bir terim burada yoksa, AI onun anlamını kendisi uydurur.

## Ubiquitous language

Bu terimler **kodda da aynı (İngilizce) biçimleriyle** geçer, böylece sözlük ile kod birebir
örtüşür — genel kod dili Türkçe olsa bile (`docs/conventions.md`, Language).

| Term | Meaning | Notes / not to be confused with |
|---|---|---|
| LogEntry | AIHub'a ulaşan ve saklanan tek log kaydı | Ham HTTP isteği değil; doğrulanmış ve maskelenmiş kayıt |
| ServiceName | Logu gönderen kaynak servisin kimliği (ör. `odeme-servisi`) | İstemcinin gönderdiği metne güvenilmez: API anahtarına bağlı olan değer esastır |
| LogLevel | Kaydın önem derecesi: `Info`, `Warning`, `Error`, `Critical` | Dört değerden fazlası yok; `Debug`/`Trace` kabul edilmez |
| StackTrace | Exception'ın çağrı yığını metni | Message ile karıştırılmaz: Message insan için, StackTrace makine içindir |
| ExceptionType | Exception'ın tam tip adı (ör. `System.NullReferenceException`) | Java tarafında karşılığı tam sınıf adıdır; serbest metin değildir |
| Metadata | Kayda eşlik eden serbest anahtar/değer verisi (PostgreSQL `JSONB`) | Şemasızdır, ama maskelemeden muaf değildir |
| RootCauseAnalysis | Bir LogEntry için AI'ın ürettiği kök neden analizi | Kesin teşhis değil, **öneri**dir; insan kararını değiştirmez |
| Retention | Bir kaydın saklanma süresi; süre sonunda otomatik silinir | Yedekleme değil; silme politikasıdır |
| User | Panele giriş yapan kişi: kullanıcı adı, görünen ad, şifre hash'i (ADR 0002, Spec 0004) | Log gönderen servis değildir; servisler API anahtarıyla gelir (ServiceName) |

## Business rules

- **BR-1:** `POST /api/logs` isteği doğrulandıktan sonra kayıt **Redis Stream**'e yazılır ve
  `202 Accepted` döner; PostgreSQL yazımı tüketicide gerçekleşir. İstek, veritabanı yazımını
  beklemez — log gönderen mikroservis AIHub yüzünden yavaşlamaz.
- **BR-2:** Her LogEntry bir ServiceName ve bir LogLevel taşır; ikisi de boş olamaz. LogLevel dört
  tanımlı değerden biri değilse istek reddedilir (`400`).
- **BR-3:** Hassas veri **yazılmadan önce** maskelenir: Message, StackTrace ve Metadata, ingestion
  sırasında bilinen desenlere (e-posta, kimlik numarası, bearer token, bağlantı dizesi, API
  anahtarı) karşı taranır ve eşleşen kısımlar maskelenmiş olarak saklanır.
- **BR-4:** Retention süresi geçen LogEntry otomatik olarak silinir. Saklanmayan veri
  sızdırılamaz; silinmeyen log sınırsız büyüyen bir yetki yoluna dönüşür.
- **BR-5:** RootCauseAnalysis yalnızca `Error` ve `Critical` seviyeli kayıtlar için üretilir, ve
  bir öneri olarak saklanır — LogEntry'nin kendi alanlarını değiştirmez.

## Key domain invariants

- Bir LogEntry saklandıktan sonra **değişmez**: düzeltme yeni bir kayıt olur, mevcut kayıt
  güncellenmez. Log, olmuş bir olayın kaydıdır; geçmiş düzenlenmez.
- Maskelenmemiş hassas veri hiçbir koşulda PostgreSQL'e veya Redis'e yazılmaz (BR-3).
- RootCauseAnalysis'in yokluğu bir hata değildir: AI servisi erişilemez olsa bile LogEntry
  yazılır ve saklanır. Analiz, ingestion'ın ön koşulu değildir.
- Terim ile kod aynı kelimeyi kullanır: yukarıdaki tablodaki bir terim koda farklı bir adla girmez.
