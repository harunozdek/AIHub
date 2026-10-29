import { NextResponse } from "next/server";
import { logoutUrl } from "@/lib/auth/oidc";
import { getSession } from "@/lib/auth/session";

/**
 * Çıkış: oturum çerezi silinir, ardından sağlayıcı oturumu da kapatılır. Sayfalar `no-store` ile
 * döndüğü için geri tuşu log içeriğini önbellekten göstermez (Spec 0002, AC-2).
 */
export async function POST() {
  const session = await getSession();
  const idToken = session.idToken;
  session.destroy();
  // 303: tarayıcı sonraki adrese GET ile gider.
  return NextResponse.redirect(await logoutUrl(idToken), 303);
}
