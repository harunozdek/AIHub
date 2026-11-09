"use client";

import { useActionState, useCallback, useState } from "react";
import { uygulamaCikarAction, type RemoveState } from "./actions";
import { UygulamaFormu } from "./uygulama-formu";

const secondary = "rounded border border-zinc-300 px-3 py-1 text-sm hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";

/**
 * Satırdaki "Düzenle" ve onaylı "İzlemeden çıkar" (Spec 0005, User experience, AC-9). Onay da düğmelerle
 * yapılır: akış yalnızca klavyeyle tamamlanır (AC-12).
 */
export function SatirIslemleri({ id, serviceName, healthUrl }: { id: string; serviceName: string; healthUrl: string }) {
  const [mode, setMode] = useState<"idle" | "edit" | "confirm">("idle");
  const [state, removeAction, pending] = useActionState<RemoveState, FormData>(uygulamaCikarAction, {});
  const closeEdit = useCallback(() => setMode("idle"), []);

  if (mode === "edit") {
    return <UygulamaFormu mode="edit" id={id} serviceName={serviceName} healthUrl={healthUrl} onDone={closeEdit} />;
  }

  return (
    <div>
      {mode === "confirm" ? (
        <form action={removeAction} className="flex flex-wrap items-center gap-2">
          <input type="hidden" name="id" value={id} />
          <span className="text-sm">{serviceName} izlemeden çıkarılsın mı?</span>
          <button type="submit" disabled={pending} className="rounded bg-red-800 px-3 py-1 text-sm text-white hover:bg-red-900 disabled:opacity-60">
            Evet, çıkar
          </button>
          <button type="button" onClick={() => setMode("idle")} className={secondary}>
            Vazgeç
          </button>
        </form>
      ) : (
        <div className="flex flex-wrap gap-2">
          <button type="button" onClick={() => setMode("edit")} aria-label={`${serviceName}: Düzenle`} className={secondary}>
            Düzenle
          </button>
          <button type="button" onClick={() => setMode("confirm")} aria-label={`${serviceName}: İzlemeden çıkar`} className={secondary}>
            İzlemeden çıkar
          </button>
        </div>
      )}
      {state.message && <p role="alert" className="mt-1 text-sm text-red-700 dark:text-red-300">{state.message}</p>}
    </div>
  );
}
