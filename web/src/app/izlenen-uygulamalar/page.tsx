import type { Metadata } from "next";
import { listMonitoredApps } from "@/lib/api-client";
import { LocalTime } from "@/lib/format-time";
import { failureText, statusText } from "@/lib/health";
import { SatirIslemleri } from "./satir-islemleri";
import { UygulamaFormu } from "./uygulama-formu";

export const metadata: Metadata = { title: "AIHub — İzlenen uygulamalar" };

/**
 * "İzlenen uygulamalar" ekranı (Spec 0005, R-2, R-8, AC-10): ekleme, liste, düzenleme ve izlemeden
 * çıkarma. Down satırları backend sırasıyla en üsttedir ve ayrıca vurgulanır; durum metinle de yazılır.
 */
export default async function IzlenenUygulamalarPage() {
  const result = await listMonitoredApps("/izlenen-uygulamalar");
  if (!result.ok) {
    // Hata sınırı "yeniden dene" gösterir (error.tsx).
    throw new Error("İzlenen uygulamalar okunamadı.");
  }

  return (
    <>
      <h1 className="mb-4 text-xl font-semibold">İzlenen uygulamalar</h1>
      <UygulamaFormu mode="add" />
      {result.data.length === 0 ? (
        <div role="status" className="rounded border border-dashed border-zinc-300 p-10 text-center dark:border-zinc-700">
          <p className="font-medium">Henüz izlenen uygulama yok</p>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
            Yukarıdaki &ldquo;Uygulama ekle&rdquo; formuyla bir ServiceName ve sağlık adresi girin.
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left text-sm">
            <caption className="sr-only">İzlenen uygulamalar ve sağlık durumları</caption>
            <thead className="border-b border-zinc-300 dark:border-zinc-700">
              <tr>
                {["ServiceName", "Sağlık adresi", "Durum", "Son denetim", "Son başarısızlık", "Down olduğu zaman", "Ekleyen", "Son değiştiren"].map((baslik) => (
                  <th key={baslik} scope="col" className="px-3 py-2 font-semibold">{baslik}</th>
                ))}
                <th scope="col" className="px-3 py-2 font-semibold"><span className="sr-only">İşlem</span></th>
              </tr>
            </thead>
            <tbody>
              {result.data.map((app) => {
                const down = app.status === "Down";
                return (
                  <tr
                    key={app.id}
                    className={down
                      ? "border-b border-red-200 bg-red-50 dark:border-red-900 dark:bg-red-950"
                      : "border-b border-zinc-200 dark:border-zinc-800"}
                  >
                    <td className="px-3 py-2 font-mono">{app.serviceName}</td>
                    <td className="max-w-xs break-all px-3 py-2 font-mono text-xs">{app.healthUrl}</td>
                    <td className={down ? "px-3 py-2 font-semibold text-red-800 dark:text-red-200" : "px-3 py-2"}>{statusText(app.status)}</td>
                    <td className="px-3 py-2">{app.lastCheckedAt ? <LocalTime iso={app.lastCheckedAt} /> : "—"}</td>
                    <td className="px-3 py-2">{failureText(app.lastFailureReason, app.lastFailureStatusCode)}</td>
                    <td className="px-3 py-2">{app.downSince ? <LocalTime iso={app.downSince} /> : "—"}</td>
                    <td className="px-3 py-2">
                      {app.createdBy ?? "—"}, <LocalTime iso={app.createdAt} />
                    </td>
                    <td className="px-3 py-2">
                      {app.updatedAt ? <>{app.updatedBy ?? "—"}, <LocalTime iso={app.updatedAt} /></> : "—"}
                    </td>
                    <td className="px-3 py-2">
                      {/* Adres değişince bileşen yeniden kurulur: düzenleme formu kendiliğinden kapanır. */}
                      <SatirIslemleri key={`${app.id}-${app.healthUrl}-${app.updatedAt ?? ""}`} id={app.id} serviceName={app.serviceName} healthUrl={app.healthUrl} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
