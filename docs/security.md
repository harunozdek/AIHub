# Security

> **Bootstrap'te dolduruldu, 2026-10-26'da ADR 0001 ile revize edildi.** Ajanların her planda ve
> her review'da uyması gereken taban kurallar.

## Secrets
- Secrets never enter the repo, specs, prompts, or chat. `.env` is gitignored; provide `.env.example`.
- Agents never print secret values, even when debugging.
- PostgreSQL/Redis bağlantı dizeleri ve AI sağlayıcı anahtarları: geliştirmede
  `dotnet user-secrets` (depo dışında, kazara commit edilemez), üretimde ortam değişkeni veya bir
  secret store. `web/` tarafında yalnızca `NEXT_PUBLIC_` önekli değerler tarayıcıya gider — bir
  sır asla bu önekle tanımlanmaz.

## AuthN / AuthZ
İki ayrı yüzey, iki ayrı mekanizma:

- **Ingestion (`POST /api/logs`, makine istemcileri):** servis başına **API anahtarı**. Anahtar
  veritabanında yalnızca **hash** olarak durur, ham değer bir kez gösterilir; iptal anında etkilidir.
- **Panel (`web/`, insanlar):** OAuth 2.0 / OIDC. Authority ortam başına yapılandırılır
  (konfigürasyon, sır değil).
  - Panel authorization code + PKCE, `state` ve `nonce` ile giriş yapar (`openid-client`).
    Token'lar yalnızca Next.js sunucusunda durur: şifreli, `httpOnly` oturum çerezi
    (`iron-session`, anahtarı `SESSION_SECRET`). Tarayıcı JavaScript'i token'a erişemez.
  - Backend'in panel yüzeyi (`/api/panel/*`) yalnızca sağlayıcının imzaladığı JWT bearer token'ı
    (`Panel` şeması; issuer, imza, süre ve audience doğrulanır) kabul eder. Servis API anahtarı
    orada 401 alır; bearer token da ingestion yüzeyinde 401 alır. Her yüzey yalnızca kendi şemasına
    bağlı bir policy ile korunur, fallback policy default-deny olarak kalır.
  - Log içeriği taşıyan panel yanıtları `Cache-Control: no-store` ile döner: çıkıştan sonra geri
    tuşu içeriği önbellekten göstermez.
  - `OIDC_ALLOW_HTTP` yalnızca geliştirme ve e2e içindir (düz HTTP mock sağlayıcı); gerçek
    ortamlarda tanımlanmaz ve sağlayıcı HTTPS olmak zorundadır.
- **ServiceName istemciye bırakılmaz:** kaydın ServiceName'i çağrıyı yapan API anahtarına bağlı
  değerden belirlenir; istek gövdesindeki ad ile çelişirse anahtara bağlı değer kazanır. Aksi halde
  her servis, bir başkasının adına log yazabilir.
- **Varsayılan ret (default-deny):** kimliği doğrulanmamış istek hiçbir uç noktaya erişmez.
- Rate limiting Redis üzerinden, API anahtarı başına uygulanır: tek bir servisin log patlaması
  platformun tamamını düşürmemeli.

## Hassas veri ve saklama
- **Maskeleme yazımdan önce yapılır (`docs/domain.md`, BR-3).** Message, StackTrace ve Metadata
  ingestion sırasında bilinen desenlere karşı taranır: e-posta, kimlik numarası, bearer token,
  bağlantı dizesi, API anahtarı. Eşleşen kısımlar maskelenmiş olarak saklanır. Maskelemeyi
  yazımdan sonraya bırakmak, sırrın diske değdiği anı kaçırmak demektir.
- **Retention (BR-4):** süresi geçen kayıt otomatik silinir. Saklama süresi konfigürasyondur ve
  bir kaydın silinmesi geri alınamaz.
- Log platformları sır sızıntısının en sık yoludur: bir review'da "bu alan maskelenmiş mi?"
  sorusu, "bu kod çalışıyor mu?" kadar zorunludur.

## Input & output
- Doğrulama `Api` sınırında yapılır: tanımsız LogLevel, boş ServiceName, aşırı büyük gövde ve
  bozuk `Metadata` JSON'u `Application`'a hiç ulaşmaz.
- Dışa çıkan her hata RFC 9457 **ProblemDetails**'dır (`docs/conventions.md`, Error handling).
- Hata mesajlarında asla sızmaz: bağlantı dizeleri, API anahtarları, yığın izi, AI sağlayıcı ham
  yanıtı, başka bir servise ait log içeriği.
- Panelde log içeriği gösterilirken HTML olarak yorumlanmaz: gelen log, saldırganın kontrolündeki
  metindir (XSS yüzeyi).

## AI analiz servisi
- `IAiAnalysisService`'e gönderilen içerik **maskelenmiş** veridir; maskelenmemiş log bir dış AI
  sağlayıcısına gönderilmez.
- RootCauseAnalysis bir öneridir, yetki kararı değildir: hiçbir erişim kontrolü veya otomatik
  eylem AI çıktısına dayandırılmaz.

## Dependencies
- Yeni bağımlılık eklemek plan aşamasında açıkça önerilir ve insan onayı gerektirir
  (AGENTS.md, Proposal rule).
- Kontrol edilenler: lisans, bakım durumu, bilinen CVE'ler, getirdiği geçişli bağımlılık yükü.
- `AIHub.Domain` dış NuGet bağımlılığı almaz (`docs/architecture.md`, FD-1).

## Review lens
Security is a mandatory dimension of every independent review (see `prompts/review.md`), not a
separate afterthought phase.
