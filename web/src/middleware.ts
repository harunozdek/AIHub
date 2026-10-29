import { NextResponse, type NextRequest } from "next/server";
import { refresh } from "@/lib/auth/oidc";
import { SESSION_COOKIE, sealSession, sessionCookieOptions, unsealSession } from "@/lib/auth/session";

/** Bitişine bu kadar kalan token önceden yenilenir; istek ortasında süresi dolmaz. */
const REFRESH_MARGIN_SECONDS = 30;

/**
 * Oturumsuz hiçbir istek panel içeriğine ulaşmaz (Spec 0002, R-1, AC-1). Süresi dolan oturum
 * yenilenir; yenilenemezse kullanıcı girişe, girişten sonra da aynı adrese döner (AC-2).
 */
export async function middleware(request: NextRequest) {
  const session = await unsealSession(request.cookies.get(SESSION_COOKIE)?.value).catch(() => null);
  const now = Math.floor(Date.now() / 1000);

  if (session && session.expiresAt - REFRESH_MARGIN_SECONDS > now) {
    return withNoStore(NextResponse.next());
  }

  const renewed = session ? await refresh(session) : null;
  if (!renewed) {
    const login = new URL("/auth/giris", request.url);
    login.searchParams.set("returnTo", request.nextUrl.pathname + request.nextUrl.search);
    const response = NextResponse.redirect(login);
    response.cookies.delete(SESSION_COOKIE);
    return response;
  }

  // Yenilenen çerez hem bu isteğin sunucu bileşenlerine hem tarayıcıya iletilir.
  const sealed = await sealSession(renewed);
  request.cookies.set(SESSION_COOKIE, sealed);
  const response = NextResponse.next({ request: { headers: request.headers } });
  response.cookies.set(SESSION_COOKIE, sealed, sessionCookieOptions);
  return withNoStore(response);
}

// Log içeriği taşıyan yanıtlar tarayıcı önbelleğine yazılmaz: çıkıştan sonra geri tuşu içerik göstermez.
function withNoStore(response: NextResponse) {
  response.headers.set("Cache-Control", "no-store");
  return response;
}

export const config = {
  // Giriş akışı, giriş hatası sayfası ve statik dosyalar oturum istemez.
  matcher: ["/((?!auth/|giris-hatasi|_next/static|_next/image|favicon.ico).*)"],
};
