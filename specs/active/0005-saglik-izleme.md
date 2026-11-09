# Spec 0005 — Uygulama sağlık izleme ve down uyarısı

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0005-plan.md`

## Intent
Ekip, AIHub'a log gönderen uygulamaların ayakta olup olmadığını panelden takip etmek istiyor
(harun.ozdek, 2026-11-02). Bugün bir uygulama çöktüğünde bunu ancak loglar kesilince ya da
kullanıcılar şikâyet edince fark ediyorlar. Panele giriş yapan kullanıcı her uygulama için bir
sağlık adresi kaydeder. AIHub bu adresi arka planda düzenli olarak dener. Uygulama down olunca
paneldeki her ekranda uyarı görünür, uygulama düzelince uyarı kalkar. Başarı şöyle tanımlanır: bir
uygulama durduktan sonra en geç iki dakika içinde panelde uyarı görünür. E-posta, Teams gibi panel
dışı bildirimler, geçmiş grafikleri ve uptime yüzdesi bilerek kapsam dışıdır. Hata sayılarını ve
uygulama durumlarını bir arada gösteren dashboard ayrı bir spec'tir (0006).

## Requirements
- **R-1 İzlenen uygulama:** İzlenen her uygulama bir ServiceName'e ve bir sağlık adresine (URL)
  bağlıdır.
  - Bir ServiceName en fazla bir kez izlenir.
  - ServiceName, log kayıtlarındaki ServiceName ile aynı kurala uyar. O ada sahip bir servisin daha
    önce log göndermiş olması gerekmez.
- **R-2 Yönetim:** Giriş yapmış her kullanıcı izlenen uygulama ekler, sağlık adresini değiştirir ve
  uygulamayı izlemeden çıkarır.
  - Kimin, ne zaman eklediği ve en son kimin değiştirdiği saklanır ve listede görünür.
  - İzlemeden çıkarılan uygulama artık denetlenmez ve uyarı üretmez. Aynı ServiceName yeniden
    eklenirse eski durumu taşımaz, Bilinmiyor durumundan başlar.
  - İki kullanıcı aynı uygulamayı aynı anda düzenlerse son kaydeden kazanır.
- **R-3 Adres kuralı:** Sağlık adresi yalnızca `http` veya `https` olabilir ve en fazla 2048
  karakterdir.
  - Kullanıcı adı ve şifre taşıyan adres (`http://kullanici:sifre@…`) kabul edilmez.
  - Adres `169.254.0.0/16` ya da IPv6 link-local (`fe80::/10`) bir adrese çözülüyorsa kaydedilmez.
    Denetim sırasında bu adreslerden birine çözülürse denetim yapılmaz ve başarısızlık sayılır.
  - Kurum içi adresler ve `localhost` kabul edilir: izlenen uygulamalar kurum içindedir.
- **R-4 Denetim:**
  - AIHub her izlenen uygulamanın adresine 30 saniyede bir istek gönderir. Panelde kimse oturum
    açmamış olsa da denetim sürer.
  - Yanıt 5 saniye içinde gelmezse denetim zaman aşımına uğrar.
  - 2xx durum kodu başarıdır. Başka her durum kodu, zaman aşımı, bağlantı hatası ve engellenen adres
    başarısızlıktır.
  - Yönlendirme (3xx) izlenmez ve başarısızlık sayılır. Yanıtın gövdesi okunmaz.
  - TLS sertifikası doğrulanamayan `https` adresi başarısızlıktır. Doğrulamayı atlama seçeneği
    yoktur.
  - Her uygulama her aralıkta bir kez denetlenir. AIHub birden çok örnekle çalışsa da sayaç her
    aralıkta en fazla bir artar. Aralık, uygulamanın 30 saniyelik denetim çizelgesidir. Denetimler
    gecikse de (ör. yeniden başlatma) iki denetim arası 15 saniyeden kısa olmaz.
- **R-5 Durum:** İzlenen uygulamanın durumu üç değerden biridir: Bilinmiyor, Ayakta, Down.
  - Yeni eklenen ve adresi değiştirilen uygulama Bilinmiyor durumundadır; ilk denetimle Ayakta ya da
    başarısızlık sayacına geçer.
  - Art arda 3 başarısız denetim uygulamayı Down yapar. Tek bir başarılı denetim uygulamayı Ayakta
    yapar ve sayacı sıfırlar.
  - Durumla birlikte son denetim zamanı, son başarısızlığın nedeni ve uygulamanın hangi zamandan beri
    Down olduğu saklanır.
  - Durum uygulama yeniden başlatılınca kaybolmaz.
