"use client";

import { useActionState } from "react";
import { sifreDegistirAction, type ChangePasswordState } from "./actions";

const inputClass = "mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900";

/** Spec 0004, User experience: form, mevcut şifre yanlış, yeni şifre kurala uymuyor, başarılı. */
export function SifreFormu() {
  const [state, action, pending] = useActionState<ChangePasswordState, FormData>(sifreDegistirAction, {});
  const currentError = state.errors?.currentPassword?.[0];
  const newError = state.errors?.newPassword?.[0];

  return (
    <form action={action} aria-label="Şifre değiştir" className="grid gap-4">
      <div>
        <label htmlFor="currentPassword" className="block text-sm font-medium">Mevcut şifre</label>
        <input id="currentPassword" name="currentPassword" type="password" autoComplete="current-password" required
          aria-invalid={currentError ? true : undefined} aria-describedby={currentError ? "currentPassword-hata" : undefined} className={inputClass} />
        {currentError && <p id="currentPassword-hata" className="mt-1 text-sm text-red-700 dark:text-red-300">{currentError}</p>}
      </div>
      <div>
        <label htmlFor="newPassword" className="block text-sm font-medium">Yeni şifre</label>
        <input id="newPassword" name="newPassword" type="password" autoComplete="new-password" required
          aria-invalid={newError ? true : undefined} aria-describedby={["newPassword-ipucu", newError ? "newPassword-hata" : null].filter(Boolean).join(" ")} className={inputClass} />
        <p id="newPassword-ipucu" className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">12–128 karakter.</p>
        {newError && <p id="newPassword-hata" className="mt-1 text-sm text-red-700 dark:text-red-300">{newError}</p>}
      </div>
      <button type="submit" disabled={pending} className="rounded bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 disabled:opacity-60">
        {pending ? "Değiştiriliyor…" : "Şifreyi değiştir"}
      </button>
      <div aria-live="polite">
        {state.done && <p className="text-sm text-green-800 dark:text-green-300">Şifreniz değiştirildi. Diğer oturumlarınız kapatıldı.</p>}
        {state.failed && <p role="alert" className="text-sm text-red-700 dark:text-red-300">Şifre şu anda değiştirilemiyor. Lütfen biraz sonra yeniden deneyin.</p>}
      </div>
    </form>
  );
}
