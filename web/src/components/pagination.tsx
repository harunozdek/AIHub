import Link from "next/link";
import { listHref, type ListFilters } from "@/lib/filters";

/** Keyset sayfalama: işaretler backend'den opak gelir, filtreler korunur (Spec 0002, R-4). */
export function Pagination({ filters, newer, older }: { filters: ListFilters; newer: string | null; older: string | null }) {
  if (!newer && !older) {
    return null;
  }

  const link = "rounded border border-zinc-300 px-4 py-2 hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800";
  const disabled = "rounded border border-zinc-200 px-4 py-2 text-zinc-500 dark:border-zinc-800";

  return (
    <nav aria-label="Sayfalar" className="mt-4 flex justify-between">
      {newer ? (
        <Link href={listHref(filters, newer)} className={link} rel="prev">
          ← Önceki sayfa
        </Link>
      ) : (
        <span className={disabled} aria-disabled="true">← Önceki sayfa</span>
      )}
      {older ? (
        <Link href={listHref(filters, older)} className={link} rel="next">
          Sonraki sayfa →
        </Link>
      ) : (
        <span className={disabled} aria-disabled="true">Sonraki sayfa →</span>
      )}
    </nav>
  );
}
