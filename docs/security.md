# Security

> **Bootstrap'te dolduruldu (2026-10-26).** Ajanların her planda ve her review'da uyması gereken
> taban kurallar.

## Secrets
- Secrets never enter the repo, specs, prompts, or chat. `.env` is gitignored; provide `.env.example`.
- Agents never print secret values, even when debugging.
- **Provider API anahtarları:** geliştirmede `dotnet user-secrets` (depo dışında, kazara commit
  edilemez), üretimde ortam değişkeni veya bir secret store. Depoya yalnızca şablon/örnek girer,
  değer asla girmez.
- Provider kimlik bilgileri yalnızca `Providers` modülünde okunur (`docs/architecture.md`, FD-2);
  hiçbir Request yanıtında, hata mesajında veya logda görünmez.

## Input & output
- Doğrulama `Api` sınırında yapılır: bilinmeyen Model adı, eksik alan ve sınır dışı değerler
  Core'a hiç ulaşmaz.
- Dışa çıkan her hata RFC 9457 **ProblemDetails**'dır (`docs/conventions.md`, Error handling).
- Hata mesajlarında asla sızmaz: Provider kimlik bilgileri, ham Provider yanıt gövdesi, yığın izi,
  başka bir Tenant'a ait herhangi bir bilgi.

## Loglama ve gözlemlenebilirlik
- **Prompt ve yanıt içeriği hiçbir koşulda loglanmaz veya saklanmaz.** Log ve Usage kaydı yalnızca
  ölçüm taşır: token sayısı, maliyet, gecikme, Model, Provider, sonuç durumu (`docs/domain.md`,
  BR-1 ve invariant'lar). Kullanıcı verisi en baştan kapsam dışıdır — saklamadığın veriyi
  sızdıramazsın.

## AuthN / AuthZ
- **AuthN:** OAuth 2.0 / OIDC bearer JWT. `Api` her isteği doğrular; authority ortam başına
  yapılandırılır (secret değil, konfigürasyon).
- **Tenant çözümü:** Tenant kimliği token claim'inden okunur — istemcinin gönderdiği gövdeden
  veya header'dan **asla** alınmaz.
- **Varsayılan ret (default-deny):** kimliği doğrulanmamış istek hiçbir uç noktaya erişmez;
  bir Tenant yalnızca kendi Usage ve Quota verisini görür.

> Açık karar (bağlayıcı değil): hangi kimlik sağlayıcı kullanılacak ve Tenant'ı hangi claim
> taşıyacak. İlk spec veya bir ADR bunu karara bağlamalı.

## Dependencies
- Yeni bağımlılık eklemek plan aşamasında açıkça önerilir ve insan onayı gerektirir
  (AGENTS.md, Proposal rule).
- Kontrol edilenler: lisans, bakım durumu (son sürüm/aktiflik), bilinen CVE'ler, getirdiği geçişli
  bağımlılık yükü.
- Provider SDK'ları yalnızca `Providers` modülüne bağımlılık olarak girer.

## Review lens
Security is a mandatory dimension of every independent review (see `prompts/review.md`), not a
separate afterthought phase.
