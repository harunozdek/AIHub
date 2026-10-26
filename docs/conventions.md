# Conventions

> **Bootstrap'te dolduruldu, 2026-10-26'da ADR 0001 ile revize edildi.** Burada yalnızca *gerçek*
> kurallar vardır: her kural ya araçla zorlanır (tercih edilen) ya da review'da kontrol edilir.
> Temenniler buraya yazılmaz.

## Language
<!-- Set at bootstrap (step 0) and mirrored in AGENTS.md. Changing it later is allowed — edit both
     places; existing documents are not translated retroactively. -->
- Chat language (interviews, sessions): **Türkçe**
- Document language (docs/, specs, plans, ADRs, review and verify reports): **Türkçe**
- Always English (protocol, not prose): ANEW core files, template headings and field labels,
  `Status:` values (Draft / Approved / In progress / Shipped), `Approved by / on:`, `Source:`.
- Code identifiers, branch names, commit messages: **Türkçe**, şu iki istisnayla:
  (1) `docs/domain.md`'deki domain terimleri kodda tanımlı (İngilizce) biçimini korur — sözlük ile
  kod birebir örtüşsün diye; (2) Conventional Commits önekleri (`feat`, `fix`, `refactor`, `docs`,
  `chore`) ve dal önekleri (`feature/`, `fix/`, `trivial/`, `chore/`) protokoldür, İngilizce kalır.
- Agent messages (handoff summaries, refusals, `Next:` lines): chat language; command names, file
  paths and protocol fields verbatim.

## Language & framework versions
- **Backend:** C# / .NET **10** (SDK 10.0.100; makinede 9.0.311 de var — `global.json` ile 10.x
  sabitlenir). ASP.NET Core 10.
- **Veritabanı:** PostgreSQL, EF Core + Npgsql sağlayıcısı. `Metadata` alanı `JSONB`.
- **Önbellek/kuyruk:** Redis — log akışı için Redis Stream, ayrıca rate limiting.
- **Frontend:** Next.js (App Router) + TypeScript + Tailwind CSS, Node **24** (`web/`).
- **Test:** xUnit + `WebApplicationFactory`; panel için Playwright (`docs/testing.md`).

## Naming
- C# standardı: `PascalCase` tip, `camelCase` yerel değişken; dosya adı = içindeki tipin adı.
- Proje adları katmanı taşır: `AIHub.Domain`, `AIHub.Application`, `AIHub.Infrastructure`,
  `AIHub.Api`. Test projesi aynaladığı katmanı adında taşır (`AIHub.Application.Tests`).
- `Application` katmanındaki arayüzler `I` öneki + niyet taşır (`IAiAnalysisService`), somut
  uygulama `Infrastructure`'da sağlayıcıyı adında belirtir.
- `web/` tarafı: dosya ve klasör adları `kebab-case`, React bileşenleri `PascalCase`.
- Dal adları: `feature/<spec-no>-<kısa-ad>` (`docs/git.md`).

## Error handling
Tek kutsal desen: **`Result<T>` + sınırda ProblemDetails**.
- `Application`'da *beklenen* hatalar (geçersiz LogLevel, eksik ServiceName, Redis erişilemez,
  AI servisi zaman aşımı) `Result<T>` olarak döner — kontrol akışıdır, exception değil.
- Exception yalnızca gerçekten beklenmeyen durumlar içindir.
- `Api` sınırında **global exception middleware** her şeyi RFC 9457 **ProblemDetails**'a çevirir.
- Kullanıcıya asla sızmaz: bağlantı dizeleri, API anahtarları, yığın izi, AI sağlayıcı ham yanıtı.
- **Kendi hatamız kendi logumuza düşmez:** AIHub'ın ingestion hattındaki bir hata, `POST /api/logs`
  üzerinden tekrar AIHub'a yazılmaz — sonsuz döngüyü engellemek için ayrı bir kanala yazılır.

## Data rules
- Zaman damgaları **UTC**, tipi `DateTimeOffset`. `DateTime.Now` kullanılmaz. Log kaynağının
  gönderdiği `Timestamp` ile AIHub'ın aldığı an ayrı alanlardır; ikisi karıştırılmaz.
- Kimlikler **GUID v7** (sıralı; indeks dostu ve dışa açılabilir). Artan `int`/`long` kimlik
  dışa açılmaz.
- Para/maliyet alanı gerekirse **`decimal`** (AI çağrı maliyeti dahil); `float`/`double` kullanılmaz.
- `Metadata` şemasızdır (`JSONB`) ama maskelemeden muaf değildir (`docs/domain.md`, BR-3).

## Enforced by tooling
Araçla zorlanır — review'da yeniden tartışılmaz:
- `<Nullable>enable</Nullable>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `.editorconfig` + `dotnet format --verify-no-changes`
- `web/`: ESLint + `tsc --noEmit` (TypeScript `strict`)

Yeni bir kural mümkün olduğunda `scripts/check`'e bağlanır — prose tavsiyedir, araç kanundur.
