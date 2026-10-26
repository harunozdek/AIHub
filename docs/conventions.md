# Conventions

> **Bootstrap'te dolduruldu (2026-10-26).** Burada yalnızca *gerçek* kurallar vardır: her kural ya
> araçla zorlanır (tercih edilen) ya da review'da kontrol edilir. Temenniler buraya yazılmaz.

## Language
<!-- Set at bootstrap (step 0) and mirrored in AGENTS.md. Changing it later is allowed — edit both
     places; existing documents are not translated retroactively. -->
- Chat language (interviews, sessions): **Türkçe**
- Document language (docs/, specs, plans, ADRs, review and verify reports): **Türkçe**
- Always English (protocol, not prose): ANEW core files, template headings and field labels,
  `Status:` values (Draft / Approved / In progress / Shipped), `Approved by / on:`, `Source:`.
- Code identifiers, branch names, commit messages: **Türkçe**, şu iki istisnayla:
  (1) `docs/domain.md`'deki domain terimleri kodda tanımlı (İngilizce) biçimini korur — sözlük ile
  kod birebir örtüşsün diye; (2) Conventional Commits önekleri (`feat`, `fix`, `refactor`, `docs`)
  ve dal önekleri (`feature/`, `fix/`, `trivial/`) protokoldür, İngilizce kalır.
- Agent messages (handoff summaries, refusals, `Next:` lines): chat language; command names, file
  paths and protocol fields verbatim.

## Language & framework versions
- C# / .NET **10** (SDK 10.0.100; makinede 9.0.311 de var — `global.json` ile 10.x sabitlenir).
- ASP.NET Core 10 (Web API, yalnızca backend).
- Test: xUnit + `WebApplicationFactory` (`docs/testing.md`).

## Naming
- Dosya ve tip adları: C# standardı — `PascalCase` tip, `camelCase` yerel değişken, dosya adı =
  içindeki tipin adı.
- Dikey dilim klasörü = yeteneğin adı; dilim içindeki her şey o klasörde durur
  (`docs/architecture.md`, FD-1).
- Test adları davranışı anlatır, uygulamayı değil (`docs/testing.md`).
- Dal adları: `feature/<spec-no>-<kısa-ad>` (`docs/git.md`).

## Error handling
Tek kutsal desen: **`Result<T>` + sınırda ProblemDetails**.
- `Core`'da *beklenen* hatalar (Quota aşımı, bilinmeyen Model, Provider reddi) `Result<T>` olarak
  döner — kontrol akışıdır, exception değil. Gateway'de "Provider 429 verdi" normal bir sonuçtur;
  exception gürültüsüne çevrilmez.
- Exception yalnızca gerçekten beklenmeyen durumlar içindir.
- `Api` sınırında tek bir middleware her şeyi RFC 9457 **ProblemDetails**'a çevirir.
- Kullanıcıya asla sızmaz: Provider kimlik bilgileri, ham Provider yanıt gövdesi, yığın izi.

## Data rules
- Zaman damgaları **UTC**, tipi `DateTimeOffset`. `DateTime.Now` kullanılmaz.
- Para ve maliyet alanları **`decimal`**. Token maliyeti çarpımında float yuvarlaması doğrudan
  faturaya yazılır — bu yüzden kural, tercih değil.
- Kimlikler **GUID v7** (sıralı; indeks dostu ve dışa açılabilir). Artan `int`/`long` kimlik
  dışa açılmaz.

## Enforced by tooling
Araçla zorlanır — review'da yeniden tartışılmaz:
- `<Nullable>enable</Nullable>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `.editorconfig` + `dotnet format --verify-no-changes` (`scripts/check`, `format` adımı)

Yeni bir kural mümkün olduğunda `scripts/check`'e bağlanır — prose tavsiyedir, araç kanundur.
