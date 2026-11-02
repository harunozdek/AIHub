"use client";

import Link from "next/link";
import type { LogSummary } from "@/lib/api-client";
import { detailHref } from "@/lib/filters";
import { useLocalTime } from "@/lib/format-time";
import { LogLevelBadge } from "./log-level-badge";

/**
 * Liste tablosu. Log içeriği React metin düğümü olarak çizilir; hiçbir alan HTML olarak yorumlanmaz
 * (Spec 0002, R-6). Satır seçimi bağlantıdır: klavyeyle Tab ve Enter ile açılır (AC-10).
 */
export function LogTable({ items, listUrl }: { items: LogSummary[]; listUrl: string }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-left text-sm">
        <caption className="sr-only">Log kayıtları, en yeni önce</caption>
        <thead className="border-b border-zinc-300 dark:border-zinc-700">
          <tr>
            <th scope="col" className="px-3 py-2 font-semibold">Alınma zamanı</th>
            <th scope="col" className="px-3 py-2 font-semibold">Servis</th>
            <th scope="col" className="px-3 py-2 font-semibold">Seviye</th>
            <th scope="col" className="px-3 py-2 font-semibold">Mesaj</th>
            <th scope="col" className="px-3 py-2 font-semibold">Exception</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <Row key={item.id} item={item} href={detailHref(item.id, listUrl)} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Row({ item, href }: { item: LogSummary; href: string }) {
  const time = useLocalTime(item.receivedAt);

  return (
    <tr className="border-b border-zinc-200 align-top hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-900">
      <td className="whitespace-nowrap px-3 py-2">
        <Link href={href} className="text-blue-700 underline hover:text-blue-900 dark:text-blue-300">
          <time dateTime={item.receivedAt}>{time}</time>
        </Link>
      </td>
      <td className="px-3 py-2 break-all">{item.serviceName}</td>
      <td className="px-3 py-2"><LogLevelBadge level={item.level} /></td>
      <td className="px-3 py-2 break-all">
        {item.messagePreview}
        {item.messageTruncated && <span className="whitespace-nowrap text-zinc-600 dark:text-zinc-400"> … (kısaltıldı)</span>}
      </td>
      <td className="px-3 py-2 break-all">{item.exceptionType ?? ""}</td>
    </tr>
  );
}
