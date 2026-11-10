import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE, unsealSession } from "@/lib/auth/session";

/**
 * Oturumsuz hiçbir istek panel içeriğine ulaşmaz (Spec 0004, AC-1). Oturum girişten itibaren mutlak 8
 * saattir; yenileme yoktur (R-10). Süresi dolan kullanıcı giriş ekranına, girişten sonra da aynı adrese
 * döner (AC-2, AC-10).
 */
export async function middleware(request: NextRequest) {
  const seal = request.cookies.get(SESSION_COOKIE)?.value;
  const session = await unsealSession(seal).catch(() => null);
  const now = Math.floor(Date.now() / 1000);

  if (session && session.expiresAt > now) {
    return withNoStore(NextResponse.next());
  }

  const login = new URL("/giris", request.url);
  login.searchParams.set("returnTo", request.nextUrl.pathname + request.nextUrl.search);
  if (seal) {
    login.searchParams.set("neden", "sure");
  }
  const response = NextResponse.redirect(login);
  response.cookies.delete(SESSION_COOKIE);
  return response;
}

// Log içeriği taşıyan yanıtlar tarayıcı önbelleğine yazılmaz: çıkıştan sonra geri tuşu içerik göstermez.
function withNoStore(response: NextResponse) {
  response.headers.set("Cache-Control", "no-store");
  return response;
}

export const config = {
  // Giriş ekranı, çıkış, statik dosyalar ve yalnızca tam `/health` adresi (Spec 0008, R-3) oturum istemez.
  matcher: ["/((?!giris|auth/|_next/static|_next/image|favicon.ico|health$).*)"],
};
