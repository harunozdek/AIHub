"use client";

import { useActionState, useState } from "react";
import { aktiflikAction, type SetActiveState } from "./actions";

/**
 * Pasifleştir / etkinleştir, onay adımıyla (Spec 0004, User experience). Onay da düğmelerle yapılır:
 * akış yalnızca klavyeyle tamamlanır (AC-12).
 */
export function AktiflikDugmesi({ id, userName, active }: { id: string; userName: string; active: boolean }) {
  const [state, action, pending] = useActionState<SetActiveState, FormData>(aktiflikAction, {});
  const [confirming, setConfirming] = useState(false);
  const label = active ? "Pasifleştir" : "Etkinleştir";

  return (
    <div>
      {confirming ? (
        <form action={action} className="flex flex-wrap items-center gap-2">
          <input type="hidden" name="id" value={id} />
          <input type="hidden" name="active" value={String(!active)} />
          <span className="text-sm">{userName} {active ? "pasifleştirilsin mi?" : "etkinleştirilsin mi?"}</span>
          <button type="submit" disabled={pending} className="rounded bg-red-800 px-3 py-1 text-sm text-white hover:bg-red-900 disabled:opacity-60">
            Evet, {label.toLowerCase()}
          </button>
          <button type="button" onClick={() => setConfirming(false)} className="rounded border border-zinc-300 px-3 py-1 text-sm hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800">
            Vazgeç
          </button>
        </form>
      ) : (
        <button
          type="button"
          onClick={() => setConfirming(true)}
          aria-label={`${userName}: ${label}`}
          className="rounded border border-zinc-300 px-3 py-1 text-sm hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800"
        >
          {label}
        </button>
      )}
      {state.message && <p role="alert" className="mt-1 text-sm text-red-700 dark:text-red-300">{state.message}</p>}
    </div>
  );
}
