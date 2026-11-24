# Spec 0010 — Next.js güvenlik yükseltmesi (mini)

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0010-plan.md`
- Source: Kullanıcı isteği 2026-11-24 ("Next.js yükseltmesini /change ile başlat"); tetikleyen: Spec 0007 Review 3 bulgusu B-1 ve plan 0009 risk 8 — full check `web-build` adımında `SecurityFatalError` ile kırmızı. Uyarılar: GHSA-mfr8-8qww-v76h, GHSA-vc7v-qv4w-wrv8, GHSA-7qx7-33qx-rqx2, GHSA-f3mh-rxg3-f5g2, GHSA-pc58-hwjq-p385, GHSA-rhh4-g77r-r9qj, GHSA-vvgp-ww4w-46vf.
- Design: yok — kullanıcı arayüzü değişmiyor.
- Supersedes: yok — gönderilmiş bir kabul kriteri değişmiyor.

## Intent
Panelin üzerine kurulu olduğu çatının kullanılan sürümünde bilinen güvenlik açıkları var; derleme bu
yüzden reddediliyor ve hiçbir panel değişikliği (0007, 0009) ship edilemiyor. Çatı, açıkların
giderildiği sürüme yükseltilir; panelin kullanıcıya görünen davranışı aynen kalır.

## Changed behavior
- [ ] CB-1 — Panelin üretim derlemesi güvenlik reddi almadan tamamlanır (full check'in `web-build` adımı yeşil).
- [ ] CB-2 — Panelin bağımlılık denetiminde (yalnız çalışma zamanı bağımlılıkları) çatıya ait bilinen güvenlik uyarısı kalmaz.
- [ ] CB-3 — Çatı ve onun lint kural paketi aynı sürümdedir; sürüm aralığı tam sabittir (bugünkü gibi `^`/`~` yok).

## Preserved behavior
- [ ] PB-1 — Panelin mevcut uçtan uca testlerinin tamamı (giriş, oturum düşmesi, log listesi ve ayrıntı, dashboard, sağlık izleme, kullanıcılar, şifre, erişilebilirlik, güvenlik başlıkları) değiştirilmeden ve atlanmadan geçer.
- [ ] PB-2 — Panelin lint ve tip denetimi, kural ya da ayar gevşetilmeden geçer.
- [ ] PB-3 — Backend tarafı (format, build, birim ve entegrasyon testleri) etkilenmez ve yeşil kalır.
- [ ] PB-4 — `web/package.json`'da doğrudan listelenen başka hiçbir bağımlılığın sürümü değişmez; değişiklik yalnızca çatı ve lint kural paketinin yama sürümüyle (16.4.x içi) sınırlıdır. Kilit dosyasındaki dolaylı değişiklikler (çatının kendi alt paketleri) serbesttir ve review'da gözden geçirilir (G-2).

## Out of scope
- Başka bağımlılık yükseltmeleri (React, ESLint, Playwright vb.) ve `npm audit`'in geliştirme bağımlılıkları için raporlayabileceği uyarılar.
- 0007 ve 0009 dallarının `main`'le birleştirilmesi: bu spec ship edildikten sonra her dal kendi segmentinde yapar.
- Panel kodunda herhangi bir davranış ya da görünüm değişikliği.

## Clarify kararları (2026-11-24, onaylayan: harun.ozdek)
- G-1 — Hedef sürüm numarası spec'te değil planda yazılır; spec yalnızca "16.4.x içi yama" sınırını koyar.
- G-2 — PB-4 yalnızca doğrudan bağımlılıkları kapsar; kilit dosyasındaki dolaylı değişiklikler review'da incelenir.
- G-3 — Kanıt: PB-1 (e2e geçişi) ve ek olarak giriş ekranı ile dashboard için önce/sonra ekran görüntüsü.

## Definition of Done
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Criterion ↔ evidence table complete for CB-* **and** PB-* (UI: before/after screenshots)
- [ ] Spec moved to `specs/done/` (immutable there)
