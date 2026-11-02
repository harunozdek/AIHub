import type { HealthFailureReason, HealthStatus } from "@/lib/api-client";

/** Durum metinle yazılır, yalnızca renkle anlatılmaz (Spec 0005, Erişilebilirlik). */
export function statusText(status: HealthStatus): string {
  return status === "Up" ? "Ayakta" : status === "Down" ? "Down" : "Bilinmiyor";
}

/** Son başarısızlığın anlaşılır nedeni (Spec 0005, R-6). Yanıt gövdesi ya da iç ayrıntı gösterilmez. */
export function failureText(reason: HealthFailureReason | null, statusCode: number | null): string {
  switch (reason) {
    case null:
      return "—";
    case "Timeout":
      return "Zaman aşımı";
    case "ConnectionFailed":
      return "Bağlantı kurulamadı";
    case "CertificateError":
      return "Sertifika hatası";
    case "HttpStatus":
      return statusCode ? `HTTP ${statusCode}` : "HTTP hatası";
    case "Redirect":
      return "Yönlendirme";
    case "BlockedAddress":
      return "Engellenen adres";
  }
}
