"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useSyncExternalStore, type FormEvent } from "react";
import type { FieldErrors } from "@/lib/api-client";
import { LOG_LEVELS, listHref, type ListFilters } from "@/lib/filters";
import { isoToLocalInput, localInputToIso } from "@/lib/format-time";

const noSubscription = () => () => {};

/**
 * Filtre formu (Spec 0002, R-3). Zamanlar kullanıcının yerel saatiyle girilir ve adrese saat dilimli
 * ISO olarak yazılır. Doğrulama mesajları backend'den gelir ve ilgili alana bağlanır (AC-6, AC-10).
 */
export function LogFilters({ filters, services, errors }: { filters: ListFilters; services: string[]; errors: FieldErrors }) {
  const router = useRouter();
  // Yerel saat yalnızca tarayıcıda bilinir: sunucu çiziminde alanlar boş, tarayıcıda hemen dolu.
  const initialFrom = useSyncExternalStore(noSubscription, () => isoToLocalInput(filters.from), () => "");
  const initialTo = useSyncExternalStore(noSubscription, () => isoToLocalInput(filters.to), () => "");
  const [from, setFrom] = useState<string | null>(null);
  const [to, setTo] = useState<string | null>(null);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    router.push(
      listHref({
        service: (data.get("service") as string) || undefined,
        levels: data.getAll("level") as string[],
        exceptionType: (data.get("exceptionType") as string)?.trim() || undefined,
        from: localInputToIso(from ?? initialFrom),
        to: localInputToIso(to ?? initialTo),
      }),
    );
  }

  // Adreste tanımsız bir değer varsa (ör. elle yazılmış level=Debug) seçili kalır ki hata mesajı anlamlı olsun.
  const levels = [...LOG_LEVELS, ...filters.levels.filter((l) => !(LOG_LEVELS as readonly string[]).includes(l))];
  const serviceOptions = filters.service && !services.includes(filters.service) ? [filters.service, ...services] : services;

  return (
    <form onSubmit={submit} aria-label="Log filtreleri" className="mb-6 grid gap-4 rounded border border-zinc-200 p-4 md:grid-cols-[1fr_auto_1fr_1fr_1fr_auto] md:items-start dark:border-zinc-800">
      <div>
        <label htmlFor="service" className="block text-sm font-medium">Servis</label>
        <select id="service" name="service" defaultValue={filters.service ?? ""} className="mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900">
          <option value="">Tüm servisler</option>
          {serviceOptions.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </div>

      <fieldset aria-describedby={errors.level ? "level-hata" : undefined}>
        <legend className="text-sm font-medium">Seviye</legend>
        <div className="mt-1 flex flex-wrap gap-3">
          {levels.map((level) => (
            <label key={level} className="inline-flex items-center gap-1.5 text-sm">
              <input type="checkbox" name="level" value={level} defaultChecked={filters.levels.includes(level)} />
              {level}
            </label>
          ))}
        </div>
        <FieldError id="level-hata" messages={errors.level} />
      </fieldset>

      <div>
        <label htmlFor="exceptionType" className="block text-sm font-medium">Exception türü</label>
        <input
          id="exceptionType"
          name="exceptionType"
          type="text"
          autoComplete="off"
          defaultValue={filters.exceptionType ?? ""}
          placeholder="ör. System.TimeoutException"
          aria-invalid={errors.exceptionType ? true : undefined}
          aria-describedby={errors.exceptionType ? "exceptionType-hata" : undefined}
          className="mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900"
        />
        <FieldError id="exceptionType-hata" messages={errors.exceptionType} />
      </div>

      <div>
        <label htmlFor="from" className="block text-sm font-medium">Başlangıç (yerel saat)</label>
        <input
          id="from"
          type="datetime-local"
          value={from ?? initialFrom}
          onChange={(e) => setFrom(e.target.value)}
          aria-invalid={errors.from ? true : undefined}
          aria-describedby={errors.from ? "from-hata" : undefined}
          className="mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900"
        />
        <FieldError id="from-hata" messages={errors.from} />
      </div>

      <div>
        <label htmlFor="to" className="block text-sm font-medium">Bitiş (yerel saat)</label>
        <input
          id="to"
          type="datetime-local"
          value={to ?? initialTo}
          onChange={(e) => setTo(e.target.value)}
          aria-invalid={errors.to ? true : undefined}
          aria-describedby={errors.to ? "to-hata" : undefined}
          className="mt-1 w-full rounded border border-zinc-400 bg-white px-2 py-1.5 dark:border-zinc-600 dark:bg-zinc-900"
        />
        <FieldError id="to-hata" messages={errors.to} />
      </div>

      <div className="flex gap-2 md:mt-6">
        <button type="submit" className="rounded bg-blue-700 px-4 py-1.5 text-white hover:bg-blue-800">Uygula</button>
        <Link href="/logs" className="rounded border border-zinc-300 px-4 py-1.5 hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800">Temizle</Link>
      </div>

      {!filters.from && !filters.to && <p className="text-sm text-zinc-600 md:col-span-6 dark:text-zinc-400">Zaman aralığı seçilmezse son 24 saat gösterilir.</p>}
    </form>
  );
}

function FieldError({ id, messages }: { id: string; messages?: string[] }) {
  if (!messages?.length) {
    return null;
  }
  return (
    <p id={id} className="mt-1 text-sm text-red-700 dark:text-red-300">
      {messages.join(" ")}
    </p>
  );
}
