import { NextResponse } from "next/server";
import { getSession } from "@/lib/auth/session";
import type { MonitoredApp } from "@/lib/api-client";
import { ekranUygulamasi } from "@/lib/health";

/**
 * "İzlenen uygulamalar" ekranının 10 sn'de bir yokladığı uç (Spec 0012, CB-6). `/durum/down` gibi BFF'tir: istek panelin
 * sunucusunda, oturum çerezindeki token'la yapılır. Yönlendirme yapmaz; oturum düşmüşse 401, backend okunamazsa 503
 * döner ve ekran son bilinen hâli "Durum güncellenemedi" notuyla gösterir. Yalnızca ekranın gösterdiği alanlar döner
 * (CB-3: ekleyen ve son değiştiren yok). Sıra backend'inkidir: Down'lar önce, en uzun süredir Down olan üstte.
 */
export async function GET() {
  const session = await getSession();
  if (!session.accessToken) {
    return NextResponse.json({ error: "oturum yok" }, { status: 401 });
  }

  let response: Response;
  try {
    response = await fetch(new URL("/api/monitored-apps", process.env.API_BASE_URL), {
      headers: { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" },
      cache: "no-store",
    });
  } catch {
    return NextResponse.json({ error: "durum okunamadı" }, { status: 503 });
  }

  if (!response.ok) {
    const status = response.status === 401 ? 401 : 503;
    return NextResponse.json({ error: "durum okunamadı" }, { status });
  }

  const apps = (await response.json()) as MonitoredApp[];
  return NextResponse.json({ uygulamalar: apps.map(ekranUygulamasi) }, { headers: { "Cache-Control": "no-store" } });
}
