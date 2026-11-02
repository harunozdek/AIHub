"use server";

import { revalidatePath } from "next/cache";
import { addMonitoredApp, removeMonitoredApp, updateMonitoredApp, type FieldErrors } from "@/lib/api-client";

const PAGE = "/izlenen-uygulamalar";

export type AppFormState = { errors?: FieldErrors; message?: string; saved?: string };

/** Uygulama ekleme (Spec 0005, R-1–R-3, AC-2, AC-3, AC-11). Alan hataları backend'den alan adlarıyla gelir. */
export async function uygulamaEkleAction(_previous: AppFormState, form: FormData): Promise<AppFormState> {
  const input = {
    serviceName: String(form.get("serviceName") ?? ""),
    healthUrl: String(form.get("healthUrl") ?? ""),
  };
  const result = await addMonitoredApp(input, PAGE);
  if (result.ok) {
    revalidatePath(PAGE);
    return { saved: input.serviceName.trim() };
  }
  return failure(result);
}

/** Sağlık adresinin değişimi (Spec 0005, AC-9): durum Bilinmiyor'a döner. */
export async function uygulamaDuzenleAction(_previous: AppFormState, form: FormData): Promise<AppFormState> {
  const id = String(form.get("id") ?? "");
  const result = await updateMonitoredApp(id, String(form.get("healthUrl") ?? ""), PAGE);
  if (result.ok) {
    revalidatePath(PAGE);
    return { saved: id };
  }
  return failure(result);
}

export type RemoveState = { message?: string };

/** İzlemeden çıkarma (Spec 0005, AC-9). Zaten çıkarılmışsa liste yenilenir. */
export async function uygulamaCikarAction(_previous: RemoveState, form: FormData): Promise<RemoveState> {
  const result = await removeMonitoredApp(String(form.get("id") ?? ""), PAGE);
  if (result.ok || result.kind === "notFound") {
    revalidatePath(PAGE);
    return {};
  }
  return { message: "İşlem şu anda yapılamıyor. Lütfen biraz sonra yeniden deneyin." };
}

function failure(result: { kind: string; errors?: FieldErrors; message?: string }): AppFormState {
  if (result.kind === "validation") {
    return { errors: result.errors };
  }
  if (result.kind === "conflict") {
    return { message: result.message };
  }
  if (result.kind === "notFound") {
    revalidatePath(PAGE);
    return { message: "Uygulama artık izlenmiyor." };
  }
  return { message: "İşlem şu anda yapılamıyor. Lütfen biraz sonra yeniden deneyin." };
}
