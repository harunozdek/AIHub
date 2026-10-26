# Architecture

> **Bootstrap'te dolduruldu (2026-10-26).** Burada *karar verilmiş* mimari yazılıdır, hedeflenen
> değil. Ajanlar plan yapmadan önce bu dosyayı okur; buradaki belirsizlik belirsiz koda dönüşür.

## System overview

AIHub, çok sağlayıcılı LLM çağrılarını tek bir HTTP yüzeyi arkasında toplayan bir ASP.NET Core
(.NET 10) gateway'idir: bir Request'i Route kurallarına göre bir Model'e yönlendirir, çağrının
Usage'ını ölçer ve Tenant Quota'sını uygular. Mimari stil **dikey dilimler (vertical slices)**:
katman soyutlaması yok, her yetenek kendi klasöründe uçtan uca durur. Sebep: bir gateway'de
yetenekler birbirinden bağımsızdır ve erken katmanlaştırma karşılıksız soyutlama üretir. Tek
süreç, tek deploy — bir gateway'e ağ atlaması eklemek gecikmeyi bedelsiz büyütür.

## Modules / components and ownership

| Module | Single responsibility | Owns |
|---|---|---|
| `Api` | HTTP yüzeyi: uç nokta tanımı, istek doğrulama, OIDC kimlik doğrulama, `Result<T>` → ProblemDetails çevirisi | Dışa açık istek/yanıt sözleşmesi |
| `Core` | Tüm iş mantığı, dikey dilimler halinde (her dilim = bir yetenek, uçtan uca) | Route kuralları, Quota değerlendirmesi, Usage ölçümü |
| `Providers` | Dış Provider adaptörleri — sistemin tek dış sızıntı noktası | Provider SDK kullanımı ve Provider kimlik bilgilerine erişim |

## Communication rules

- `Api` → `Core`: doğrudan çağrı (süreç içi), serbest.
- `Core` → `Providers`: yalnızca `Core` tarafında tanımlı arayüz üzerinden.
- `Core` dilimleri arası: **doğrudan çağrı yok** (bkz. FD-1).
- Olay/mesaj altyapısı şimdilik yok; gerekirse bir ADR ile gelir.

## Forbidden dependencies (make them testable)

Bağlayıcı — bir mimari testle assert edilebilecek biçimde yazılmıştır:

- **FD-1:** Bir `Core` dilimi başka bir dilimin tiplerini veya klasörünü referans almaz.
  Paylaşım gerekiyorsa ortak tip `Core/Shared` altına taşınır.
- **FD-2:** Provider SDK türleri yalnızca `Providers` içinde geçer; `Core` ve `Api` yalnızca
  AIHub'ın kendi tiplerini görür. Provider eklemek/değiştirmek tek nokta değişikliğidir.

> Bağlayıcı **değil**, açık karar olarak bekliyor: `Api`'nin iş mantığı içermemesi ·
> `Core`'un doğrudan `HttpClient`/`DbContext` kullanmaması.

## Deliberately out of scope

Kullanıcı arayüzü · mikroservislere ayrıştırma · gerçek Provider'a karşı otomatik test
(`docs/testing.md`) · prompt/yanıt arşivleme · faturalama ve ödeme akışı.
