import type { Metadata } from "next";
import Link from "next/link";
import { DownUyarisi } from "@/components/down-uyarisi";
import { getSession } from "@/lib/auth/session";
import "./globals.css";

export const metadata: Metadata = {
  title: "AIHub — Loglar",
  description: "Kurum servislerinin logları",
};

export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const session = await getSession();
  const signedIn = Boolean(session.accessToken);

  return (
    <html lang="tr">
      <body className="min-h-screen">
        <a href="#icerik" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:bg-white focus:px-3 focus:py-2 dark:focus:bg-zinc-900">
          İçeriğe geç
        </a>
        <header className="border-b border-zinc-200 dark:border-zinc-800">
          <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3">
            <div className="flex items-center gap-6">
              <Link href="/logs" className="text-lg font-semibold">
                AIHub
              </Link>
              {signedIn && (
                <nav aria-label="Ana menü" className="flex gap-4 text-sm">
                  <Link href="/logs" className="hover:underline">Loglar</Link>
                  <Link href="/kullanicilar" className="hover:underline">Kullanıcılar</Link>
                  <Link href="/izlenen-uygulamalar" className="hover:underline">İzlenen uygulamalar</Link>
                </nav>
              )}
            </div>
            {signedIn && (
              <div className="flex items-center gap-3 text-sm">
                {session.name && <span className="text-zinc-600 dark:text-zinc-400">{session.name}</span>}
                <Link href="/sifre-degistir" className="hover:underline">Şifre değiştir</Link>
                <form action="/auth/cikis" method="post">
                  <button type="submit" className="rounded border border-zinc-300 px-3 py-1.5 hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800">
                    Çıkış yap
                  </button>
                </form>
              </div>
            )}
          </div>
        </header>
        {/* Spec 0005, R-7: oturum açıkken her ekranda; giriş ekranında yok. */}
        {signedIn && <DownUyarisi />}
        <main id="icerik" className="mx-auto max-w-7xl px-4 py-6">
          {children}
        </main>
      </body>
    </html>
  );
}
