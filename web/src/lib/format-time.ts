"use client";

import { createElement, useSyncExternalStore } from "react";

/**
 * Zamanlar kullanıcının yerel saatinde ve saat dilimi belirtilerek gösterilir (Spec 0002, R-5).
 * Sunucu kullanıcının saat dilimini bilmez: ilk çizim UTC'dir, tarayıcıda hemen yerel saate döner
 * (plan 0002, risk 8). Her iki çıktı da saat dilimini yazar; hiçbir an belirsiz görünmez.
 */
function format(iso: string, timeZone?: string): string {
  return new Intl.DateTimeFormat("tr-TR", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    fractionalSecondDigits: 3,
    timeZoneName: "short",
    timeZone,
  }).format(new Date(iso));
}

const noSubscription = () => () => {};

export function useLocalTime(iso: string): string {
  return useSyncExternalStore(
    noSubscription,
    () => format(iso),
    () => format(iso, "UTC"),
  );
}

/** `datetime-local` alanının değeri (yerel saat, saniyesiz) ↔ saat dilimli ISO. */
export function isoToLocalInput(iso: string | undefined): string {
  if (!iso) return "";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

export function localInputToIso(value: string): string | undefined {
  if (!value) return undefined;
  const date = new Date(value); // saat dilimsiz değer tarayıcıda yerel saat olarak okunur
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

/** Sunucu bileşenlerinden kullanılan zaman öğesi; dosya .ts kaldığı için JSX yerine createElement. */
export function LocalTime({ iso }: { iso: string }) {
  return createElement("time", { dateTime: iso }, useLocalTime(iso));
}
