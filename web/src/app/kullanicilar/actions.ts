"use server";

import { revalidatePath } from "next/cache";
import { createUser, setUserActive, type FieldErrors } from "@/lib/api-client";

export type CreateUserState = { errors?: FieldErrors; failed?: boolean; created?: string };

/** Kullanıcı ekleme (Spec 0004, R-7, AC-7). Alan hataları backend'den alan adlarıyla gelir. */
export async function kullaniciEkleAction(_previous: CreateUserState, form: FormData): Promise<CreateUserState> {
  const input = {
    userName: String(form.get("userName") ?? ""),
    displayName: String(form.get("displayName") ?? ""),
    password: String(form.get("password") ?? ""),
  };
  const result = await createUser(input, "/kullanicilar");
  if (result.ok) {
    revalidatePath("/kullanicilar");
    return { created: input.userName };
  }
  if (result.kind === "validation") {
    return { errors: result.errors };
  }
  return { failed: true };
}

export type SetActiveState = { message?: string };

/** Pasifleştir / etkinleştir (Spec 0004, R-8). Kendini ve son aktif kullanıcıyı pasifleştirme backend'de reddedilir. */
export async function aktiflikAction(_previous: SetActiveState, form: FormData): Promise<SetActiveState> {
  const id = String(form.get("id") ?? "");
  const active = form.get("active") === "true";
  const result = await setUserActive(id, active, "/kullanicilar");
  if (result.ok) {
    revalidatePath("/kullanicilar");
    return {};
  }
  if (result.kind === "conflict") {
    return { message: result.message };
  }
  return { message: "İşlem şu anda yapılamıyor. Lütfen biraz sonra yeniden deneyin." };
}