- **R-6 Neden:** Son başarısızlığın nedeni kullanıcıya anlaşılır biçimde gösterilir: zaman aşımı,
  bağlantı kurulamadı, sertifika hatası, HTTP durum kodu (ör. "HTTP 503"), yönlendirme, engellenen
  adres. Yanıt
  gövdesi, sunucunun iç hata ayrıntısı ya da stack trace gösterilmez.
- **R-7 Uyarı:** En az bir uygulama Down iken, oturum açıkken panelin her ekranında bir uyarı
  görünür; giriş ekranında görünmez. Uyarı Down
  olan uygulamaların adlarını ve ne zamandan beri Down olduklarını listeler.
  - Panel durumu 15 saniyede bir yeniden okur. Sayfa yenilenmeden uyarı belirir ve kalkar.
  - Uygulama Ayakta olunca ya da izlemeden çıkarılınca uyarıdan düşer.
- **R-8 Liste:** "İzlenen uygulamalar" ekranı her uygulamayı şu bilgilerle gösterir: ServiceName,
  sağlık adresi, durum, son denetim zamanı, son başarısızlığın nedeni, Down olduğu zaman, ekleyen ve
  son değiştiren. Down olan satırlar listede ayırt edilir ve en üstte durur.

## User experience
- **Akış:**
  - Üst menüde "İzlenen uygulamalar" ekranı bulunur.
  - Kullanıcı "Uygulama ekle" formunu açar, ServiceName ve sağlık adresini girip kaydeder.
  - Yeni satır Bilinmiyor durumuyla listeye girer; ilk denetimden sonra durumu güncellenir.
  - Satırdaki "Düzenle" ile sağlık adresi değişir. "İzlemeden çıkar" onay ister.
  - Bir uygulama Down olunca hangi ekranda olunursa olsun üstte uyarı belirir. Uyarıdaki uygulama
    adı "İzlenen uygulamalar" ekranına götürür.
- **"İzlenen uygulamalar" ekranının durumları:**
  - Boş: "Henüz izlenen uygulama yok" ve "Uygulama ekle" yolu.
  - Yükleniyor.
  - Hata: "yeniden dene" ile.
  - Liste: her satırda durum metinle yazılır (Bilinmiyor / Ayakta / Down). Down satırları ayrıca
    vurgulanır.
- **"Uygulama ekle" ve "Düzenle" formlarının durumları:**
  - Alan yanında doğrulama hataları: geçersiz ServiceName, geçersiz ya da kural dışı adres.
  - ServiceName zaten izleniyorsa mesaj gösterilir.
  - Kaydediliyor, başarılı.
- **Uyarının durumları:**
  - Görünmez: Down uygulama yok.
  - Görünür: bir ya da daha çok Down uygulama.
  - Durum okunamıyorsa panel uyarıyı kaldırmaz, son bilinen durumu gösterir. Bunu
    "durum güncellenemedi" notuyla belirtir.
- **Tasarım referansı:** none — yukarıdaki akış tasarımdır. Görünüm 0002 ve 0004 ekranlarının
  bileşenlerini kullanır.
- **Erişilebilirlik:**
  - Ekleme, düzenleme ve izlemeden çıkarma yalnızca klavyeyle tamamlanır.
  - Alanların görünür etiketi vardır ve hatalar alanlara bağlıdır.
  - Durum yalnızca renkle anlatılmaz, metinle de yazılır.
  - Uyarının belirmesi ekran okuyucuya duyurulur.
  - Ekranlarda WCAG 2.1 AA ihlali yoktur.

## Constraints & out of scope
- **Yetki:** Yalnızca giriş yapmış kullanıcılar izlenen uygulamaları görür ve yönetir (Spec 0004).
  Rol yoktur.
- **Kabul edilen riskler (V1, kurum içi ağ, güvenilir kullanıcılar):**
  - Kurum içi adresler serbest olduğu için giriş yapmış biri AIHub üzerinden iç ağdaki adresleri
    yoklayabilir. Başarısızlık nedeni, bir adresin açık olup olmadığını belli eder.
  - Eşzamanlı düzenlemede son kaydeden kazanır.
- **Sınırlar:**
  - En fazla 200 izlenen uygulama.
  - Denetim aralığı (30 sn), zaman aşımı (5 sn), Down eşiği (3) ve panelin yoklama aralığı (15 sn)
    sabittir; ekrandan değiştirilmez.
