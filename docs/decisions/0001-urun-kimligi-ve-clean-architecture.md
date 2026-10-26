# ADR 0001 — Ürün kimliği: merkezi log/hata analiz platformu, Clean Architecture ile

- Status: Accepted
- Date: 2026-10-26

## Context

Bu workspace 2026-10-26'da bir kez bootstrap edildi. O oturumda ürün tanımı için insana seçenekler
sunuldu ve ajanın önerdiği seçenek — "çok sağlayıcılı LLM gateway" — işaretlendi. Tek ipucu dizin
yolu (`D:\Projects\NetCore\AIHub`) olduğu için bu öneri koddan değil, isimden türetilmiş bir
tahmindi. Dokümanlar (`AGENTS.md`, `docs/architecture.md`, `docs/domain.md`, `docs/testing.md`)
bu tahmine göre yazıldı ve `023c7cd` olarak commit edildi.

Aynı gün, ilk `/new-feature` çağrısı ürünün gerçekte ne olduğunu ortaya koydu: kurum içindeki
Java ve .NET Core servislerinden gelen logları ve exception raporlarını tek merkezde toplayan,
depolayan ve AI desteğiyle kök neden analizi üreten bir gözlemlenebilirlik platformu — ayrıca
Clean Architecture, PostgreSQL, Redis ve bir Next.js yönetim paneli şartları.

Çelişki dokuz boyutta ölçüldü (ürün kimliği, mimari stil, modül sınırları, domain sözlüğü,
kapsam dışı ilan edilenler, yasaklı bağımlılıklar, test stratejisi, yığın, kalıcı depo). Bir
boyut dışında hepsi doğrudan karşıtlıktı. Değişmez kural 8 gereği iş durduruldu ve R-10
(`prompts/recovery/ambiguity.md`) rampası kullanıldı; insan "bootstrap'i revize modunda yeniden
koş + bir ADR yaz" seçeneğini seçti. Bu ADR, o kaybolacak gerekçeyi kayıt altına alır.

## Decision

AIHub'ın ürün kimliği **AI destekli merkezi log ve hata analiz platformu (Observability &
Incident Hub)** olarak sabitlenir ve mimari stil **Clean Architecture** (Domain → Application →
Infrastructure → Api, bağımlılıklar içe doğru) olur. İlk bootstrap'in ürettiği "LLM gateway /
dikey dilimler / Api-Core-Providers" doküman seti tümüyle geçersizdir ve revize bootstrap
tarafından değiştirilmiştir. Proje adı `AIHub` olarak korunur.

## Consequences

Kazandığımız: spec, plan ve review artık aynı gerçeklikten çalışır — yazılacak ilk spec kendi
`docs/architecture.md`'siyle çelişmez. Katman yasakları (bu ADR ile birlikte dört tanesi
bağlayıcı) mimari testle assert edilebilir, yani kural prose olarak kalmaz. Redis ve PostgreSQL
kararı, ilk bootstrap'in "kalıcı depo kararı yok" boşluğunu da kapatır.

Ödediğimiz: `023c7cd`'deki doküman emeği çöpe gitti ve o commit artık yanıltıcı bir tarihsel
kayıt — bu ADR olmadan ileride "AIHub neden LLM gateway olarak yazılmış?" sorusu cevapsız
kalırdı. Clean Architecture, dikey dilimlere göre daha fazla proje ve daha fazla tören getirir;
bunu dört katmanı gerçekten ayrı tutma karşılığında kabul ediyoruz. Ayrıca yığın tek araç
zincirinden (`dotnet`) ikiye çıktı (`dotnet` + Node/Next.js), bu da `scripts/check`'i ve CI'ı
kalıcı olarak daha karmaşık yapar.

## Alternatives considered

- **Yalnızca ADR'lerle üzerine yazmak** (bootstrap'i yeniden koşmadan): karar geçmişi en zengin
  kalan yol olurdu, ancak `AGENTS.md` özeti, dört doküman ve `check.conf` elle güncellenecekti —
  bir bölümü atlama riski, kazancından büyüktü.
- **Log platformunu ayrı bir dizinde temiz kurmak**: iki ürün gerçekten ayrı olsaydı doğru yoldu.
  Ancak LLM gateway hiç var olmadı — tahminden doğmuş bir dokümandı, korunacak bir şey yoktu.
- **Çelişkiyi yok sayıp ANALYZE'a geçmek**: en hızlı görünen yol. Yazılacak spec kendi mimari
  dokümanına aykırı olacağı için REVIEW'da kendi kurallarına çarpardı; FD-1/FD-2 testleri
  anlamsızlaşırdı. `scripts/doctor` yeşil kalır, dokümanlar yalan söylerdi — en pahalı seçenek.

## Revisit triggers

- Yönetim panelinin ayrı bir ekibe veya ayrı bir dağıtım hattına geçmesi (monorepo kararı düşer).
- Log hacminin PostgreSQL'in makul sınırlarını aşması (OpenSearch/ClickHouse gibi bir log deposu
  gerekebilir; bu ADR'nin veri deposu varsayımı düşer).
- Katman sayısının taşıdığı törenin, kazandırdığı ayrımdan pahalı hale gelmesi — Application
  katmanı yalnızca Infrastructure'a delege eden sınıflardan oluşmaya başlarsa.
