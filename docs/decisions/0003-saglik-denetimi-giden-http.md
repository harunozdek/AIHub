# ADR 0003 — Sağlık denetimi: backend kullanıcının verdiği adreslere istek gönderir

- Status: Accepted
- Date: 2026-11-02

## Context
Spec 0005 ile panel kullanıcıları, izlenen her uygulama için bir sağlık adresi kaydedecek. AIHub
bu adresi arka planda 30 saniyede bir deneyecek. Bugüne kadar backend yalnızca gelen istekleri
karşılıyordu. Bu iş ilk kez dışarıya, üstelik kullanıcının yazdığı bir adrese istek gönderiyor.
Bu, sunucu tarafı istek sahteciliği (SSRF) yüzeyi açar: adres bulut metadata servisine
(`169.254.169.254`) ya da yönlendirmeyle başka bir hedefe götürebilir. İzlenen uygulamalar kurum
içinde olduğu için iç adresleri engellemek de mümkün değil. AIHub birden çok örnekle
çalıştırılırsa aynı uygulama her aralıkta birkaç kez denetlenebilir ve "art arda 3 başarısızlık"
kuralı bozulur.

## Decision
Denetimi backend'deki bir arka plan görevi yapar. Dışa giden istekler şu sınırlarla gider:
- yalnızca `http`/`https`;
- yönlendirme ve proxy kapalı, yanıt gövdesi okunmaz;
- adres bağlantı anında çözülür. `169.254.0.0/16` ve `fe80::/10` (IPv4-mapped biçimleri dahil)
  aralığındaki IP'lere bağlanılmaz ve bağlantı doğrulanan IP'ye kurulur.

Kurum içi adresler ve `localhost` serbesttir. "Bir aralıkta bir kez" kuralı veritabanında sağlanır:
her satırın sonraki denetim zamanı vardır. Zamanı gelen satırlar `FOR UPDATE SKIP LOCKED` ile
sahiplenilir ve aynı sorguda sonraki denetim zamanı ileri alınır.

## Consequences
- **Kazanç:**
  - Panelde kimse yokken de denetim sürer.
  - Metadata servisi ve link-local adresler, DNS sonradan değişse de hedef olamaz.
  - Birden çok örnek ek bir kilit servisi olmadan aynı uygulamayı aralık başına bir kez denetler.
- **Bedel:**
  - Giriş yapmış bir kullanıcı AIHub üzerinden iç ağdaki adresleri yoklayabilir; başarısızlık
    nedeni bir adresin açık olup olmadığını belli eder. Kullanıcılar güvenilir ve kurum içi
    olduğu için bu kabul edilen risktir (spec 0005, G2; `docs/security.md`).
  - Backend'in iç ağa çıkış izni olmalıdır.
  - Sağlık adresi kimlik doğrulama istiyorsa V1'de izlenemez: header ya da kimlik bilgisi
    gönderilmez.
- **Etkilenen dokümanlar:** `docs/architecture.md` (iletişim kuralları; "alarm/bildirim" kapsam
  dışı satırı panel dışı kanallar içindir), `docs/security.md`.

## Alternatives considered
- **Denetimi panelin (Next.js) yapması:** Panel sunucusu her zaman açık bir arka plan süreci
  değildir. Denetim oturuma bağlanırdı; AC-7 karşılanmaz.
- **Ayrı bir izleme aracı (Uptime Kuma, Prometheus blackbox exporter):** Olgun ama ayrı bir
  servis, ayrı bir arayüz ve ayrı hesaplar demek. Durumun AIHub'ın log ve dashboard
  ekranlarıyla aynı yerde olması isteniyor.
- **İç adresleri de engellemek:** SSRF yüzeyi küçülürdü, ama izlenen uygulamalar kurum içinde
  olduğu için özellik işe yaramazdı.
- **Örnek başına kilit için Redis:** Çalışır, ama durum zaten PostgreSQL'de. Tek bir sorguyla
  sahiplenmek ek bir bağımlılık ve hata kipi getirmiyor.

## Revisit triggers
- AIHub kurum dışına, internete açık bir yerde çalıştırılırsa: iç adreslere izin yeniden
  değerlendirilir.
- Rol ve yetki geldiğinde: izlenen uygulama ekleme yetkisi kısıtlanabilir.
- Kimlik doğrulamalı sağlık adresleri istenirse.
