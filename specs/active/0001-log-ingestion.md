# Spec 0001 — Log ingestion (kabul, maskeleme, kalıcı saklama)

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0001-plan.md`

## Intent
Kurum içindeki Java ve .NET Core servisleri, ürettikleri logları ve exception raporlarını tek bir
merkeze gönderebilmek istiyor. Böylece bir hata olduğunda ekipler ilgili servisin sunucusuna tek
tek bakmak zorunda kalmaz. Başarı şöyle tanımlanır: kayıtlı bir servis bir log gönderdiğinde
AIHub logu hemen kabul eder ve gönderen servisi bekletmez. Log, hassas verileri maskelenmiş olarak
kalıcı biçimde saklanır ve AIHub kısa süreli bir arıza yaşasa bile kabul edilmiş hiçbir kayıt
kaybolmaz. Bu spec yalnızca kabul ve saklama ile ilgilidir. AI kök neden analizi, panel, retention,
rate limiting ve API anahtarı yönetim ekranı bilerek kapsam dışında bırakıldı.

## Requirements
- **R-1 Kimlik doğrulama:** Yalnızca geçerli ve iptal edilmemiş bir servis API anahtarı taşıyan
  gönderimler kabul edilir. Anahtarı olmayan, tanınmayan veya iptal edilmiş istek reddedilir ve
  hiçbir kayıt oluşmaz.
- **R-2 ServiceName anahtardan gelir:** Saklanan kaydın ServiceName'i, gönderimi yapan anahtarın
  bağlı olduğu servistir. Gövdede başka bir servis adı gönderilirse bu ad dikkate alınmaz
  (`docs/security.md`).
- **R-3 Doğrulama:** Her gönderim bir LogLevel (`Info`, `Warning`, `Error`, `Critical`) ve boş
  olmayan bir Message taşır. ExceptionType, StackTrace, kaynak zaman damgası (Timestamp) ve
  Metadata isteğe bağlıdır. Tanımsız LogLevel, eksik veya boş Message, nesne olmayan Metadata, biçimi
  bozuk veya saat dilimi belirtmeyen Timestamp ve boyut sınırını aşan gönderim reddedilir. Reddin nedeni yanıtta okunabilir biçimde yer alır
  (BR-2).
- **R-4 Hızlı kabul:** Geçerli bir gönderim kalıcı depolamaya yazılmasını beklemeden "kabul
  edildi" yanıtı alır. Yanıtta kayda verilen benzersiz kimlik bulunur (BR-1).
- **R-5 Maskeleme:** Message, StackTrace ve Metadata değerleri, kayıt kuyruğa veya depoya hiçbir
  biçimde yazılmadan önce şu desenlere karşı taranır: e-posta, T.C. kimlik numarası, bearer
  token, bağlantı dizesi parolası, API anahtarı. Eşleşen kısım, türünü belirten bir yer tutucuyla
  değiştirilir (ör. `[MASKED:email]`). Metnin geri kalanı değişmez (BR-3).
- **R-6 Kalıcılık:** Kabul edilen her kayıt, kabul anından itibaren kısa süre içinde kalıcı depoda
  maskelenmiş haliyle tam olarak bir kez bulunur. Kayıtta servis tarafından bildirilen zaman ile
  AIHub'ın kaydı aldığı zaman ayrı ayrı tutulur.
- **R-7 Kayıpsızlık:** Kalıcı depo geçici olarak erişilemezse kabul edilmiş kayıtlar kaybolmaz.
  Depo yeniden erişilebilir olduğunda kayıtlar yazılır. AIHub süreci yeniden başlarsa da yazılmamış
  kayıtlar kaybolmaz.
- **R-8 Kuyruk erişilemezse:** Kayıt güvenli biçimde kuyruğa alınamıyorsa gönderim kabul edilmiş
  sayılmaz. Gönderen servise sonra yeniden denemesini söyleyen bir yanıt döner. Yanıtta iç sistem
  ayrıntısı bulunmaz.
- **R-9 Değişmezlik:** Saklanan bir kayıt sonradan güncellenmez (domain invariant).

## User experience
none — no user interface

## Constraints & out of scope
- **Boyut sınırları:** gönderim gövdesi en fazla 256 KB, Message en fazla 32 KB, ExceptionType en
  fazla 512 karakter. Metadata için ayrı bir derinlik veya boyut sınırı yoktur; gövde sınırı
  geçerlidir.
- **Tek kayıt / gönderim:** V1'de bir gönderim tek bir log kaydı taşır.
  Toplu gönderim sonraki bir spec'e kalır.
- **Gecikme hedefi:** "Kabul edildi" yanıtı, kalıcı depo yavaş veya erişilemez olsa bile
  depolamadan etkilenmez. Normal koşulda kayıt kabulden sonra en geç 5 saniye içinde depoda olur.
- **Hatalar** ProblemDetails biçimindedir. Hiçbir hata yanıtında anahtar, bağlantı dizesi, yığın
  izi veya başka bir servise ait içerik yer almaz.
- **Kapsam dışı:**
  - API anahtarı oluşturma, listeleme ve iptal etme ekranı veya komutu. Testler ve geliştirme
    ortamı anahtarı doğrudan hazırlar. Anahtar yönetimi Spec 0002 olarak ele alınacak.
  - Rate limiting. Ayrı bir spec olacak ve ürüne çıkmadan önce yapılması zorunlu
    (`docs/security.md`).
  - Retention ve otomatik silme (BR-4).
  - AI kök neden analizi (BR-5).
  - Panel ve log sorgulama.
  - Toplu gönderim.
  - Java ve .NET istemci kütüphaneleri veya appender'ları.
- **Maskeleme ayrıntıları:** Bağlantı dizesinde yalnızca parola değeri maskelenir; host ve
  veritabanı adı korunur. T.C. kimlik numarası yalnızca kontrol hanesi geçerliyse maskelenir.
- **Kaynak zamanı:** Gelecekteki veya çok eski bir zaman damgası reddedilmez, olduğu gibi
  saklanır. Alınma zamanı ayrıca tutulur.
- **Tekrar gönderim:** İstemci aynı kaydı yeniden gönderirse iki ayrı kayıt oluşur. Bu V1'de
  kabul edilmiştir. "Tam olarak bir kez" ifadesi yalnızca AIHub içindeki işlemeyi kapsar.
- **Split:** proposed "kabul ve maskeleme" + "kalıcılık" olarak iki spec; kept as one — harun.ozdek,
  tek kullanıcı yeteneği var ve ayrılan ilk parça tek başına doğrulanamaz.

## Acceptance criteria
- [ ] AC-1 — Geçerli anahtarla gönderilen geçerli bir `Error` kaydı "kabul edildi" yanıtı ve bir
      kayıt kimliği alır. Aynı kimlikle kayıt en geç 5 saniye içinde depoda bulunur ve
      ServiceName, LogLevel, Message, ExceptionType, StackTrace, Metadata, kaynak zamanı ile
      alınma zamanını içerir.
- [ ] AC-2 — Anahtarsız, tanınmayan anahtarlı veya iptal edilmiş anahtarlı gönderim yetkisiz
      yanıtı alır. Depoda ve kuyrukta hiçbir kayıt oluşmaz.
- [ ] AC-3 — Gövdede `serviceName: "baska-servis"` gönderen `odeme-servisi` anahtarının kaydı
      `odeme-servisi` adıyla saklanır.
- [ ] AC-4 — LogLevel değeri `Debug`, boş veya eksik olan gönderim reddedilir. Dört geçerli
      değerin her biri kabul edilir.
- [ ] AC-5 — Message eksik ya da yalnızca boşluk olan, Metadata'sı JSON nesnesi olmayan, biçimi
      bozuk veya saat dilimi belirtmeyen Timestamp taşıyan ya da boyut sınırını aşan gönderim
      reddedilir ve kayıt oluşmaz. Tam sınırda olan gönderim kabul
      edilir.
- [ ] AC-6 — Yalnızca zorunlu alanları içeren gönderim (isteğe bağlı alanlar yok) kabul edilir ve
      eksik alanlar depoda boş olarak saklanır. Kaynak zamanı yoksa yalnızca alınma zamanı dolar.
- [ ] AC-7 — Message, StackTrace ve iç içe bir Metadata değerinde geçen e-posta, geçerli bir T.C.
      kimlik numarası, bearer token, bağlantı dizesi parolası ve API anahtarı, depoda ve kuyruk
      içeriğinde yalnızca türünü belirten yer tutucu olarak görünür. Ham değer hiçbir yerde
      bulunmaz.
- [ ] AC-8 — Hassas desen içermeyen metin ve geçersiz kontrol haneli 11 haneli bir sayı (ör. bir
      sipariş numarası) değişmeden saklanır.
- [ ] AC-9 — Kalıcı depo erişilemezken kabul edilen kayıtlar, depo geri geldiğinde eksiksiz ve
      birer kez yazılır. Süreç kuyruktaki kayıtlar işlenmeden yeniden başlatıldığında da sonuç
      aynıdır.
- [ ] AC-10 — Kuyruk erişilemezken yapılan geçerli gönderim, "sonra yeniden dene" anlamına gelen
      bir yanıt alır. Yanıt iç sistem ayrıntısı içermez ve bu gönderim için hiçbir kayıt oluşmaz.
- [ ] AC-11 — Hiçbir hata yanıtında anahtar değeri, bağlantı dizesi veya yığın izi bulunmaz.

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
