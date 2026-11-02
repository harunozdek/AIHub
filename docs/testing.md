# Testing

> **Bootstrap'te dolduruldu, 2026-10-26'da ADR 0001 ile revize edildi.**

## The contract
- Every acceptance criterion maps to at least one test (criterion ↔ test map lives in the plan).
- Tests assert **behavior**, not implementation details or mere status codes.
- The whole suite runs inside `scripts/check` — one command, everywhere.

## Frameworks & layout
- **Backend:** xUnit. Birim testler `Domain` ve `Application` için (çerçevesiz, ağsız);
  entegrasyon testleri `WebApplicationFactory` ile tam HTTP yüzeyine karşı (süreç içi).
- Testler `tests/` altında ve aynaladıkları katmanı adlarında taşır
  (`tests/AIHub.Application.Tests`, `tests/AIHub.Api.Tests`).
- **Panel (`web/`):** e2e için **Playwright** — `--workers=1`, headless, **yalnızca tam profil**
  (`[fast]` etiketlenmez; e2e asla iç döngüde koşmaz).
- Test adı davranışı anlatır, uygulamayı değil.

### Dış sistemler: PostgreSQL, Redis, AI sağlayıcısı
- **AI sağlayıcısı:** `./scripts/check` **hiçbir zaman** gerçek bir AI sağlayıcısına çağrı yapmaz.
  Testler `IAiAnalysisService`'in sahte (fake) uygulamasını kullanır. Gerçek çağrı hem para harcar
  hem determinizm kuralını ihlal eder — ağ kaynaklı kırmızı test, kodun hatası değildir.
- **PostgreSQL ve Redis:** entegrasyon testleri gerçek bir veritabanı ve Redis'e ihtiyaç duyar;
  bunlar test fixture'ı tarafından konteyner olarak ayağa kaldırılır. **Yaşam döngüsü sahibi tek
  bir bileşendir:** fixture konteyneri başlatır ve **hata ve timeout dahil** aynı adımın içinde
  durdurur (Resource hygiene, kural 1 ve 4). Bu adımlar tam profile aittir.
- **Veri hijyeni (kural 5):** her koşu taze bir şema/veritabanı ile başlar; bir koşunun yazdığı
  satır, stream girdisi veya dosya bir sonraki koşuya geçmez.

### App-under-test sahipliği
Playwright adımı backend'i ve `web/`i kendisi başlatır ve kendisi durdurur; bu adımın dışında
hiçbir yerde uygulama ayağa kaldırılmaz. `next dev`, `dotnet watch` ve benzeri etkileşimli modlar
`scripts/check`'e asla girmez (kural 4).

- **Tek sahip `web/e2e/global-setup.ts` ve `global-teardown.ts`'tir.** Setup PostgreSQL 17,
  ve Redis 7'yi testcontainers-node ile, backend'i derlenmiş dll'den (tek süreç, alt süreç
  bırakmaz; ilk yönetici hesabı ayarlı) ve paneli `next start` ile boş portlarda başlatır. Herhangi bir adım başarısız olursa başlatılan her şeyi kendisi durdurur;
  teardown kalanı durdurur.
- **Giriş AIHub'ın kendi hesaplarıyladır (ADR 0002).** e2e panelin giriş formunu, açılışta
  oluşan ilk yönetici hesabıyla kullanır (`web/e2e/kimlik.ts`). Backend entegrasyon testleri
  token'ı Api'nin imza anahtarıyla, depodaki gerçek bir test kullanıcısına bağlı üretir
  (`tests/AIHub.Api.Tests/Altyapi/TestToken.cs`). Kullanıcı tablosunun tümüne bakan testler (ilk
  yönetici, "son aktif kullanıcı") kendi boş veritabanında çalışır (`TestVerisi.BosVeritabaniAsync`).
