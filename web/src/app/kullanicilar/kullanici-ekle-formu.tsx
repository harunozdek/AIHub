"use client";

import { useActionState } from "react";
import { kullaniciEkleAction, type CreateUserState } from "./actions";

const inputClass = "mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900";

const fields = [
  { name: "userName", label: "Kullanıcı adı", type: "text", autoComplete: "off", hint: "3–64 karakter: harf, rakam, nokta, alt çizgi, tire." },
  { name: "displayName", label: "Görünen ad", type: "text", autoComplete: "off", hint: undefined },
  { name: "password", label: "Başlangıç şifresi", type: "password", autoComplete: "new-password", hint: "12–128 karakter." },
] as const;

/** Spec 0004, User experience: alan yanında doğrulama hataları, alınmış kullanıcı adı mesajı. */
export function KullaniciEkleFormu() {
  const [state, action, pending] = useActionState<CreateUserState, FormData>(kullaniciEkleAction, {});

  return (
    <form action={action} aria-label="Kullanıcı ekle" className="mb-6 grid gap-4 rounded border border-zinc-200 p-4 md:grid-cols-[1fr_1fr_1fr_auto] md:items-start dark:border-zinc-800">
      {fields.map((field) => {
        const error = state.errors?.[field.name]?.[0];
        const describedBy = [field.hint ? `${field.name}-ipucu` : null, error ? `${field.name}-hata` : null].filter(Boolean).join(" ") || undefined;
        return (
          <div key={field.name}>
            <label htmlFor={field.name} className="block text-sm font-medium">{field.label}</label>
            <input
              id={field.name}
              name={field.name}
              type={field.type}
              autoComplete={field.autoComplete}
              required
              aria-invalid={error ? true : undefined}
              aria-describedby={describedBy}
              className={inputClass}
            />
            {field.hint && <p id={`${field.name}-ipucu`} className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">{field.hint}</p>}
            {error && <p id={`${field.name}-hata`} className="mt-1 text-sm text-red-700 dark:text-red-300">{error}</p>}
          </div>
        );
      })}
      <div className="md:mt-6">
        <button type="submit" disabled={pending} className="rounded bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 disabled:opacity-60">
          {pending ? "Ekleniyor…" : "Kullanıcı ekle"}
        </button>
      </div>
      <div aria-live="polite" className="md:col-span-4">
        {state.created && <p className="text-sm text-green-800 dark:text-green-300">{state.created} eklendi.</p>}
        {state.failed && <p role="alert" className="text-sm text-red-700 dark:text-red-300">Kullanıcı şu anda eklenemiyor. Lütfen biraz sonra yeniden deneyin.</p>}
      </div>
    </form>
  );
}
