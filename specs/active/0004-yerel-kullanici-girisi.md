# Spec 0004 — Yerel kullanıcı girişi ve kullanıcı yönetimi

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0004-plan.md`

## Intent
Panel bugün harici bir OIDC sağlayıcısına bağlı. Kurumda bağlı bir sağlayıcı olmadığı için panel
gerçekte kullanılamıyor. Ürün sahibi girişin AIHub'ın kendi kullanıcı hesaplarıyla yapılmasını istedi
(harun.ozdek, 2026-11-02; ADR 0002). Başarı şöyle tanımlanır: ilk yönetici hesabı kurulumla birlikte
hazırdır; giriş yapan kullanıcı panelden yeni kullanıcı ekler, kullanıcıları pasifleştirir ve kendi
şifresini değiştirir. Şifreler güvenle saklanır, tahmin saldırıları kilitlemeyle durdurulur. Rol, SSO
ve MFA bilerek kapsam dışıdır. Bu spec, sonraki health check (0005) ve dashboard (0006) işlerinin
giriş temelidir.

## Requirements
- **R-1 Giriş:** Kullanıcı, kullanıcı adı ve şifresiyle giriş yapar. Kullanıcı adı büyük-küçük harf
  duyarsızdır.
- **R-2 Belirsiz hata:** Yanlış kullanıcı adı, yanlış şifre, pasif hesap ve kilitli hesap aynı genel
  mesajı alır. Mesaj, kullanıcı adının var olup olmadığını belli etmez.
- **R-3 Kilitleme:** Aynı hesaba art arda 5 başarısız denemeden sonra hesap 15 dakika kilitlenir.
  Kilit süresince doğru şifre de kabul edilmez. Başarılı giriş sayacı sıfırlar.
- **R-4 Şifre saklama:** Şifre hiçbir yerde (veritabanı, log, yanıt) açık metin olarak durmaz;
  yalnızca yavaş ve tuzlu bir hash'i saklanır.
- **R-5 Şifre kuralı:** Şifre en az 12, en fazla 128 karakterdir. Kuralı karşılamayan şifre, nedeni
  söylenerek reddedilir.
- **R-6 İlk yönetici:** Hiç kullanıcı yokken uygulama açılışta yapılandırmadan (sır deposundan)
  verilen ilk hesabı oluşturur. En az bir kullanıcı varsa bu adım hiçbir şey yapmaz; mevcut hesabı ve
  şifresini değiştirmez.
- **R-7 Kullanıcı ekleme:** Giriş yapmış her kullanıcı yeni kullanıcı ekler: kullanıcı adı, görünen ad
  ve başlangıç şifresi. Kullanıcı adı 3–64 karakterdir; harf, rakam, `.`, `_`, `-` içerir ve
  benzersizdir (büyük-küçük harf duyarsız).
- **R-8 Pasifleştirme:** Giriş yapmış kullanıcı başka bir kullanıcıyı pasifleştirir veya yeniden
  etkinleştirir; kendini ve son aktif kullanıcıyı pasifleştiremez. Pasif kullanıcının açık oturumu bir
  sonraki işleminde biter.
- **R-9 Şifre değiştirme:** Kullanıcı mevcut şifresini doğrulayarak kendi şifresini değiştirir.
  Şifre değişince kullanıcının diğer açık oturumları bir sonraki işlemlerinde biter. Başkasının
  şifresini sıfırlamak, o kullanıcıya yeni bir başlangıç şifresi vermekle olur.
- **R-10 Oturum:** Oturum, girişten itibaren mutlak 8 saat sonra biter (hareketsizlik süresi değil). Süresi dolan oturumda kullanıcı giriş ekranına gider;
  girişten sonra istediği sayfaya aynı filtrelerle döner (0002 AC-2 davranışı korunur). Çıkış
  oturumu hemen bitirir.
- **R-11 Kayıt:** Kullanıcının kim tarafından ve ne zaman eklendiği ya da pasifleştirildiği, ayrıca
  son başarılı giriş zamanı saklanır ve kullanıcı listesinde görünür.

## User experience
- **Akış:**
  - Panelin herhangi bir sayfası açılır. Oturum yoksa giriş ekranı gelir: kullanıcı adı, şifre,
    "Giriş yap".
  - Giriş başarılıysa kullanıcı istediği sayfaya döner.
  - Üst menüde "Kullanıcılar" ekranı ve kullanıcının adının altında "Şifre değiştir" bulunur.
- **Giriş ekranının durumları:** boş form, gönderiliyor, genel hata ("Kullanıcı adı veya şifre
  hatalı"), oturum süresi doldu bilgisi, çıkış yapıldı bilgisi.
- **Kullanıcılar ekranının durumları:**
  - Liste: kullanıcı adı, görünen ad, durum (aktif/pasif), son giriş, ekleyen ve tarih.
  - Yükleniyor, hata ("yeniden dene" ile).
  - "Kullanıcı ekle" formunda alan yanında doğrulama hataları; kullanıcı adı alınmışsa mesaj.
  - Pasifleştir/etkinleştir için onay; kendi satırında pasifleştirme yok.
- **Şifre değiştir ekranının durumları:** form, mevcut şifre yanlış, yeni şifre kurala uymuyor,
  başarılı.
- **Tasarım referansı:** none — yukarıdaki akış tasarımdır; görünüm 0002 panelinin bileşenlerini
  izler.
- **Erişilebilirlik:**
  - Tüm formlar yalnızca klavyeyle kullanılabilir, her alanın etiketi vardır.
  - Hata mesajları alana bağlıdır (`aria-describedby`).
  - WCAG 2.1 AA (0002 AC-10 ile aynı ölçüt).

## Constraints & out of scope
- **Supersedes:** 0002/AC-2'nin kimlik sağlayıcısı dalları (sağlayıcıda iptal ve sağlayıcı hatası) ve
  spec 0003'ün tamamı. OIDC giriş yolu kaldırılır. Shipped spec'ler düzenlenmez.
- **Güvenlik:** ADR 0002 ve `docs/security.md` güncellenir. Ayrıca:
  - Giriş isteği kullanıcı adı başına R-3'le kilitlenir.
  - Şifre ve hash hiçbir log satırına girmez.
  - İlk yönetici şifresi ilk girişten sonra değiştirilmeli ve sır deposundan silinmelidir;
    `web/README.md`'de yazılır.
- **Kabul edilen riskler (V1, kurum içi ağ; harun.ozdek, 2026-11-02):**
  - Kullanıcı adını bilen biri hesabı tekrar tekrar 15 dakika kilitleyebilir.
  - Birçok kullanıcı adına dağıtılmış deneme (credential stuffing) için ayrı bir sınır yoktur.
- **Testler:** 0002 ve 0003'ün OIDC'ye özgü e2e testleri, yerini alan davranışın testleriyle
  eşleştirilerek değiştirilir; eşleme planda gösterilir (kural 5). Açık yönlendirme testleri
  korunur.
- **Erişim (V1):** Rol yok. Giriş yapan her kullanıcı kullanıcı ekleyebilir ve pasifleştirebilir
  (0002'deki "herkes her şeyi görür" kararıyla uyumlu). Kim yaptı kaydı tutulur (R-11).
- **Kapsam dışı:**
  - Rol veya yetki seviyeleri.
  - SSO ve MFA.
  - "Şifremi unuttum" e-postası.
  - Kullanıcı silme (yalnızca pasifleştirme var).
  - İlk girişte şifre değiştirmeye zorlama.
  - Şifre geçmişi ve süreli şifre.
  - Log gönderen servislerin API anahtarıyla kimlik doğrulaması (0001) değişmez.
- **Split:** Önerilen: 0004 giriş, ilk yönetici, çıkış ve şifre değiştirme; 0004b kullanıcı ekleme ve
  pasifleştirme. Tek spec tutuldu (harun.ozdek, 2026-11-02, "önerilerinle devam"). Gerekçe: kullanıcı
  yönetimi olmadan panelde yalnızca tek bir hesap olur ve 0005 ile 0006 için ekip kullanımı mümkün
  olmaz.

## Acceptance criteria
- [ ] AC-1 — Oturumsuz kullanıcı panelin herhangi bir sayfasını açınca giriş ekranına gider ve log
  içeriği görmez. Backend, panel uçlarını oturum kanıtı olmadan reddeder.
- [ ] AC-2 — Doğru kullanıcı adı ve şifreyle (kullanıcı adı farklı büyük-küçük harfle de olur) giriş
  yapan kullanıcı, girişten önce istediği sayfaya aynı filtrelerle döner. Dış adrese yönlendirme
  yapılmaz.
- [ ] AC-3 — Yanlış şifre, var olmayan kullanıcı adı, pasif hesap ve kilitli hesap aynı mesajı
  alır. Yanıt durumu aynıdır ve gövde dışında ayırt edici bir alan yoktur.
- [ ] AC-4 — Art arda 5 yanlış denemeden sonra doğru şifre de 15 dakika reddedilir. 15 dakika
  dolunca giriş yapılabilir. Başarılı giriş sayacı sıfırlar: 4 yanlış, 1 doğru, 4 yanlış hesabı
  kilitlemez.
- [ ] AC-5 — Veritabanında şifre açık metin olarak bulunmaz. Aynı şifreye sahip iki kullanıcının
  saklanan değerleri farklıdır. Giriş denemesinin ürettiği hiçbir log satırı şifreyi içermez.
- [ ] AC-6 — Hiç kullanıcı yokken açılışta yapılandırmadaki ilk hesap oluşur ve onunla giriş
  yapılır. Kullanıcı varken açılış hiçbir hesabı değiştirmez. Yapılandırma eksikken ve kullanıcı
  yokken uygulama hangi ayarın eksik olduğunu söyleyerek başlamaz.
- [ ] AC-7 — Giriş yapmış kullanıcı yeni kullanıcı ekler; yeni kullanıcı başlangıç şifresiyle giriş
  yapar. Alınmış kullanıcı adı (büyük-küçük harf farkıyla da), 3'ten kısa ya da 64'ten uzun ad,
  izin dışı karakter ve 12'den kısa ya da 128'den uzun şifre, alan yanında mesajla reddedilir.
- [ ] AC-8 — Pasifleştirilen kullanıcı giriş yapamaz (AC-3 mesajı). Açık oturumu bir sonraki
  işleminde biter. Yeniden etkinleştirilince giriş yapabilir. Kullanıcı kendini ve son aktif
  kullanıcıyı pasifleştiremez.
- [ ] AC-9 — Kullanıcı mevcut şifresini doğrulayarak şifresini değiştirir; eski şifre artık kabul
  edilmez ve kullanıcının diğer açık oturumları sonraki işlemde biter. Mevcut şifre yanlışsa ya da yeni şifre kurala uymuyorsa değişiklik olmaz ve nedeni
  gösterilir.
- [ ] AC-10 — Oturum girişten mutlak 8 saat sonra biter, sonraki işlemde giriş ekranı gelir. Çıkıştan sonra geri tuşu
  log içeriği göstermez.
- [ ] AC-11 — Kullanıcılar ekranı tüm kullanıcıları ekleyen, eklenme zamanı, durum ve son giriş
  zamanıyla listeler. Yükleniyor ve hata ("yeniden dene") durumları görünür.
- [ ] AC-12 — Giriş, kullanıcı ekleme ve şifre değiştirme akışları yalnızca klavyeyle tamamlanır.
  Bu ekranlarda WCAG 2.1 AA ihlali yoktur.

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
