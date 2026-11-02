/**
 * Giriş hatasının nedeni yalnızca sunucu sürecinin loguna yazılır; kullanıcı iç ayrıntı görmez
 * (Spec 0004 ile korunan 0003/CB-1 ve 0002/R-9). Yalnızca hatanın adı, mesajı, kodu ve asıl nedeni yazılır:
 * kullanıcı adı, şifre, token ve çerez hiçbir zaman loga girmez.
 */
export function logLoginError(context: "giris" | "sifre", reason: unknown): void {
  console.error(`Giriş hatası (${context}): ${describe(reason)}`);
}

function describe(reason: unknown): string {
  if (!(reason instanceof Error)) return String(reason);
  const code = (reason as { code?: unknown }).code;
  const cause = reason.cause instanceof Error && reason.cause.message ? ` — neden: ${reason.cause.message}` : "";
  return `${reason.name}: ${reason.message}${typeof code === "string" ? ` [${code}]` : ""}${cause}`;
}
