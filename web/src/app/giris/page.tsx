import type { Metadata } from "next";
import { safeReturnTo } from "@/lib/auth/return-to";
import { GirisFormu } from "./giris-formu";

export const metadata: Metadata = { title: "AIHub — Giriş" };

/**
 * Giriş ekranı (Spec 0004). Oturum süresi dolduysa ya da kullanıcı çıkış yaptıysa bunu söyler; log
 * içeriği göstermez.
 */
export default async function GirisPage({ searchParams }: { searchParams: Promise<{ returnTo?: string; neden?: string; cikis?: string }> }) {
  const params = await searchParams;
  const notice =
    params.neden === "sure" ? "Oturumunuzun süresi doldu. Lütfen yeniden giriş yapın." : params.cikis ? "Çıkış yaptınız." : null;

  return (
    <section className="mx-auto max-w-sm py-12" aria-labelledby="baslik">
      <h1 id="baslik" className="mb-6 text-xl font-semibold">Giriş</h1>
      {notice && (
        <p role="status" className="mb-4 rounded border border-zinc-300 bg-zinc-50 p-3 text-sm dark:border-zinc-700 dark:bg-zinc-900">
          {notice}
        </p>
      )}
      <GirisFormu returnTo={safeReturnTo(params.returnTo)} />
    </section>
  );
}
