"use server";

import { redirect } from "next/navigation";
import { login } from "@/lib/api-client";
import { logLoginError } from "@/lib/auth/login-error";
import { safeReturnTo } from "@/lib/auth/return-to";
import { saveToken } from "@/lib/auth/session";

export type LoginState = { error?: "invalid" | "unavailable" };

/**
 * Giriş formu: backend doğrular, token oturum çerezine yazılır ve kullanıcı istediği sayfaya döner
 * (Spec 0004, AC-2). Başarısızlığın türü kullanıcıya söylenmez (R-2); backend'e ulaşılamıyorsa neden
 * yalnızca sunucu loguna yazılır.
 */
export async function girisYapAction(_previous: LoginState, form: FormData): Promise<LoginState> {
  const userName = String(form.get("userName") ?? "");
  const password = String(form.get("password") ?? "");
  const returnTo = safeReturnTo(String(form.get("returnTo") ?? ""));

  let outcome: Awaited<ReturnType<typeof login>>;
  try {
    outcome = await login(userName, password);
    if (outcome.ok) {
      await saveToken(outcome.token, outcome.expiresAt, outcome.displayName);
    }
  } catch (error) {
    // Ör. SESSION_SECRET eksik: kullanıcı 500 değil genel hata görür (0003/CB-2'nin karşılığı).
    logLoginError("giris", error);
    return { error: "unavailable" };
  }

  if (outcome.ok) {
    redirect(returnTo);
  }
  if (outcome.kind === "unavailable") {
    logLoginError("giris", outcome.reason);
  }
  return { error: outcome.kind };
}
