# AIHub paneli

Salt okunur log paneli (Next.js). Spec: `specs/done/0002-log-panel.md`. Testler: `web/e2e/`.
Doğrulama her zaman `./scripts/check` ile yapılır (repo kökünden).

## Paneli yerelde çalıştırma

Panel tek başına çalışmaz. Girişin tamamlanması için dört parça birlikte ayakta olmalıdır:
1. PostgreSQL ve Redis.
2. Bir OIDC sağlayıcısı (yerelde mock).
3. API.
4. Panel.

Aşağıdaki adresler `web/.env.example` ve `src/AIHub.Api/appsettings.Development.json` ile
uyumludur.

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

5432'de zaten bir PostgreSQL çalışıyorsa başka bir port seçin (ör. `-p 5433:5432`) ve bağlantı
dizesini ona göre yazın.

### 2. Mock OIDC sağlayıcısı (8080, issuer `aihub`)

Token'lar hem panelin client id'sini (`aihub-panel`) hem API'nin audience'ını (`aihub-api`) taşımalıdır.
Aksi halde API panelin isteklerini 401 ile reddeder. Config, e2e'dekiyle aynıdır
(`web/e2e/global-setup.ts`).

```bash
docker run -d --name aihub-dev-oidc -p 8080:8080 -e JSON_CONFIG='{"interactiveLogin":true,"tokenCallbacks":[{"issuerId":"aihub","tokenExpiry":3600,"requestMappings":[{"requestParam":"grant_type","match":"*","claims":{"aud":["aihub-panel","aihub-api"],"azp":"aihub-panel","name":"Geliştirici"}}]}]}' \
  ghcr.io/navikt/mock-oauth2-server:6.1.2
```

Kontrol: `http://localhost:8080/aihub/.well-known/openid-configuration` bir JSON döndürmelidir.
Mock'un giriş formunda herhangi bir kullanıcı adı kabul edilir.

### 3. API (`http://localhost:5009`)

Bağlantı dizeleri sır sayılır ve `dotnet user-secrets` ile verilir (`docs/security.md`):

```bash
cd src/AIHub.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Username=aihub;Password=aihub-dev;Database=aihub"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
dotnet run --launch-profile http
```

Development ortamında migration'lar açılışta uygulanır. Panel token'larının sağlayıcısı da
`http://localhost:8080/aihub` olarak ayarlıdır.

### 4. Panel (`http://localhost:3000`)

```bash
cd web
npm ci
cp .env.example .env.local
```

`.env.local` içinde iki değeri değiştirin:
- `SESSION_SECRET`: `openssl rand -base64 32` çıktısı.
- `OIDC_CLIENT_SECRET`: mock her değeri kabul eder, ör. `dev-secret`.

```bash
npm run dev -- -p 3000
```

Tarayıcıda **`http://localhost:3000`** adresini açın.

### Adres kuralı

`APP_URL`, tarayıcıda açtığınız adresle birebir aynı olmalıdır: aynı host, aynı port. Kimlik
sağlayıcısı geri dönüşü `APP_URL`'e yapar ve giriş akışı çerezi yalnızca açtığınız adreste vardır.
- `127.0.0.1:3000` veya `next dev`'in yazdığı ağ adresi ile açarsanız giriş tamamlanmaz.
- 3000 doluysa `next dev` sessizce 3001'e geçer. Bu yüzden portu `-p 3000` ile verin; port doluysa
  önce o süreci kapatın.

`OIDC_ALLOW_HTTP=true` yalnızca `npm run build && npm start` (production derlemesi) için gerekir.
`next dev` düz HTTP mock'a zaten izin verir.

### Giriş hatalarını teşhis etme

Giriş başarısız olursa kullanıcı yalnızca "Giriş yapılamadı" sayfasını görür. Nedeni, paneli
çalıştırdığınız terminalde `Giriş hatası (giris)` veya `Giriş hatası (geri-donus)` ile başlayan
satırdadır.

Panelin kendi hata satırı token, yetkilendirme kodu ya da `state` içermez. Ancak `next dev` her
isteği adresiyle birlikte terminale yazar ve `GET /auth/geri-donus?code=…&state=…` satırında bu
değerler görünür. Kod kısa ömürlü ve tek kullanımlıktır; yine de bu terminal çıktısını paylaşırken o
satırları çıkarın.

| Terminaldeki satır | Neden | Çözüm |
|---|---|---|
| `APP_URL tanımlı değil` / `OIDC_ISSUER tanımlı değil` | `.env.local` yok veya eksik | 4. adım |
| `fetch failed` veya bağlantı hatası (`giris`) | 8080'de sağlayıcı yok | 2. adım |
| `giriş akışı çerezi yok` | Panel `APP_URL`'den farklı bir adresten açıldı | Adres kuralı |
| `OAUTH_HTTP_REQUEST_FORBIDDEN` | Production derlemesinde düz HTTP sağlayıcı | `OIDC_ALLOW_HTTP=true` |

Giriş tamamlanıyor ama liste açılmıyorsa sorun `API_BASE_URL` ya da API'dedir. Önce API'nin 5009'da
çalıştığını kontrol edin.

### Temizlik

```bash
docker rm -f aihub-dev-pg aihub-dev-redis aihub-dev-oidc
```
