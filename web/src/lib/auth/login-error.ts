import { NextResponse, type NextRequest } from "next/server";
import { appUrl } from "./oidc";

/**
 * Giriş hatasının nedeni yalnızca sunucu sürecinin loguna yazılır; kullanıcı iç ayrıntı görmez
 * (Spec 0003, CB-1; Spec 0002, R-9). Yalnızca hatanın adı, mesajı ve kodu yazılır: istek adresi,
 * yetkilendirme kodu, state, token ve çerez hiçbir zaman loga girmez.
 */
export function logLoginError(context: "giris" | "geri-donus", reason: unknown): void {
  console.error(`Giriş hatası (${context}): ${describe(reason)}`);
}

function describe(reason: unknown): string {
  if (!(reason instanceof Error)) return String(reason);
  const code = (reason as { code?: unknown }).code;
  // openid-client asıl nedeni `cause`'a koyar ("invalid response encountered" tek başına teşhis
  // ettirmez). Yalnızca mesajı yazılır: nesnenin `parameters`/`expected` alanları state taşır (review 1, R1-03).
  const cause = reason.cause instanceof Error ? ` — neden: ${reason.cause.message}` : "";
  return `${reason.name}: ${reason.message}${typeof code === "string" ? ` [${code}]` : ""}${cause}`;
}

/** Hata sayfasına yönlendirir; APP_URL tanımsızken de 500 vermez, isteğin kendi adresini kullanır (CB-2). */
export function loginErrorRedirect(request: NextRequest, reason: "hata" | "iptal"): NextResponse {
  const path = `/giris-hatasi?neden=${reason}`;
  let target: URL;
  try {
    target = appUrl(path);
  } catch {
    target = new URL(path, request.url);
  }
  return NextResponse.redirect(target);
}
