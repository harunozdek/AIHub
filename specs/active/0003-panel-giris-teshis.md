# Spec 0003 — Panel girişinin yerelde kurulması ve teşhis edilebilmesi (mini)

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0003-plan.md`
- Source: kullanıcı talebi, sohbet 2026-11-02 — "web projesini ayağa kaldırdığımızda login de hata veriyor"
- Design: —
- Supersedes: —

## Intent
Paneli kendi makinesinde çalıştıran geliştirici giriş yapamıyor ve nedenini göremiyor. Bunun iki
nedeni var: yerel kurulum (OIDC sağlayıcısı, `.env.local`, adres kuralı) hiçbir yerde yazılı değil
ve giriş route'ları hatanın nedenini yutuyor. Bu değişiklik kurulumu yazılı hale getirir ve giriş
hatalarını sunucu tarafında teşhis edilebilir yapar. Kullanıcıya görünen davranış değişmez.

## Changed behavior
- [ ] CB-1 — Giriş başlatılırken (`/auth/giris`) ya da kimlik sağlayıcısından dönüşte
  (`/auth/geri-donus`) bir hata olursa, nedeni (hata türü ve mesajı) sunucu sürecinin loguna
  yazılır. Log satırı token, yetkilendirme kodu, `state` ya da çerez değeri içermez.
- [ ] CB-2 — Panel konfigürasyonu eksikken (ör. `APP_URL` veya `OIDC_ISSUER` tanımsız) giriş 500
  vermez. Kullanıcı "Giriş yapılamadı" sayfasına gider ve sunucu logunda eksik değişkenin adı
  görünür.
- [ ] CB-3 — Repoda paneli yerelde çalıştırma talimatı bulunur. Talimat şunları kapsar: PostgreSQL ve
  Redis, mock OIDC sağlayıcısı (8080, `aihub` issuer'ı, `aud: aihub-panel + aihub-api`), API,
  `web/.env.local`, ve `APP_URL`'in tarayıcıda açılan origin ile (host ve port) birebir aynı olması
  kuralı. Talimat temiz bir makinede baştan sona izlenince giriş tamamlanır ve log listesi açılır.
- [ ] CB-4 — `web/.env.example`'daki `API_BASE_URL`, API'nin yerel geliştirme adresiyle
  (`launchSettings.json`, `http://localhost:5009`) aynıdır. `APP_URL` satırı adres kuralını
  açıklar.

## Preserved behavior
- [ ] PB-1 — 0002/AC-2 korunur: iptal edilen giriş "iptal", başarısız giriş "hata" sayfasını gösterir;
  hiçbirinde log içeriği görünmez. `web/e2e/giris.spec.ts` testleri değişmeden geçer.
- [ ] PB-2 — Hata sayfası kullanıcıya iç ayrıntı (hata mesajı, değişken adı, yığın) göstermez
  (0002/R-9).
- [ ] PB-3 — Başarılı giriş, girişten önce istenen sayfaya aynı filtrelerle döner; dış adrese
  yönlendirme koruması (0002 açık yönlendirme testleri) aynen geçer.

## Out of scope
- `middleware.ts` → `proxy.ts` yeniden adlandırması (0002 F-09).
- Konfigürasyonun uygulama açılışında doğrulanması (`instrumentation.ts`) ve `npm run dev` portunun
  sabitlenmesi.
- Gerçek kimlik sağlayıcısı seçimi, çerez boyutu ve token rotasyonu (0002 F-04, F-08).
- AC9 flaky testi (R-03, ayrı iş).

## Definition of Done
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots)
- [ ] Spec moved to `specs/done/` (immutable there)