- **Sıra projelerle sabittir:** `bos-depo` (henüz hiç kayıt yokken) → `panel` → `ariza`
  (PostgreSQL'i durdurur, bu yüzden en sondadır). e2e verisi `web/e2e/seed.ts` ile container
  içindeki `psql`'den yazılır; her test kendi servis adıyla çalışır.

## What must be tested
- Her iş kuralı (`docs/domain.md`, BR-1 … BR-5) en az bir testle.
- Her domain invariant'ı — özellikle: saklanmış bir LogEntry'nin değişmezliği ve maskelenmemiş
  hassas verinin hiçbir koşulda yazılmadığı.
- Her yasaklı bağımlılık (`docs/architecture.md`, FD-1 … FD-4) bir mimari testle.
- `Api` sınırının hata sözleşmesi: beklenen her hata ProblemDetails olarak çıkar; bağlantı dizesi,
  API anahtarı, yığın izi veya AI sağlayıcı ham yanıtı sızmaz.
- Ingestion'ın asenkron sözleşmesi: istek `202` döner ve kayıt tüketici tarafından yazılır (BR-1).
- ServiceName'in istemci gövdesinden değil, API anahtarından belirlendiği
  (`docs/security.md`, AuthN/AuthZ).
- Her kabul kriteri (spec ↔ test eşlemesi plandadır).

## Protected-tests rule
Weakening asserts, deleting, or skipping tests to reach green is forbidden. A red test triggers
`prompts/recovery/red-test.md` (R-02) — first decide what is wrong: code, test, or spec.

## Characterization tests
Before refactoring untested code (`workflows/refactor.md`) and for brownfield change requests
(`workflows/change-request.md`, Preserved behavior), pin the current behavior first — warts
included. They are written from observation, not from what the code "should" do.

## Evidence for UI criteria
A screenshot is evidence for a UI criterion; for change requests, before/after screenshots that
also show the preserved behavior. Screenshots go into the PR, not the repo (Evidence policy).
A screenshot proves a criterion only against the spec's "User experience": every state it lists
(empty, loading, error, success, no permission), compared with the linked design if there is one.

## Evidence policy
1. **Evidence is referenced, not stored.** Each acceptance criterion's evidence is a pointer: the
   test's name and the command that reproduces it (`./scripts/check`). The criterion ↔ evidence
   table in the spec/plan is the only evidence artifact that lives in the repo.
2. **Reproducible evidence is never filed.** No saved check output, console dumps, coverage reports
   or log copies in the repo — git history and CI runs already archive every execution.
3. **Non-reproducible evidence** (a screenshot of a manual UI check, a one-off measurement, an
   external-system confirmation) goes into the pull request (description or attachment); the
   evidence table links to it. It is the exception, not the rule.
4. **Never clone or copy the repository inside its own working tree.** Clean-clone verification
   runs in a temporary directory outside the repo; only the one-line result (command + green/red)
   is recorded in the evidence table. The clone is always deleted, never kept.
5. **There is no `evidence/` (or `.evidence/`) directory in this workspace.** An agent that feels
   the need to create one is about to violate rule 1. `scripts/doctor` warns on both violations.

## Resource hygiene
1. **A check run leaves nothing running.** No survivors of any kind: build/compile daemons,
   package-manager services, test hosts and parallel workers, watch modes, browsers, the application
   under test, containers or emulators started for the run. Agents run check an order of magnitude
   more often than humans; per-run residue compounds into machine starvation.
2. **The residue test** (the rule's definition, not advice): run `./scripts/check` twice back to
   back; after each run, process count and memory return to the baseline measured before the first.
   `./scripts/residue` runs it (`--fast` for the fast profile) and names every survivor with its
   command line; it lists anything started meanwhile too, so run it on an otherwise idle machine.
   Rerun it whenever `scripts/check.conf` changes.
3. Warm-process caches trade memory for speed for humans who build rarely; with an agent, trade the
   seconds back for a flat memory profile.
4. Interactive modes (watchers, dev servers, REPLs) never belong in check. e2e runs single-worker
   and headless by default; one component owns the app-under-test lifecycle — started for the run,
   stopped at the end, including on failure and timeout.
5. **Data counts too.** Every run starts from a known state: what a run writes — database rows,
   files, queues, uploaded objects — never reaches the next run, also after a run killed halfway.
   Use a fresh database (or schema) per run, or seed and clean up inside the same step, keeping its
   exit code. The residue test measures processes and memory only; tests that pass on a clean state
   and fail on a used one are this rule failing, not the code (R-02, environment first).

## Two speeds, one contract
1. `./scripts/check` is still the one way to ask "is this good?"; `--fast` is a speed of the
   contract, not a second contract.
2. **Fast green is a working state; only full green is evidence.** Per step, per finding, per
   iteration → `--fast`. Segment exits, PROVE steps, recovery-ramp exits, the verify table and CI
   → `CHECK GREEN (full, …)`. `scripts/doctor` warns when a shipped spec, or in CI the pull
   request description, shows `CHECK GREEN (fast, …)` and no `(full, …)`.
3. Fast: compile and the quick tests (unit, lint). Full only: e2e, long integration suites, and
   any single step over ~a minute — the minute is a per-step threshold, not a total for the fast
   profile; fast has no fixed total. Bootstrap decides per stack and tags the steps `[fast]` in
   check.conf. When fast grows too long to iterate with, take steps out in this order: lint
   first, then formatting, then unit tests; compilation stays longest.
4. **Split a test step by a selector, never by a list.** The fast step selects with a filter
   (a category, trait, tag or namespace) and the full-only step runs the same filter inverted,
   so "fast ∪ the rest = every test" holds by construction: a test added later still runs in
   one of them. Splitting by naming projects or files leaves a silent hole — a new one that is
   not added to check.conf runs in neither profile while check stays green.
5. Durations come from check itself (`<== <step> (n s)` per step, the total in the green line).
   Decide a split, or report a profile's duration, on the median of at least 3 runs — single
   runs vary too much to decide on.
6. **A full green is evidence for its commit — run it once.** When `CHECK GREEN (full, …)` exists
   for the exact commit at hand (the builder's handoff, or a CI run), review, verify and the gates
   cite it — the line, plus the CI run link and its commit SHA, which must equal HEAD — instead of
   running full again. A new commit needs a new full run.
7. **Writing one test runs that test.** While writing or fixing a single test, run it alone with
   the test tool's own filter, without the suites that depend on it. That is authoring, never
   evidence; `scripts/check` stays the way to ask "is this good?". Repeat runs are for the
   flaky-test hunt only (R-03).

## Determinism
Flaky tests are fixed, not retried or skipped — see R-03. Evidence of a fix: 5 consecutive green runs.
