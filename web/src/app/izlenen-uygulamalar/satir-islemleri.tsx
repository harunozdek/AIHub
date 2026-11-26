"use client";

import { useActionState, useCallback, useEffect, useRef } from "react";
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
  const [state, removeAction, pending] = useActionState<RemoveState, FormData>(uygulamaCikarAction, is?.cikarDurumu ?? {});
  const kap = useRef<HTMLDivElement>(null);
  const ilkDurum = useRef(state);

  const closeEdit = useCallback(() => isGuncelle(id, null), [id, isGuncelle]);
  const modSec = (yeni: SatirIsi["mod"]) => isGuncelle(id, { mod: yeni, formDurumu: undefined, cikarDurumu: undefined });

  // Çıkarma hatası satır yeniden kurulunca da görünür.
  useEffect(() => {
    if (state !== ilkDurum.current) isGuncelle(id, { cikarDurumu: state });
  }, [state, id, isGuncelle]);

  // Satır yeniden kurulduysa (bölüm değişti) ve odak kaybolduysa, odak aynı öğeye geri verilir.
  const odak = is?.odak;
  useEffect(() => {
    if (odak && (document.activeElement === document.body || document.activeElement === null)) {
      document.getElementById(odak)?.focus();
    }
    // Yalnızca kurulurken: sonraki odak değişiklikleri kullanıcınındır.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const odakKaydet = (e: React.FocusEvent) => {
    if (e.target.id && e.target.id !== is?.odak) isGuncelle(id, { odak: e.target.id });
  };
  // Odak satırdan başka bir öğeye geçince unutulur; öğe DOM'dan kalkınca (bölüm değişimi) hedef yoktur, hatırlanır.
  const odakBirak = (e: React.FocusEvent) => {
    const hedef = e.relatedTarget as Node | null;
    if (hedef && !kap.current?.contains(hedef) && is?.odak) isGuncelle(id, { odak: undefined });
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
