# ADR 0002 — Panel kullanıcıları AIHub'ın kendi hesaplarıyla giriş yapar

- Status: Accepted
- Date: 2026-11-02

## Context
Panel bugün harici bir OIDC kimlik sağlayıcısıyla giriş yapıyor (`docs/security.md`, spec 0002 ve
0003). Kurumda panele bağlanmış gerçek bir sağlayıcı yok; yerelde ve testte mock sağlayıcı
kullanılıyor. Ürün sahibi, girişin AIHub'ın içinde, kendi kullanıcı hesaplarıyla yapılmasını istedi
(harun.ozdek, 2026-11-02). Önerilen alternatif ise kurum sağlayıcısını bağlamaktı.

## Decision
Panel kullanıcıları AIHub'ın sakladığı yerel hesaplarla (kullanıcı adı ve şifre) giriş yapar. Şifre
doğrulaması backend'de yapılır. Panel, OIDC akışının yerine backend'in giriş yüzeyini kullanır;
OIDC giriş yolu kaldırılır.

## Consequences
- **Kazanç:**
  - Harici bir sağlayıcıya bağımlılık yok.
  - Yerel kurulum ve testler basitleşir: mock OIDC container'ı gerekmez.
  - Kullanıcılar panelden yönetilir.
- **Bedel, AIHub artık bir şifre deposu:**
  - Şifreler yalnızca yavaş ve tuzlu bir hash olarak saklanır.
  - Kaba kuvvet denemelerine karşı hesap kilitleme gerekir.
  - Hata mesajları kullanıcı adının var olup olmadığını sızdırmamalıdır.
  - Şifre sıfırlama süreci gerekir.
  - Bunların her biri review'da ayrıca denetlenir.
- **Kayıplar:**
  - Kurumsal tek oturum açma (SSO) yok.
  - Çok faktörlü doğrulama (MFA) V1'de yok.
  - İşten ayrılan kişinin hesabı kurum dizininden otomatik kapanmaz; elle pasifleştirilir.
- **Etkilenen kararlar:**
  - `docs/security.md`'deki panel OIDC bölümü bu ADR ile güncellenir.
  - Spec 0002'nin giriş akışı (AC-2'nin sağlayıcı iptal/hata dalları) ve spec 0003 (OIDC teşhisi) yeni spec'le yerini değiştirir. Shipped spec'ler düzenlenmez.

## Alternatives considered
- **Kurum OIDC sağlayıcısını bağlamak (Entra ID, AD FS):** SSO, MFA ve merkezi hesap kapatma
  bedavaya gelirdi; mevcut kod hazır. Ürün sahibinin kararıyla seçilmedi.
- **Keycloak'ı AIHub'la birlikte işletmek:** OIDC korunur ama ayrı bir kimlik sunucusunu kurmak,
  yedeklemek ve güncellemek gerekir. Tek bir panel için operasyon yükü orantısız.
- **OIDC ve yerel hesap birlikte:** İki giriş yolu, iki kat güvenlik yüzeyi ve test demek. Bugün
  OIDC'yi kullanan kimse yok.

## Revisit triggers
- Kurumda panele bağlanabilecek bir kimlik sağlayıcı devreye girerse.
- MFA ya da denetim (audit) için bir uyum şartı gelirse.
- Kullanıcı sayısı elle yönetimin zorlaşacağı düzeye çıkarsa (ör. 50+).
