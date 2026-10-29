/**
 * Metadata okunur ve girintili gösterilir (Spec 0002, R-5). JSON metni bir React metin düğümüdür;
 * içindeki hiçbir değer HTML olarak yorumlanmaz (R-6).
 */
export function MetadataView({ metadata }: { metadata: unknown }) {
  if (metadata === null || metadata === undefined) {
    return <>—</>;
  }
  return (
    <pre className="overflow-x-auto whitespace-pre-wrap break-all rounded bg-zinc-100 p-3 text-sm dark:bg-zinc-900">
      {JSON.stringify(metadata, null, 2)}
    </pre>
  );
}
