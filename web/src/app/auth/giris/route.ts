import { NextResponse, type NextRequest } from "next/server";
import { appUrl, beginLogin } from "@/lib/auth/oidc";
import { getLoginFlow } from "@/lib/auth/session";

/** Giriş akışını başlatır: dönüş adresi saklanır, kullanıcı kimlik sağlayıcısına gönderilir (Spec 0002, AC-2). */
export async function GET(request: NextRequest) {
  try {
    const { url, flow } = await beginLogin(request.nextUrl.searchParams.get("returnTo") ?? "/logs");
    const stored = await getLoginFlow();
    Object.assign(stored, flow);
    await stored.save();
    return NextResponse.redirect(url);
  } catch {
    // Sağlayıcıya ulaşılamıyor: iç ayrıntı gösterilmez (R-9).
    return NextResponse.redirect(appUrl("/giris-hatasi?neden=hata"));
  }
}
