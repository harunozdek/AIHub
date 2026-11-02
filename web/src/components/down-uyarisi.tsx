"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { DownApp } from "@/app/durum/down/route";
import { useLocalTime } from "@/lib/format-time";

/** Panel durumu bu aralıkla yeniden okur (Spec 0005, R-7). */
export const POLL_INTERVAL_MS = 15_000;

/**
 * Down uyarısı (Spec 0005, R-7, AC-8, AC-10, AC-12). Oturum açıkken her ekranın üstünde durur:
 * - açılışta ve 15 sn'de bir durumu okur; sayfa yenilenmeden belirir ve kalkar;
 * - okunamazsa son bilinen durumu "durum güncellenemedi" notuyla göstermeyi sürdürür;
 * - belirmesi ekran okuyucuya duyurulur (role="alert").
 */
export function DownUyarisi() {
  const [down, setDown] = useState<DownApp[]>([]);
  const [stale, setStale] = useState(false);

  useEffect(() => {
    let active = true;
    const read = async () => {
      try {
        const response = await fetch("/durum/down", { cache: "no-store", redirect: "manual" });
        if (!response.ok) throw new Error(String(response.status));
        const body = (await response.json()) as { down: DownApp[] };
        if (active) {
          setDown(body.down);
          setStale(false);
        }
      } catch {
        if (active) setStale(true);
      }
    };
    void read();
    const timer = setInterval(read, POLL_INTERVAL_MS);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, []);

  if (down.length === 0) {
    return null;
  }

  return (
    <div role="alert" className="border-b border-red-300 bg-red-50 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100">
      <div className="mx-auto max-w-7xl px-4 py-3 text-sm">
        <p className="font-semibold">
          {down.length === 1 ? "Bir uygulama Down" : `${down.length} uygulama Down`}
        </p>
        <ul className="mt-1 flex flex-wrap gap-x-6 gap-y-1">
          {down.map((app) => (
            <DownSatiri key={app.serviceName} app={app} />
          ))}
        </ul>
        {stale && <p className="mt-1 text-xs">Durum güncellenemedi; son bilinen durum gösteriliyor.</p>}
      </div>
    </div>
  );
}

function DownSatiri({ app }: { app: DownApp }) {
  return (
    <li>
      <Link href="/izlenen-uygulamalar" className="font-mono underline hover:no-underline">
        {app.serviceName}
      </Link>
      {app.downSince && <DownZamani iso={app.downSince} />}
    </li>
  );
}

function DownZamani({ iso }: { iso: string }) {
  const text = useLocalTime(iso);
  return (
    <span>
      {" "}— <time dateTime={iso}>{text}</time> zamanından beri
    </span>
  );
}
