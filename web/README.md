# AIHub paneli

Log paneli, kullanıcı yönetimi ve uygulama sağlık izleme (Next.js). Spec'ler:
`specs/done/0002-log-panel.md`, `specs/done/0004-yerel-kullanici-girisi.md`,
`specs/done/0005-saglik-izleme.md`, `specs/active/0006-dashboard.md`. Testler: `web/e2e/`. Doğrulama her zaman `./scripts/check`
ile yapılır (repo kökünden).

## Paneli yerelde çalıştırma

Panel tek başına çalışmaz. Üç parça birlikte ayakta olmalıdır:
1. PostgreSQL ve Redis.
2. API.
3. Panel.

Giriş, AIHub'ın kendi kullanıcı hesaplarıyla yapılır; harici bir kimlik sağlayıcısı gerekmez (ADR 0002).
Aşağıdaki adresler `web/.env.example` ve `src/AIHub.Api/Properties/launchSettings.json` ile uyumludur.

**Önkoşullar:**
- Docker (Docker Desktop).
- .NET 10 SDK (`global.json`).
- Node 24 (`web/.nvmrc`, `package.json` › `engines`).
- `openssl`: Git Bash'te zaten var.

API ve panel terminali bırakmaz. Her birini **ayrı bir terminalde, repo kökünden** başlatın.

### 1. PostgreSQL ve Redis

```bash
docker run -d --name aihub-dev-pg -p 5432:5432 \
  -e POSTGRES_USER=aihub -e POSTGRES_PASSWORD=aihub-dev -e POSTGRES_DB=aihub postgres:17-alpine
docker run -d --name aihub-dev-redis -p 6379:6379 redis:7-alpine
```

5432'de zaten bir PostgreSQL çalışıyorsa iki seçeneğiniz var:
- O sunucuda ayrı bir `aihub` veritabanı açın.
- Yeni container'ı başka bir portta başlatın (ör. `-p 5433:5432`).

İkisinde de bağlantı dizesini buna göre yazın.

### 2. API (`http://localhost:5009`)

Bağlantı dizeleri, oturum token'ının imza anahtarı ve ilk yönetici hesabı sır sayılır.
`dotnet user-secrets` ile verilir (`docs/security.md`):

```bash
cd src/AIHub.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Username=aihub;Password=aihub-dev;Database=aihub"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
dotnet user-secrets set "Auth:TokenSigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "Auth:BootstrapAdmin:UserName" "yonetici"
dotnet user-secrets set "Auth:BootstrapAdmin:DisplayName" "Yönetici"
dotnet user-secrets set "Auth:BootstrapAdmin:Password" "<en az 12 karakterlik bir şifre>"
dotnet run --launch-profile http
```

- **Açılış:**
  - Development ortamında migration'lar açılışta uygulanır.
  - Veritabanında hiç kullanıcı yoksa ilk yönetici hesabı bu ayarlardan oluşturulur. Kullanıcı varken
    bu ayarlar hiçbir şeyi değiştirmez.
  - Kullanıcı yokken ve ayar eksikken API, eksik ayarın adını söyleyerek başlamaz.
  - `Auth:TokenSigningKey` eksik ya da 32 bayttan kısaysa da API başlamaz.
- **İlk girişten sonra:**
  - Yönetici şifresini panelden ("Şifre değiştir") değiştirin.
  - `Auth:BootstrapAdmin:Password` ayarını silin:
    `dotnet user-secrets remove "Auth:BootstrapAdmin:Password"`.
- **İmza anahtarı:** Anahtar değişirse bütün açık oturumlar düşer; kullanıcılar yeniden giriş yapar.

### 3. Panel (`http://localhost:3000`)

```bash
cd web
npm ci
cp .env.example .env.local
```

`.env.local` içinde `SESSION_SECRET` değerini `openssl rand -base64 32` çıktısıyla değiştirin, sonra:

```bash
npm run dev -- -p 3000
```

Tarayıcıda `http://localhost:3000` adresini açın ve ilk yönetici hesabıyla giriş yapın. Yeni
kullanıcıları "Kullanıcılar" ekranından ekleyin.

Girişten sonra ve kök adreste **Dashboard** açılır (Spec 0006): son 24 saatin önem derecesi
sayıları, en çok hata üreten servisler, en sık exception türleri ve izlenen uygulamaların durumu.
Her sayı, aynı pencere ve filtrelerle log listesini açar. Veri ekran açılırken bir kez yüklenir;
"Yenile" yeniden yükler.

### Örnek veri (yalnızca geliştirme)

Boş bir yerel ortamda dashboard'u ve log listesini dolu görmek için, API'nin kullandığı
veritabanına örnek kayıtlar yükleyin (repo kökünden, API çalışırken ya da değilken):

```bash
dotnet run --project tools/AIHub.DemoSeed
```

- Araç API ile aynı user-secrets'ı (`ConnectionStrings:Postgres`, `ConnectionStrings:Redis`) okur;
  API anahtarı gerekmez.
- 6 `demo-` servisinden, dört derecede, bir kısmı exception'lı 480 kayıt ekler. Alınma zamanları
  son 24 saate yayılır. Kayıtlar gerçek kayıtlarla aynı doğrulama ve maskeleme kurallarından geçer.
- Her çalıştırma yeni kayıt ekler; mevcut veriyi silmez. İzlenen uygulama eklemez.
- Yalnızca `Development` ortamında yazar (`dotnet run` bunu launchSettings'ten alır). Başka bir
  ortamda ya da ortam belirtilmeden çalıştırılırsa hiçbir kayıt yazmadan çıkış kodu 2 ile durur.

Uygulamaların sağlığını "İzlenen uygulamalar" ekranından takip edin (Spec 0005). Her uygulama için
bir ServiceName ve bir sağlık adresi (`http://…/health`) girin. API, adresi 30 saniyede bir dener.
Art arda 3 başarısız denemede uygulama Down olur ve panelin her ekranında uyarı görünür. Ek bir ayar
gerekmez; API'nin izlenen adreslere ağ erişimi olmalıdır.

### Giriş hatalarını teşhis etme

Giriş başarısız olursa kullanıcı yalnızca genel bir mesaj görür:
- **"Kullanıcı adı veya şifre hatalı."**
  - Neden bilerek söylenmez: yanlış şifre, var olmayan kullanıcı, pasif ve kilitli hesap aynı
    mesajı alır.
  - Art arda 5 yanlış denemeden sonra hesap 15 dakika kilitlenir.
- **"Giriş şu anda yapılamıyor."** Panel backend'e ulaşamadı. Nedeni, paneli çalıştırdığınız
  terminalde `Giriş hatası (giris)` ile başlayan satırdadır. Bu satır kullanıcı adı, şifre ya da
  token içermez.

| Terminaldeki satır | Neden | Çözüm |
|---|---|---|
| `API_BASE_URL tanımlı değil` | `.env.local` yok veya eksik | 3. adım |
| `fetch failed` | API çalışmıyor ya da `API_BASE_URL` yanlış | 2. adım; API 5009'da mı? |
| `SESSION_SECRET en az 32 karakter olmalıdır` | `.env.local`'da `SESSION_SECRET` yok ya da kısa | 3. adım |

### Temizlik

```bash
docker rm -f aihub-dev-pg aihub-dev-redis
```
