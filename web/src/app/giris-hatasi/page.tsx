import type { Metadata } from "next";

export const metadata: Metadata = { title: "AIHub — Giriş yapılamadı" };

/** Giriş iptal edildi veya başarısız oldu: log içeriği gösterilmez, iç ayrıntı verilmez (Spec 0002, AC-2, R-9). */
export default async function LoginErrorPage({ searchParams }: { searchParams: Promise<{ neden?: string }> }) {
  const { neden } = await searchParams;
  const cancelled = neden === "iptal";

  return (
    <section className="mx-auto max-w-md py-16 text-center" aria-labelledby="baslik">
      <h1 id="baslik" className="text-xl font-semibold">
        {cancelled ? "Giriş iptal edildi" : "Giriş yapılamadı"}
      </h1>
      <p className="mt-3 text-zinc-600 dark:text-zinc-400">
        {cancelled
          ? "Logları görmek için giriş yapmanız gerekiyor."
          : "Kimlik doğrulama şu anda tamamlanamadı. Lütfen biraz sonra yeniden deneyin."}
      </p>
      <a href="/auth/giris" className="mt-6 inline-block rounded bg-blue-700 px-4 py-2 text-white hover:bg-blue-800">
        Yeniden giriş yap
      </a>
    </section>
  );
}
