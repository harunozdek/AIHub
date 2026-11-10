# Spec 0008 — AIHub'ın kendi sağlık denetimi

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0008-plan.md`

## Intent
Ekip, AIHub'ın kendisinin de diğer uygulamalar gibi izlenmesini istiyor (harun.ozdek, 2026-11-10).
Backend ve panelin her biri durumunu bildiren bir sağlık adresi sunar. Bu adresler "İzlenen
uygulamalar" listesine, elle eklemeye gerek kalmadan, AIHub'ın kendi iki kaydı olarak girer:
`AIHub.Api` ve `AIHub.Panel`. Böylece AIHub'ın bir parçası ya da bağımlılığı bozulduğunda bu,
panelde diğer uygulamalarla aynı Down uyarısıyla görünür.

Başarı şöyle tanımlanır: yapılandırılmış her ortamda iki kayıt listede görünür ve ilk denetimden
sonra Ayakta durumuna geçer. AIHub tamamen durduğunda bunu kendisi bildiremez; dışarıdan izleme
bilerek kapsam dışıdır.

## Requirements
- **R-1 Backend sağlık adresi:** Backend'in yanıtı şunu söyler: backend çalışıyor ve hem log
  deposuna (PostgreSQL) hem kuyruğa (Redis) erişebiliyor.
  - Hepsi yerindeyse adres başarı döner.
  - Biri erişilemezse başarısızlık döner. Başarısızlık 0005'in denetim kuralında Down sayılır.
  - Bağımlılık beklerse yanıt en geç 0005'in denetim zaman aşımından (5 sn) önce döner; yanıt
    vermeyen bir bağımlılık adresi kilitlemez.
- **R-2 Panel sağlık adresi:** Panelin yanıtı yalnızca panelin kendisinin çalıştığını söyler.
  Backend'e erişimi denetlemez; backend arızası panel kaydını Down yapmaz.
- **R-3 Erişim ve içerik:**
  - İki adres de girişsiz açılır; denetim isteği oturum taşımaz.
  - Yanıt yalnızca durumu söyler (ör. "Healthy" / "Unhealthy"). Bağlantı dizesi, sunucu adı,
    sürüm, hata mesajı ya da yığın izi içermez (`docs/security.md`).
  - Adresler önbelleğe alınmaz.
  - Panelin diğer bütün sayfaları giriş istemeye devam eder.
- **R-4 Kayıtların eklenmesi:**
  - İki sağlık adresi ortam yapılandırmasından okunur. Uygulama açılırken o ServiceName ile izlenen
    bir uygulama yoksa kayıt eklenir.
  - Yapılandırmada bir adres boşsa o kayıt eklenmez; diğerinin eklenmesini etkilemez.
  - Aynı ServiceName ile bir kayıt zaten varsa (elle eklenmiş olsa bile) dokunulmaz: adresi,
    durumu ve ekleyeni değişmez.
  - Adresler 0005 R-3'teki adres kuralından geçer. Kurala uymayan bir adres eklenmez; neden süreç
    loguna yazılır ve uygulama açılmaya devam eder.
  - Birden çok backend örneği aynı anda açılırsa kayıt bir kez eklenir.
  - Açılışta log deposuna erişilemezse ekleme yapılamaz: neden süreç loguna yazılır, uygulama
    açılmaya devam eder ve ekleme bir sonraki açılışta yeniden denenir (spec onayı; harun.ozdek,
    2026-11-10).
- **R-5 Silme kalıcıdır:** Kullanıcı bu kayıtlardan birini izlemeden çıkarırsa sonraki açılışlarda
  yeniden eklenmez. Yeniden izlemek için kullanıcı ekrandan elle ekler.
- **R-6 Ekleyen:** Otomatik eklenen kayıtlarda ekleyen "Sistem" olarak görünür. Bu kayıtlar
  bundan sonra diğer kayıtlar gibi düzenlenir ve izlemeden çıkarılır (0005 R-2).

## User experience
- **Flow:**
  1. Yönetici ortam yapılandırmasına iki sağlık adresini yazar ve AIHub'ı açar.
  2. Kullanıcı "İzlenen uygulamalar" ekranında `AIHub.Api` ve `AIHub.Panel` kayıtlarını görür;
     ekleyen "Sistem"dir.
  3. İlk denetimden sonra kayıtlar Ayakta olur. Bir bağımlılık bozulursa `AIHub.Api` Down olur ve
     her ekranda 0005'in Down uyarısı görünür.
- **States:**
  - Var olan ekranlar ve durumları (0005) değişmez.
  - Yeni durum yalnızca "ekleyen: Sistem" görünümüdür.
  - Sağlık adreslerinin kendisi bir ekran değildir; düz metin yanıt verir.
- **Design reference:** none — the flow above is the design.
- **Accessibility:** Var olan ekranlarda değişiklik yok; "Sistem" metin olarak yazılır.

## Constraints & out of scope
- **Kendi kendini izleme sınırı:** Backend tamamen çökerse denetimi yapan worker da durur ve Down
  uyarısı üretilemez. Bu özellik bağımlılık arızalarını ve panel arızasını yakalar. Tam çöküş için
  dışarıdan izleme ayrı bir iştir.
  - PostgreSQL erişilemezken backend sağlık adresi doğru biçimde başarısızlık döner, ama denetim
    sonucu ve Down durumu PostgreSQL'e yazıldığı için `AIHub.Api` Down gösterilemez; panel 0005'in
    "durum güncellenemedi" notunu gösterir (plan 0008, risk 1; harun.ozdek, 2026-11-10).
- **Kapsam dışı:**
  - AIHub'ın tam çöküşünü dışarıdan izleme ve bildirme.
  - Sağlık yanıtında ayrıntı (bileşen bazında durum, süreler, sürüm).
  - Hazırlık (readiness) ile canlılık (liveness) için ayrı adresler; konteyner orkestrasyonu yok.
  - Silinen kaydı otomatik geri getirme (R-5).
  - Panelin backend'e erişimini denetleme (R-2).
- **Split:** Önerilmedi. Tek yetenek var (AIHub'ın kendini izlemesi); sağlık adresleri ve otomatik
  kayıt birlikte anlamlıdır.

## Acceptance criteria
- [ ] AC-1 — PostgreSQL ve Redis erişilebilirken backend sağlık adresi girişsiz istekte başarı ve
  "Healthy" döner.
- [ ] AC-2 — PostgreSQL ya da Redis erişilemezken backend sağlık adresi başarısızlık döner. Bağımlılık
  yanıt vermiyorsa yanıt 5 saniyeden önce gelir.
- [ ] AC-3 — Panel sağlık adresi girişsiz istekte başarı döner; backend kapalıyken de başarı döner.
  Panelin diğer sayfaları girişsiz istekte girişe yönlendirmeye devam eder.
- [ ] AC-4 — Sağlık yanıtları, arıza durumunda da, bağlantı dizesi, sunucu adı, sürüm, hata
  mesajı ya da yığın izi içermez ve önbelleğe alınmaz.
- [ ] AC-5 — İki adres yapılandırılmış ve kayıt yokken açılışta `AIHub.Api` ve `AIHub.Panel`
  eklenir; ekleyen "Sistem" görünür; ilk denetimden sonra Ayakta olurlar.
- [ ] AC-6 — Bir adres boşsa yalnızca diğeri eklenir; kurala uymayan bir adres eklenmez, neden
  süreç loguna yazılır ve uygulama açılır.
- [ ] AC-7 — Aynı ServiceName ile kayıt zaten varsa açılışta adresi, durumu ve ekleyeni değişmez.
  İki backend örneği aynı anda açılınca her kayıt bir kez bulunur.
- [ ] AC-8 — Kullanıcı kaydı izlemeden çıkardıktan sonra yeniden açılışta kayıt eklenmez.
- [ ] AC-9 — Redis erişilemezken `AIHub.Api` Down olur ve 0005'in Down uyarısı görünür; Redis
  düzelince Ayakta olur (plan 0008, risk 1; harun.ozdek, 2026-11-10).

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
