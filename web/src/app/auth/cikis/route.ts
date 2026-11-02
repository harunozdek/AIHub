import { NextResponse, type NextRequest } from "next/server";
import { getSession } from "@/lib/auth/session";

/**
 * Çıkış: oturum çerezi silinir, kullanıcı "çıkış yapıldı" bilgisiyle giriş ekranına gider. Sayfalar
 * `no-store` ile döndüğü için geri tuşu log içeriğini önbellekten göstermez (Spec 0004, AC-10).
 */
export async function POST(request: NextRequest) {
  const session = await getSession();
  session.destroy();
  // 303: tarayıcı sonraki adrese GET ile gider.
  return NextResponse.redirect(new URL("/giris?cikis=1", request.url), 303);
}
