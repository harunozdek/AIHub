"use server";

import { changePassword, type FieldErrors } from "@/lib/api-client";
import { saveToken } from "@/lib/auth/session";

export type ChangePasswordState = { errors?: FieldErrors; failed?: boolean; done?: boolean };

/**
 * Kendi şifresini değiştirme (Spec 0004, R-9, AC-9). Backend damgayı yeniler: diğer oturumlar düşer,
 * bu oturum dönen yeni token'la sürer.
 */
export async function sifreDegistirAction(_previous: ChangePasswordState, form: FormData): Promise<ChangePasswordState> {
  const result = await changePassword(
    { currentPassword: String(form.get("currentPassword") ?? ""), newPassword: String(form.get("newPassword") ?? "") },
    "/sifre-degistir",
  );
  if (result.ok) {
    await saveToken(result.data.token, result.data.expiresAt);
    return { done: true };
  }
  if (result.kind === "validation") {
    return { errors: result.errors };
  }
  return { failed: true };
}
