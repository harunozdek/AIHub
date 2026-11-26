"use client";

import { useActionState } from "react";
import { uygulamaDuzenleAction, uygulamaEkleAction, type AppFormState } from "./actions";

const inputClass = "mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900";

type Props =
  | { mode: "add" }
  | {
      mode: "edit";
      id: string;
      serviceName: string;
      healthUrl: string;
      onDone: () => void;
      /** Spec 0012, CB-6: satır yeniden kurulunca (bölüm değişimi) girilen değer ve son hata geri gelir. */
      taslak?: string;
      baslangicDurumu?: AppFormState;
      onTaslak?: (taslak: string) => void;
      onDurum?: (durum: AppFormState) => void;
      /** Review 1 F-6: imleç konumu da satırın dışında tutulur. */
      onSecim?: (secim: [number, number]) => void;
    };

/**
 * "Uygulama ekle" ve "Düzenle" formları (Spec 0005, User experience): alan yanında doğrulama hataları,
 * izlenen ServiceName mesajı, kaydediliyor ve başarılı durumları. Düzenlemede yalnızca adres değişir. Düzenleme
 * formunun taslağı ve son hatası satırın dışında tutulur (Spec 0012, CB-6).
 */
export function UygulamaFormu(props: Props) {
  const edit = props.mode === "edit";
  // Düzenlemenin sonucu işlemin kendisinde satıra bildirilir: kaydedilince form açıkça kapanır (review 5, N5-1),
  // hata satırın işine yazılır. Bir effect'e bırakılmaz: kayıt uygulamayı başka bölüme taşıyınca bu form effect
  // koşmadan kalkar ve form yeni bölümde açık kalırdı (0012 Review 1 F-2); effect her çizimde yeniden de koşuyordu
  // (F-1).
  const [state, action, pending] = useActionState<AppFormState, FormData>(
    async (onceki, form) => {
      if (!edit) return uygulamaEkleAction(onceki, form);
      const sonuc = await uygulamaDuzenleAction(onceki, form);
      if (sonuc.saved) props.onDone();
      else props.onDurum?.(sonuc);
      return sonuc;
    },
    (edit && props.baslangicDurumu) || {},
  );
  const prefix = edit ? `duzenle-${props.id}` : "ekle";
  const fields = [
    ...(edit ? [] : [{ name: "serviceName", label: "ServiceName", hint: "Log kayıtlarındaki ServiceName ile aynı yazılır." }]),
    { name: "healthUrl", label: "Sağlık adresi", hint: "http:// ya da https:// ile başlayan tam adres." },
  ];

  return (
    <form
      action={action}
      aria-label={edit ? `${props.serviceName}: sağlık adresini düzenle` : "Uygulama ekle"}
      className={edit ? "grid gap-2" : "mb-6 grid gap-4 rounded border border-zinc-200 p-4 md:grid-cols-[1fr_2fr_auto] md:items-start dark:border-zinc-800"}
    >
      {edit && <input type="hidden" name="id" value={props.id} />}
      {fields.map((field) => {
        const id = `${prefix}-${field.name}`;
        const error = state.errors?.[field.name]?.[0];
        return (
          <div key={field.name}>
            <label htmlFor={id} className="block text-sm font-medium">{field.label}</label>
            <input
              id={id}
              name={field.name}
              type="text"
              autoComplete="off"
              required
              defaultValue={edit && field.name === "healthUrl" ? (props.taslak ?? props.healthUrl) : undefined}
              onChange={edit && field.name === "healthUrl" ? (e) => props.onTaslak?.(e.target.value) : undefined}
              onSelect={
                edit && field.name === "healthUrl"
                  ? (e) => props.onSecim?.([e.currentTarget.selectionStart ?? 0, e.currentTarget.selectionEnd ?? 0])
                  : undefined
              }
              aria-invalid={error ? true : undefined}
              aria-describedby={[`${id}-ipucu`, error ? `${id}-hata` : null].filter(Boolean).join(" ")}
              className={inputClass}
            />
            <p id={`${id}-ipucu`} className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">{field.hint}</p>
            {error && <p id={`${id}-hata`} className="mt-1 text-sm text-red-700 dark:text-red-300">{error}</p>}
          </div>
        );
      })}
      <div className={edit ? "flex gap-2" : "md:mt-6"}>
        <button id={`${prefix}-kaydet`} type="submit" disabled={pending} className="rounded bg-blue-700 px-4 py-2 text-sm text-white hover:bg-blue-800 disabled:opacity-60">
          {pending ? "Kaydediliyor…" : edit ? "Kaydet" : "Uygulama ekle"}
        </button>
        {edit && (
          <button id={`${prefix}-vazgec`} type="button" onClick={props.onDone} className="rounded border border-zinc-300 px-3 py-2 text-sm hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800">
            Vazgeç
          </button>
        )}
      </div>
      <div aria-live="polite" className={edit ? undefined : "md:col-span-3"}>
        {!edit && state.saved && <p className="text-sm text-green-800 dark:text-green-300">{state.saved} izlemeye alındı.</p>}
        {state.message && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{state.message}</p>}
      </div>
    </form>
  );
}
