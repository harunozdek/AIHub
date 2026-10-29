import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ErrorState } from "@/components/error-state";
import { LogLevelBadge } from "@/components/log-level-badge";
import { MetadataView } from "@/components/metadata-view";
import { getLog } from "@/lib/api-client";
import { detailHref, safeListUrl } from "@/lib/filters";
import { LocalTime } from "@/lib/format-time";

export const metadata: Metadata = { title: "AIHub — Log ayrıntısı" };

type Props = {
  params: Promise<{ id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

/** Kaydın tüm alanları (Spec 0002, R-5). Adres paylaşılabilir; boş isteğe bağlı alan "—" görünür (AC-7). */
export default async function LogDetailPage({ params, searchParams }: Props) {
  const { id } = await params;
  const donus = (await searchParams).donus;
  const listUrl = safeListUrl(Array.isArray(donus) ? donus[0] : donus);

  const result = await getLog(id, detailHref(id, listUrl));
  if (!result.ok && result.kind === "notFound") {
    notFound();
  }

  const back = (
    <Link href={listUrl} className="text-blue-700 underline hover:text-blue-900 dark:text-blue-300">
      ← Listeye dön
    </Link>
  );

  if (!result.ok) {
    return (
      <>
        {back}
        <h1 className="mb-4 mt-4 text-xl font-semibold">Log ayrıntısı</h1>
        <ErrorState />
      </>
    );
  }

  const log = result.data;
  const empty = "—";

  return (
    <>
      {back}
      <h1 className="mb-4 mt-4 text-xl font-semibold">Log ayrıntısı</h1>
      <dl className="grid gap-x-6 gap-y-3 md:grid-cols-[12rem_1fr]">
        <Field label="Kayıt kimliği"><code className="break-all">{log.id}</code></Field>
        <Field label="Servis"><span className="break-all">{log.serviceName}</span></Field>
        <Field label="Seviye"><LogLevelBadge level={log.level} /></Field>
        <Field label="Alınma zamanı"><LocalTime iso={log.receivedAt} /></Field>
        <Field label="Servis zamanı">{log.occurredAt ? <LocalTime iso={log.occurredAt} /> : empty}</Field>
        <Field label="Exception türü"><span className="break-all">{log.exceptionType ?? empty}</span></Field>
        <Field label="Mesaj">
          <pre className="whitespace-pre-wrap break-all font-sans">{log.message}</pre>
        </Field>
        <Field label="Yığın izi">
          {log.stackTrace ? (
            <pre className="overflow-x-auto whitespace-pre-wrap break-all rounded bg-zinc-100 p-3 text-sm dark:bg-zinc-900">{log.stackTrace}</pre>
          ) : (
            empty
          )}
        </Field>
        <Field label="Metadata"><MetadataView metadata={log.metadata} /></Field>
      </dl>
    </>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="font-medium text-zinc-700 dark:text-zinc-300">{label}</dt>
      <dd className="min-w-0">{children}</dd>
    </>
  );
}
