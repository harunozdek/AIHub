"use client";

import { startTransition, useActionState, useCallback, useEffect, useRef } from "react";
import type { EkranUygulamasi } from "@/lib/health";
import { uygulamaCikarAction, type AppFormState, type RemoveState } from "./actions";
import { UygulamaFormu } from "./uygulama-formu";

const secondary = "rounded border border-zinc-300 px-3 py-1 text-sm hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";

/**
 * Bir satırdaki iş (Spec 0012, CB-6). Liste tutar: uygulama Ayakta ile Down bölümü arasında yer değiştirince satır yeniden
 * kurulur, ama açık form ya da onay, düzenleme alanındaki taslak, son hata ve odaktaki öğe buradan geri gelir.
 */
export type SatirIsi = {
  mod: "idle" | "edit" | "confirm";
  taslak?: string;
  formDurumu?: AppFormState;
  cikarDurumu?: RemoveState;
  /** Odaktaki öğenin kimliği; satır yeniden kurulunca odak buna geri verilir. */
  odak?: string;
  /** Adres alanındaki imleç ya da seçim; odakla birlikte geri verilir (Review 1 F-6). */
  secim?: [number, number];
};

/**
 * Satırdaki "Düzenle" ve onaylı "İzlemeden çıkar" (Spec 0005, User experience, AC-9). Onay da düğmelerle yapılır: akış
 * yalnızca klavyeyle tamamlanır (AC-12). İş satırın dışında tutulur (yukarıda).
 */
export function SatirIslemleri({
  app,
  is,
  isGuncelle,
}: {
  app: EkranUygulamasi;
  is: SatirIsi | undefined;
  isGuncelle: (id: string, degisiklik: Partial<SatirIsi> | null) => void;
}) {
  const { id, serviceName, healthUrl } = app;
  const mod = is?.mod ?? "idle";
  // Çıkarmanın hatası işlemin kendisinde satırın işine yazılır; satır yeniden kurulunca da görünür. Effect'e
  // bırakılmaz: işlem sürerken satır bölüm değiştirirse effect koşmadan kalkardı (Review 1 F-2). Başarıda uygulama
  // listeden düşer.
  const [state, removeAction, pending] = useActionState<RemoveState, FormData>(async (onceki, form) => {
    const sonuc = await uygulamaCikarAction(onceki, form);
    // Transition: sunucunun yeni verisiyle aynı commit'e düşer (Review 2 B-1; bkz. UygulamaFormu).
    if (sonuc.message) startTransition(() => isGuncelle(id, { cikarDurumu: sonuc }));
    return sonuc;
  }, is?.cikarDurumu ?? {});
  const kap = useRef<HTMLDivElement>(null);

  const closeEdit = useCallback(() => isGuncelle(id, null), [id, isGuncelle]);
  const modSec = (yeni: SatirIsi["mod"]) => isGuncelle(id, { mod: yeni, formDurumu: undefined, cikarDurumu: undefined });

  // Satır yeniden kurulduysa (bölüm değişti) ve odak kaybolduysa, odak aynı öğeye, imleç aynı yere geri verilir.
  const odak = is?.odak;
  const secim = is?.secim;
  useEffect(() => {
    if (odak && (document.activeElement === document.body || document.activeElement === null)) {
      const oge = document.getElementById(odak);
      oge?.focus();
      if (secim && oge instanceof HTMLInputElement) oge.setSelectionRange(secim[0], secim[1]);
    }
    // Yalnızca kurulurken: sonraki odak değişiklikleri kullanıcınındır.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const odakKaydet = (e: React.FocusEvent) => {
    if (e.target.id && e.target.id !== is?.odak) isGuncelle(id, { odak: e.target.id, secim: undefined });
  };
  // Odak satırdan ayrılınca unutulur. Hedef yoksa ya kullanıcı boş bir yere tıklamıştır ya da öğe DOM'dan kalkıyordur
  // (bölüm değişimi): öğe bir an sonra hâlâ sayfadaysa ve odakta değilse kullanıcı ayrılmıştır (Review 1 F-6). Pencere
  // odağı kaybedince öğe odakta kalır, unutulmaz.
  const odakBirak = (e: React.FocusEvent) => {
    const unut = () => isGuncelle(id, { odak: undefined, secim: undefined });
    const hedef = e.relatedTarget as Node | null;
    if (hedef) {
      if (!kap.current?.contains(hedef) && is?.odak) unut();
      return;
    }
    const oge = e.target;
    setTimeout(() => {
      if (oge.isConnected && document.activeElement !== oge) unut();
    }, 0);
  };

  if (mod === "edit") {
    return (
      <div ref={kap} onFocus={odakKaydet} onBlur={odakBirak}>
        <UygulamaFormu
          mode="edit"
          id={id}
          serviceName={serviceName}
          healthUrl={healthUrl}
          taslak={is?.taslak}
          baslangicDurumu={is?.formDurumu}
          onTaslak={(taslak) => isGuncelle(id, { taslak })}
          onDurum={(formDurumu) => isGuncelle(id, { formDurumu })}
          onSecim={(secim) => isGuncelle(id, { secim })}
          onDone={closeEdit}
        />
      </div>
    );
  }

  return (
    <div ref={kap} onFocus={odakKaydet} onBlur={odakBirak}>
      {mod === "confirm" ? (
        <form action={removeAction} className="flex flex-wrap items-center gap-2">
          <input type="hidden" name="id" value={id} />
          <span className="text-sm">{serviceName} izlemeden çıkarılsın mı?</span>
          <button id={`${id}-evet`} type="submit" disabled={pending} className="rounded bg-red-800 px-3 py-1 text-sm text-white hover:bg-red-900 disabled:opacity-60">
            Evet, çıkar
          </button>
          <button id={`${id}-vazgec`} type="button" onClick={() => isGuncelle(id, null)} className={secondary}>
            Vazgeç
          </button>
        </form>
      ) : (
        <div className="flex flex-wrap gap-2">
          <button id={`${id}-duzenle`} type="button" onClick={() => modSec("edit")} aria-label={`${serviceName}: Düzenle`} className={secondary}>
            Düzenle
          </button>
          <button id={`${id}-cikar`} type="button" onClick={() => modSec("confirm")} aria-label={`${serviceName}: İzlemeden çıkar`} className={secondary}>
            İzlemeden çıkar
          </button>
        </div>
      )}
      {state.message && <p role="alert" className="mt-1 text-sm text-red-700 dark:text-red-300">{state.message}</p>}
    </div>
  );
}
