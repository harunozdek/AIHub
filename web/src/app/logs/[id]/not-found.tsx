import Link from "next/link";

/** Var olmayan ve bozuk kimlik dışarıdan ayırt edilmez: ikisi de "bulunamadı" (Spec 0002, AC-9). */
export default function LogNotFound() {
  return (
    <section className="py-10 text-center" aria-labelledby="baslik">
      <h1 id="baslik" className="text-xl font-semibold">Kayıt bulunamadı</h1>
      <p className="mt-2 text-zinc-600 dark:text-zinc-400">Bu adreste bir log kaydı yok.</p>
      <Link href="/logs" className="mt-6 inline-block text-blue-700 underline hover:text-blue-900 dark:text-blue-300">
        Log listesine git
      </Link>
    </section>
  );
}
