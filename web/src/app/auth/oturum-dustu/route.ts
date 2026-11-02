import { NextResponse, type NextRequest } from "next/server";
import { safeReturnTo } from "@/lib/auth/return-to";
import { getSession } from "@/lib/auth/session";

/**
 * Backend oturumu reddetti (ör. kullanıcı pasifleştirildi veya şifresi başka oturumda değişti): çerez
 * silinir, kullanıcı aynı adrese dönüş bilgisiyle giriş ekranına gider (Spec 0004, R-8, R-9; review 1,
 * F-06). Çerez silinmezse giriş ekranı oturum açıkmış gibi menüyü gösterirdi. GET'tir: sayfa render'ı
 * ve server action'lar buraya yalnızca `redirect` ile gelebilir. Yan etkisi yalnızca kendi oturumunu
 * kapatmaktır.
 */
export async function GET(request: NextRequest) {
  const session = await getSession();
  session.destroy();
  const login = new URL("/giris", request.url);
  login.searchParams.set("returnTo", safeReturnTo(request.nextUrl.searchParams.get("returnTo")));
  return NextResponse.redirect(login, 303);
}
