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
- **Panel (`web/`, insanlar):** AIHub'ın kendi kullanıcı hesapları, kullanıcı adı ve şifre (ADR 0002,
  Spec 0004).
  - **Şifre saklama:** Şifre yalnızca PBKDF2-HMAC-SHA256 (600.000 tur, 16 bayt tuz) hash'i olarak
    saklanır. Karşılaştırma sabit zamanlıdır. Şifre ve hash hiçbir log satırına, yanıta ya da hata
    mesajına girmez.
  - **Giriş hataları:** Yanlış şifre, var olmayan kullanıcı, pasif ve kilitli hesap aynı 401'i ve
    aynı gövdeyi alır. Kullanıcı yoksa da bir hash doğrulaması yapılır; yanıt süresi kullanıcı adının
    varlığını sızdırmaz.
  - **Kilitleme:** Art arda 5 başarısız denemede hesap 15 dakika kilitlenir. Kabul edilen riskler
    (V1, kurum içi ağ): kullanıcı adını bilen biri hesabı tekrar tekrar kilitleyebilir; dağıtılmış
    deneme için ayrı bir sınır yoktur.
  - **Oturum token'ı:**
    - Backend'in `Auth:TokenSigningKey` (sır, en az 32 bayt) ile HS256 imzaladığı bir JWT. Girişten
      itibaren mutlak 8 saat geçerlidir; saat kayması payı ve yenileme yoktur. Şifre değişikliğinden
      dönen yeni token da girişteki bitişi taşır.
    - Token yalnızca Next.js sunucusunda durur: şifreli, `httpOnly` oturum çerezi (`iron-session`,
      anahtarı `SESSION_SECRET`). Tarayıcı JavaScript'i token'a erişemez. Anahtar değişince bütün
      oturumlar düşer.
  - **Panel yüzeyi:**
    - Backend'in panel yüzeyi (`/api/panel/*`, `/api/users`, `/api/me/*`) yalnızca bu token'ı kabul
      eder (`Panel` şeması; issuer, audience, imza ve süre doğrulanır).
    - Her istekte kullanıcının aktif olduğu ve token'daki güvenlik damgasının güncel olduğu denetlenir.
      Pasifleştirme ve şifre değişikliği damgayı yenilediği için eski oturumlar sonraki istekte düşer.
      Depo erişilemezse istek 401 değil 503 alır.
    - Backend oturumu reddederse panel çerezi siler (`/auth/oturum-dustu`) ve giriş ekranına döner.
      Bu rota GET'tir: sayfa render'ı ve server action'lar oraya yalnızca yönlendirmeyle ulaşır. Kabul
      edilen risk (V1, kurum içi ağ): başka bir sitedeki link kullanıcının oturumunu kapatabilir
      (logout CSRF; çerez `SameSite=Lax`). Veri sızmaz, yetki kazanılmaz. `/auth/` rotaları girişten
      sonraki dönüş adresi olamaz.
    - Servis API anahtarı panel yüzeyinde, panel token'ı da ingestion yüzeyinde 401 alır. Her yüzey
      yalnızca kendi şemasına bağlı bir policy ile korunur; fallback policy default-deny kalır.
  - **İlk yönetici:** Hiç kullanıcı yokken açılışta `Auth:BootstrapAdmin:*` ayarlarından (sır deposu)
    oluşturulur. Şifresi ilk girişten sonra değiştirilmeli ve ayar silinmelidir.
  - **Rol yoktur (V1):** Giriş yapan her kullanıcı kullanıcı ekler ve pasifleştirir; kimin yaptığı
    kaydedilir. Kendini ve son aktif kullanıcıyı pasifleştirmek reddedilir.
  - Log içeriği taşıyan panel yanıtları `Cache-Control: no-store` ile döner: çıkıştan sonra geri
    tuşu içeriği önbellekten göstermez.
- **ServiceName istemciye bırakılmaz:** kaydın ServiceName'i çağrıyı yapan API anahtarına bağlı
  değerden belirlenir; istek gövdesindeki ad ile çelişirse anahtara bağlı değer kazanır. Aksi halde
  her servis, bir başkasının adına log yazabilir.
- **Varsayılan ret (default-deny):** kimliği doğrulanmamış istek hiçbir uç noktaya erişmez.
  - Tek istisna sağlık adresleridir (Spec 0008): backend'de ve panelde yalnızca tam `/health`
    girişsizdir. Yanıt yalnızca `Healthy`/`Unhealthy` metnidir; bağlantı dizesi, sunucu adı, sürüm,
    hata mesajı ya da yığın izi yazılmaz ve önbelleğe alınmaz. Hata ayrıntısı yalnızca süreç loguna gider.
- Rate limiting Redis üzerinden, API anahtarı başına uygulanır: tek bir servisin log patlaması
  platformun tamamını düşürmemeli.

## Sağlık denetimi: giden istekler (Spec 0005, ADR 0003)
Backend, panel kullanıcılarının kaydettiği sağlık adreslerine istek gönderir. Bu bir sunucu tarafı
istek sahteciliği (SSRF) yüzeyidir. Sınırlar:
- **Adres kuralı:** yalnızca mutlak `http`/`https`, en fazla 2048 karakter, kullanıcı bilgisi
  (`kullanici:sifre@`) yok.
- **Engelli adresler:** `169.254.0.0/16` (bulut metadata servisi dahil) ve `fe80::/10`; IPv4-mapped
  IPv6 biçimleri de engellidir.
  - Kayıtta adres çözülür; engelli bir IP'ye çözülüyorsa kaydedilmez.
  - Denetim anında ad yeniden çözülür. Engelli IP'ye bağlanılmaz; bağlantı doğrulanan IP'ye
    kurulur, böylece DNS sonradan değişse de (rebinding) engel aşılamaz.
- **İstek:** yönlendirme izlenmez, proxy ve çerez kullanılmaz, yanıt gövdesi okunmaz, 5 sn zaman
  aşımı. Kimlik bilgisi ya da özel header gönderilmez.
- **Kullanıcıya gösterilen:** yalnızca neden (zaman aşımı, bağlantı kurulamadı, sertifika hatası,
  HTTP durum kodu, yönlendirme, engellenen adres). Yanıt gövdesi ve iç hata ayrıntısı gösterilmez.
- **Kabul edilen riskler (V1, kurum içi ağ, güvenilir kullanıcılar):**
  - Kurum içi adresler ve `localhost` serbesttir. Giriş yapmış biri AIHub üzerinden iç ağdaki
    adresleri yoklayabilir; başarısızlık nedeni bir adresin açık olup olmadığını belli eder.
  - İki kullanıcı aynı uygulamayı aynı anda düzenlerse son kaydeden kazanır.
  - Backend'in `/health` adresi girişsizdir ve hız sınırı yoktur (Spec 0008). Her istek PostgreSQL'e
    yeni bir bağlantı açar ve Redis'e PING gönderir. Ana bağlantı havuzunu kullanmaz; PostgreSQL yanıt
    vermezken bile bağlantı en geç 2 sn'de bırakılır (review 1, F-5/F-6). Sık çağıran kimliksiz bir
    istemci yine de veritabanına bağlantı yükü bindirebilir.
  - AIHub internete açık bir yerde çalıştırılırsa iç adreslere izin yeniden değerlendirilir
    (ADR 0003, Revisit triggers).

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
