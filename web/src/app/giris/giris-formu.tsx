"use client";

import { useActionState } from "react";
import { girisYapAction, type LoginState } from "./actions";

const inputClass = "mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900";

const messages = {
  invalid: "Kullanıcı adı veya şifre hatalı.",
  unavailable: "Giriş şu anda yapılamıyor. Lütfen biraz sonra yeniden deneyin.",
} as const;

/** Spec 0004, User experience: boş form, gönderiliyor, genel hata. Hata form alanlarına bağlıdır. */
export function GirisFormu({ returnTo }: { returnTo: string }) {
  const [state, action, pending] = useActionState<LoginState, FormData>(girisYapAction, {});
  const errorId = state.error ? "giris-hata" : undefined;

  return (
    <form action={action} aria-label="Giriş" className="grid gap-4">
      <input type="hidden" name="returnTo" value={returnTo} />
      {state.error && (
        <p id="giris-hata" role="alert" className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100">
          {messages[state.error]}
        </p>
      )}
      <div>
        <label htmlFor="userName" className="block text-sm font-medium">Kullanıcı adı</label>
        <input id="userName" name="userName" autoComplete="username" required aria-describedby={errorId} className={inputClass} />
      </div>
      <div>
        <label htmlFor="password" className="block text-sm font-medium">Şifre</label>
        <input id="password" name="password" type="password" autoComplete="current-password" required aria-describedby={errorId} className={inputClass} />
      </div>
      <button type="submit" disabled={pending} className="rounded bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 disabled:opacity-60">
        {pending ? "Giriş yapılıyor…" : "Giriş yap"}
      </button>
    </form>
  );
}