- **Kapsam dışı (V1):**
  - E-posta, Teams, SMS ya da başka bir panel dışı bildirim.
  - Durum geçmişi, grafik, uptime yüzdesi.
  - Uygulama başına farklı aralık veya eşik.
  - Sağlık adresine kimlik bilgisi ya da özel header göndermek.
  - Yanıt gövdesini yorumlamak (ör. `{"status":"Degraded"}`).
  - Bir uygulama için birden çok adres.
  - Hata sayıları ve exception özeti: Spec 0006.
- **Split:** Önerilmedi. Tek bir kullanıcı yeteneği var: uygulamaları izlemek ve down olunca uyarı
  almak. Dashboard 0006 olarak zaten ayrıldı.

## Acceptance criteria
- [ ] AC-1 — Oturumsuz kullanıcı "İzlenen uygulamalar" ekranını göremez ve izlenen uygulamaları
  değiştiremez. Backend bu işlemleri oturum kanıtı olmadan reddeder.
- [ ] AC-2 — Giriş yapmış kullanıcı ServiceName ve sağlık adresiyle bir uygulama ekler. Satır
  listede Bilinmiyor durumuyla, ekleyen ve zamanla görünür. Aynı ServiceName ikinci kez
  eklenemez ve alan yanında mesaj gösterilir.
- [ ] AC-3 — Şu adresler alan yanında mesajla reddedilir:
  - `http` / `https` dışı şema (ör. `ftp://`, `file://`) ve göreli adres.
  - 2048 karakterden uzun adres.
  - Kullanıcı adı ve şifre taşıyan adres.
  - `169.254.x.x` ya da `fe80::` adresine çözülen adres.

  Geçersiz ServiceName de alan yanında reddedilir. 2048 karakterlik adres kabul edilir.
- [ ] AC-4 — 2xx dönen bir adres ilk denetimden sonra Ayakta görünür. Son denetim zamanı
  güncellenir.
- [ ] AC-5 — Aşağıdakilerin her biri başarısızlık sayılır ve nedeni listede görünür; yanıt gövdesi
  gösterilmez:
  - 2xx dışı durum kodu.
  - 5 saniyede yanıt vermeyen adres.
  - Sertifikası doğrulanamayan `https` adresi.
  - Bağlantı kurulamayan adres.
  - Yönlendirme dönen adres (yönlendirilen adrese istek gitmez).
  - Denetim anında engellenen bir adrese çözülen adres (o adrese istek gitmez).
- [ ] AC-6 — Art arda 2 başarısız denetim uygulamayı Down yapmaz. 3. başarısız denetim Down yapar ve
  Down başlangıç zamanını kaydeder. Down iken tek bir başarılı denetim uygulamayı Ayakta yapar.
  2 başarısızlık, 1 başarı ve 2 başarısızlık Down yapmaz.
- [ ] AC-7 — Denetim panelde oturum açık olmadan da sürer. İki AIHub örneği çalışırken bir
  uygulama bir aralıkta bir kez denetlenir. Duran bir uygulama, durduktan sonra en
  geç iki dakika içinde panelde Down görünür. Uygulama yeniden başlatılınca durumlar korunur.
- [ ] AC-8 — En az bir uygulama Down iken panelin her ekranında uyarı görünür; uyarı Down
  uygulamaları ve Down olma zamanlarını listeler. Uygulama Ayakta olunca ya da izlemeden
  çıkarılınca, sayfa yenilenmeden 15 saniye içinde uyarıdan düşer. Down uygulama yokken ve
  giriş ekranında uyarı görünmez.
- [ ] AC-9 — Sağlık adresi değiştirilen uygulama Bilinmiyor durumuna döner ve yeni adresle
  denetlenir. Son değiştiren kullanıcı ve zaman listede görünür. İzlemeden çıkarma onay ister.
  Onaydan sonra uygulama listeden ve uyarıdan çıkar, adresine artık istek gitmez. Aynı
  ServiceName yeniden eklenince Bilinmiyor durumundan başlar.
- [ ] AC-10 — "İzlenen uygulamalar" ekranında boş, yükleniyor, hata ("yeniden dene") ve liste
  durumları görünür. Down satırları listenin en üstündedir. Durum okunamadığında uyarı son bilinen
  durumu "durum güncellenemedi" notuyla gösterir.
- [ ] AC-11 — 200 izlenen uygulama varken yeni uygulama eklenemez ve neden gösterilir.
- [ ] AC-12 — Ekleme, düzenleme ve izlemeden çıkarma yalnızca klavyeyle tamamlanır. Durum metinle
  yazılır, uyarının belirmesi ekran okuyucuya duyurulur. Bu ekranlarda WCAG 2.1 AA ihlali yoktur.

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
