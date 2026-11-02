import Link from "next/link";

/**
 * Boş liste (Spec 0002, States):
 * - depoda hiç kayıt yok → "Henüz log kaydı yok";
 * - filtre yok ama son 24 saatte kayıt yok → daha eski bir aralık seçme yolu (review 1, F-05);
 * - filtreye uyan kayıt yok → "filtreleri temizle" seçeneğiyle "Bu filtrelere uyan kayıt yok".
 */
export type EmptyKind = "store-empty" | "no-recent" | "no-match";

export function EmptyState({ kind }: { kind: EmptyKind }) {
  return (
    <div role="status" className="rounded border border-dashed border-zinc-300 p-10 text-center dark:border-zinc-700">
      {kind === "store-empty" && <p className="font-medium">Henüz log kaydı yok</p>}
      {kind === "no-recent" && (
        <>
          <p className="font-medium">Son 24 saatte kayıt yok</p>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">Daha eski kayıtlar için Başlangıç ve Bitiş alanlarından bir aralık seçin.</p>
        </>
      )}
      {kind === "no-match" && (
        <>
          <p className="font-medium">Bu filtrelere uyan kayıt yok</p>
          <Link href="/logs" className="mt-4 inline-block text-blue-700 underline hover:text-blue-900 dark:text-blue-300">
            Filtreleri temizle
          </Link>
        </>
      )}
    </div>
  );
}
