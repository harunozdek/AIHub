# Spec 0001 — İskelet ve doğrulama hattı

- Status: Approved
- Mode: lite
- Plan: `specs/plans/0001-plan.md`

## Intent

Ekip, AIHub'ın ilk gerçek yeteneğini (log ingestion) yazmaya başlamak üzere; ancak şu anda
`./scripts/check` dokuz adımının hepsi "GREENFIELD NO-OP" basıyor, yani yeşil sonuç hiçbir şeyi
doğrulamıyor. Bu spec, doğrulama hattını gerçek hâle getirir: dört katmanlı çözüm kurulur, mimari
sınırlar insan dikkatine değil otomatik teste bağlanır, ve derleyici/biçim kuralları araçla
zorlanır hâle gelir. Başarı ölçütü tek cümleyle: *bundan sonra "check yeşil" demek bir kanıt
olur.* Bu aşamada **bilinçli olarak yapılmayan**: log alma uç noktası, veritabanı kalıcılığı,
Redis bağlantısı, maskeleme, AI analizi ve yönetim paneli — bunlar 0002-0004 spec'lerine aittir.

## Requirements

1. Çözüm, `docs/architecture.md`'de tanımlı dört katmandan oluşur ve katmanlar arası bağımlılık
   yönü içe doğrudur.
2. Katman sınırlarının ihlali **otomatik olarak** yakalanır: bir ihlal eklendiğinde doğrulama
   komutu kırmızıya düşer, review'ın fark etmesine gerek kalmaz.
3. `docs/conventions.md`'de "araçla zorlanır" diye yazılan kurallar (nullable, uyarıların hata
   sayılması, kod biçimi) gerçekten zorlanır: kural ihlal edildiğinde doğrulama kırmızıya düşer.
4. Uygulama ayağa kalkar ve kimlik doğrulaması gerektirmeyen bir canlılık sorgusuna cevap verir;
   bu, tüm bağımlılık kablolamasının çalıştığının kanıtıdır.
5. Doğrulama komutu artık hiçbir adımda "no-op" davranmaz; test adımı gerçekten test koşar ve
   koştuğu test sayısı sıfırdan büyüktür.
6. Yerel geliştirme için PostgreSQL ve Redis'i ayağa kaldıran bir kompozisyon dosyası depoda
   bulunur (bu spec'te kullanılmaz; 0002 ve 0003 için hazırdır).
7. Doğrulama komutu, çalıştıktan sonra arkada süreç bırakmaz (`docs/testing.md`, Resource hygiene).

## User experience

none — no user interface.

Bu spec'in tüketicisi geliştirici ve CI'dır; gözlemlenebilir yüzeyi `./scripts/check` çıktısıdır.
Canlılık sorgusu bir operatör yüzeyidir, son kullanıcı arayüzü değildir.

## Constraints & out of scope

**Kapsam dışı (bilinçli):** log alma uç noktası ve sözleşmesi · API anahtarı doğrulama ·
maskeleme · EF Core/PostgreSQL kalıcılığı ve migration'lar · Redis bağlantısı ve Stream
tüketicisi · retention · `IAiAnalysisService` ve AI analizi · global exception middleware ·
`web/` paneli ve Playwright kurulumu · rate limiting.

**Bölme (onaylandı 2026-10-26):** 0001 İskelet ve doğrulama hattı → 0002 Log ingestion →
0003 Kalıcılık ve retention → 0004 AI analiz arayüzü. 0001 önce gelir çünkü sonraki üç spec'in
"CHECK GREEN" kanıtı, bu spec tamamlanmadan boş bir iddiadır.

**Karara bağlandı, sonraki spec'lere devredildi** (CLARIFY'da kararlaştırıldı, burada uygulanmaz):
- *0002:* Gövde hem tek kayıt hem kayıt dizisi kabul eder, dizide üst sınır olur.
- *0002:* Redis erişilemezse istek `503` + `Retry-After` ile reddedilir; `202` dönülmez, sessiz
  kayıp olmaz.
- *0002:* API anahtarları konfigürasyondan seed edilir, hash'li saklanır; admin uç noktası yok.
- *0003:* Kompozisyon dosyası yalnızca yerel geliştirme içindir; entegrasyon testleri kendi
  konteyner örneğini açar (test izolasyonu ve veri hijyeni kuralı).

**Sınırlar:** `scripts/check.conf`'taki greenfield guard satırları bu spec'te **kaldırılır** —
guard'lı bir adım yanlış-yeşil üretir. Fast profilinde hiçbir adım ~1 dakikayı geçmez
(`docs/testing.md`, Two speeds).

**FD-4 bu spec'te otomatik teste bağlanmaz.** Self-critique sonucu: "Api iş mantığı içermez" için
güvenilir bir otomatik vekil bulunamadı; kural `docs/architecture.md`'de review ile uygulanacak
şekilde işaretlendi. FD-1, FD-2 ve FD-3 testlenir (AC-4, AC-5, AC-6).

## Acceptance criteria

- [ ] AC-1 — Kökte `AIHub.sln` bulunur ve çözüm `docs/architecture.md`'deki dört katman projesini
      içerir; `./scripts/check` çıktısında hiçbir adım "GREENFIELD NO-OP" basmaz.
- [ ] AC-2 — Kasıtlı bir derleyici uyarısı eklendiğinde `build` adımı kırmızıya düşer (nullable
      etkin + uyarılar hata sayılır); uyarı kaldırıldığında yeşile döner.
- [ ] AC-3 — Kod biçimi bozulduğunda `format` adımı kırmızıya düşer; biçim düzeltildiğinde yeşile
      döner.
- [ ] AC-4 — `AIHub.Domain`'e başka bir katman veya dış NuGet referansı eklendiğinde FD-1 testi
      kırmızıya düşer.
- [ ] AC-5 — `AIHub.Application`'a `AIHub.Infrastructure` referansı eklendiğinde FD-2 testi
      kırmızıya düşer.
- [ ] AC-6 — Bir EF Core türü `AIHub.Application` veya `AIHub.Api` içinde kullanıldığında FD-3
      testi kırmızıya düşer.
- [ ] AC-7 — Çalışan uygulamada `GET /health` kimlik doğrulaması olmadan `200` döner ve yanıt
      gövdesi ortam, sürüm veya bağımlılık durumu bilgisi içermez; bu, `WebApplicationFactory` ile
      süreç içi test edilir.
- [ ] AC-8 — Fast profilin raporladığı test sayısı sıfırdan büyüktür ve full profilin test sayısı
      fast profilinkinden küçük değildir (fast > 0, full ≥ fast); bölmenin sessiz deliği olmadığı
      bu iki sayıyla gösterilir.
- [ ] AC-9 — Depoda PostgreSQL ve Redis tanımlayan bir kompozisyon dosyası bulunur ve geçerliliği
      VERIFY sırasında tek seferlik gözlemle doğrulanır (`docker compose config` hatasız döner).
      Bu doğrulama bir `scripts/check` adımı **değildir** — aksi hâlde Docker'ı kapalı olan
      geliştiricide check kırmızıya düşerdi.
- [ ] AC-10 — `./scripts/check` ve `./scripts/check --fast` yeşildir; `./scripts/residue` ve
      `./scripts/residue --fast` temizdir — survivor listelenirse her biri AIHub araç zincirine ait
      olmadığı gerekçesiyle yazılı olarak kapatılır.

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
