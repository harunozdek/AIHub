import type { Metadata } from "next";
import { EmptyState } from "@/components/empty-state";
import { ErrorState } from "@/components/error-state";
import { LogFilters } from "@/components/log-filters";
import { LogTable } from "@/components/log-table";
import { Pagination } from "@/components/pagination";
import { listLogs, listServices } from "@/lib/api-client";
import { hasActiveFilters, listHref, parseFilters, toSearchParams } from "@/lib/filters";

export const metadata: Metadata = { title: "AIHub — Loglar" };

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };

/** Log listesi (Spec 0002, R-2 … R-4, R-8, R-9). Filtreler adresten okunur; sorgu sunucuda yapılır. */
export default async function LogsPage({ searchParams }: Props) {
  const filters = parseFilters(await searchParams);
  const listUrl = listHref(filters, filters.cursor);

  const [page, services] = await Promise.all([
    listLogs(toSearchParams(filters, { withCursor: true }), listUrl),
    listServices(listUrl),
  ]);

  if ((!page.ok && page.kind !== "validation") || !services.ok) {
    return (
      <>
        <h1 className="mb-4 text-xl font-semibold">Loglar</h1>
        <ErrorState />
      </>
    );
  }

  const errors = page.ok ? {} : page.kind === "validation" ? page.errors : {};

  return (
    <>
      <h1 className="mb-4 text-xl font-semibold">Loglar</h1>
      <LogFilters key={listUrl} filters={filters} services={services.data.services} errors={errors} />

      {errors.cursor && (
        <p role="alert" className="mb-4 text-sm text-red-700 dark:text-red-300">
          {errors.cursor.join(" ")}
        </p>
      )}

      {page.ok &&
        (page.data.items.length === 0 ? (
          <EmptyState storeEmpty={services.data.services.length === 0 && !hasActiveFilters(filters)} />
        ) : (
          <>
            <LogTable items={page.data.items} listUrl={listUrl} />
            {/* Sayfalar, ilk sayfada çözülen aralıkta kalır: varsayılan "son 24 saat" sayfalar arasında kaymaz (R-4, review 1 F-02). */}
            <Pagination filters={{ ...filters, from: page.data.from, to: page.data.to }} newer={page.data.newer} older={page.data.older} />
          </>
        ))}
    </>
  );
}
