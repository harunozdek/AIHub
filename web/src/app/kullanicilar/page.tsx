import type { Metadata } from "next";
import { listUsers } from "@/lib/api-client";
import { getSession } from "@/lib/auth/session";
import { LocalTime } from "@/lib/format-time";
import { AktiflikDugmesi } from "./aktiflik-dugmesi";
import { KullaniciEkleFormu } from "./kullanici-ekle-formu";

export const metadata: Metadata = { title: "AIHub — Kullanıcılar" };

/**
 * Kullanıcılar ekranı (Spec 0004, R-7, R-8, R-11, AC-11): liste, ekleme ve pasifleştir/etkinleştir.
 * Pasif kullanıcının durum hücresi pasifleştireni ve zamanını da gösterir.
 * Kullanıcı kendi satırında pasifleştirme görmez; kural ayrıca backend'de uygulanır.
 */
export default async function KullanicilarPage() {
  const [result, session] = await Promise.all([listUsers("/kullanicilar"), getSession()]);
  if (!result.ok) {
    // Hata sınırı "yeniden dene" gösterir (error.tsx).
    throw new Error("Kullanıcılar okunamadı.");
  }

  return (
    <>
      <h1 className="mb-4 text-xl font-semibold">Kullanıcılar</h1>
      <KullaniciEkleFormu />
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-left text-sm">
          <caption className="sr-only">Panel kullanıcıları</caption>
          <thead className="border-b border-zinc-300 dark:border-zinc-700">
            <tr>
              <th scope="col" className="px-3 py-2 font-semibold">Kullanıcı adı</th>
              <th scope="col" className="px-3 py-2 font-semibold">Görünen ad</th>
              <th scope="col" className="px-3 py-2 font-semibold">Durum</th>
              <th scope="col" className="px-3 py-2 font-semibold">Son giriş</th>
              <th scope="col" className="px-3 py-2 font-semibold">Ekleyen</th>
              <th scope="col" className="px-3 py-2 font-semibold">Eklenme</th>
              <th scope="col" className="px-3 py-2 font-semibold"><span className="sr-only">İşlem</span></th>
            </tr>
          </thead>
          <tbody>
            {result.data.map((user) => (
              <tr key={user.id} className="border-b border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2 font-mono">{user.userName}</td>
                <td className="px-3 py-2">{user.displayName}</td>
                <td className="px-3 py-2">
                  {user.isActive ? (
                    "Aktif"
                  ) : (
                    <>
                      Pasif
                      {/* R-11: kim tarafından ve ne zaman pasifleştirildiği. */}
                      {user.deactivatedAt && (
                        <span className="block text-xs text-zinc-600 dark:text-zinc-400">
                          {user.deactivatedBy ?? "—"}, <LocalTime iso={user.deactivatedAt} />
                        </span>
                      )}
                    </>
                  )}
                </td>
                <td className="px-3 py-2">{user.lastLoginAt ? <LocalTime iso={user.lastLoginAt} /> : "—"}</td>
                <td className="px-3 py-2">{user.createdBy ?? "Kurulum"}</td>
                <td className="px-3 py-2"><LocalTime iso={user.createdAt} /></td>
                <td className="px-3 py-2">
                  {user.id === session.userId ? (
                    <span className="text-xs text-zinc-600 dark:text-zinc-400">Siz</span>
                  ) : (
                    // Aktiflik değişince bileşen yeniden kurulur: onay adımı kendiliğinden kapanır.
                    <AktiflikDugmesi key={`${user.id}-${user.isActive}`} id={user.id} userName={user.userName} active={user.isActive} />
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
