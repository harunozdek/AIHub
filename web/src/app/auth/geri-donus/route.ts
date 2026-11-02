import { NextResponse, type NextRequest } from "next/server";
import { appUrl, completeLogin } from "@/lib/auth/oidc";
import { getLoginFlow, getSession, type LoginFlow } from "@/lib/auth/session";

/**
 * Kimlik sağlayıcısından dönüş. İptal veya başarısız giriş log içeriği göstermeyen hata sayfasına,
 * başarılı giriş girişten önce istenen adrese gider (Spec 0002, AC-2).
 */
export async function GET(request: NextRequest) {
  const stored = await getLoginFlow();
  const flow = { ...stored } as Partial<LoginFlow>;
  stored.destroy(); // tek kullanımlık: aynı geri dönüş ikinci kez işlenmez

  if (request.nextUrl.searchParams.has("error")) {
    const reason = request.nextUrl.searchParams.get("error") === "access_denied" ? "iptal" : "hata";
    return NextResponse.redirect(appUrl(`/giris-hatasi?neden=${reason}`));
  }

  if (!flow.state || !flow.nonce || !flow.codeVerifier || !flow.returnTo) {
    return NextResponse.redirect(appUrl("/giris-hatasi?neden=hata"));
  }

  try {
    // Sağlayıcı geri dönüşü APP_URL'e yapar; doğrulama da o adres üzerinden yapılır (proxy arkasında da doğru).
    const callbackUrl = appUrl(`/auth/geri-donus${request.nextUrl.search}`);
    const session = await completeLogin(callbackUrl, flow as LoginFlow);
    const current = await getSession();
    Object.assign(current, session);
    await current.save();
    return NextResponse.redirect(appUrl(flow.returnTo));
  } catch {
    return NextResponse.redirect(appUrl("/giris-hatasi?neden=hata"));
  }
}
