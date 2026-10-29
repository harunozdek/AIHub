import Link from "next/link";

/**
 * Boş liste: depoda hiç kayıt yoksa "Henüz log kaydı yok", kayıt var ama filtreye uyan yoksa
 * "filtreleri temizle" seçeneğiyle "Bu filtrelere uyan kayıt yok" (Spec 0002, States).
 */
export function EmptyState({ storeEmpty }: { storeEmpty: boolean }) {
  return (
    <div role="status" className="rounded border border-dashed border-zinc-300 p-10 text-center dark:border-zinc-700">
      {storeEmpty ? (
        <p className="font-medium">Henüz log kaydı yok</p>
      ) : (
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
