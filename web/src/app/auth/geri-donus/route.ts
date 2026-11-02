import { NextResponse, type NextRequest } from "next/server";
import { logLoginError, loginErrorRedirect } from "@/lib/auth/login-error";
import { appUrl, completeLogin } from "@/lib/auth/oidc";
import { getLoginFlow, getSession, type LoginFlow } from "@/lib/auth/session";

/**
 * Kimlik sağlayıcısından dönüş. İptal veya başarısız giriş log içeriği göstermeyen hata sayfasına,
 * başarılı giriş girişten önce istenen adrese gider (Spec 0002, AC-2). Başarısızlığın nedeni sunucu
 * loguna yazılır (Spec 0003, CB-1).
 */
export async function GET(request: NextRequest) {
  const stored = await getLoginFlow();
  const flow = { ...stored } as Partial<LoginFlow>;
  stored.destroy(); // tek kullanımlık: aynı geri dönüş ikinci kez işlenmez

  const providerError = request.nextUrl.searchParams.get("error");
  if (providerError !== null) {
    const cancelled = providerError === "access_denied";
    // Değer adresten gelir: tırnaklanarak yazılır, satır sonuyla log satırı taklit edilemez.
    if (!cancelled) logLoginError("geri-donus", `sağlayıcı hata döndürdü: ${JSON.stringify(providerError)}`);
    return loginErrorRedirect(request, cancelled ? "iptal" : "hata");
  }

  if (!flow.state || !flow.nonce || !flow.codeVerifier || !flow.returnTo) {
    // Çoğunlukla panel APP_URL'den farklı bir adresten (host veya port) açıldığında olur.
    logLoginError("geri-donus", "giriş akışı çerezi yok (panel APP_URL ile aynı adresten mi açıldı?)");
    return loginErrorRedirect(request, "hata");
  }

  try {
    // Sağlayıcı geri dönüşü APP_URL'e yapar; doğrulama da o adres üzerinden yapılır (proxy arkasında da doğru).
    const callbackUrl = appUrl(`/auth/geri-donus${request.nextUrl.search}`);
    const session = await completeLogin(callbackUrl, flow as LoginFlow);
    const current = await getSession();
    Object.assign(current, session);
    await current.save();
    return NextResponse.redirect(appUrl(flow.returnTo));
  } catch (error) {
    logLoginError("geri-donus", error);
    return loginErrorRedirect(request, "hata");
  }
}
