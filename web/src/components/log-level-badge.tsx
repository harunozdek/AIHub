/** LogLevel yalnızca renkle değil, her zaman metinle de belirtilir (Spec 0002, AC-10). Renkler AA kontrastındadır. */
const styles: Record<string, string> = {
  Info: "bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-100",
  Warning: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-100",
  Error: "bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-100",
  Critical: "bg-fuchsia-100 text-fuchsia-900 dark:bg-fuchsia-950 dark:text-fuchsia-100",
};

export function LogLevelBadge({ level }: { level: string }) {
  return (
    <span className={`inline-block rounded px-2 py-0.5 text-xs font-semibold ${styles[level] ?? "bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100"}`}>
      {level}
    </span>
  );
}
