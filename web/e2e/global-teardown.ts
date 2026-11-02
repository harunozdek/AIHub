import type { ChildProcess } from "node:child_process";
import { rmSync } from "node:fs";
import type { StartedTestContainer } from "testcontainers";

/** Setup'ın başlattığı her şey; setup ve teardown Playwright'ın ana sürecinde aynı nesneyi görür. */
export type Running = { processes: ChildProcess[]; containers: StartedTestContainer[]; dirs: string[] };

const holder = globalThis as typeof globalThis & { __aihubE2e?: Running };

export function running(): Running {
  holder.__aihubE2e ??= { processes: [], containers: [], dirs: [] };
  return holder.__aihubE2e;
}

/** Hata ve zaman aşımı dahil hiçbir şey arkada kalmaz (docs/testing.md, Resource hygiene). */
export async function stopAll(): Promise<void> {
  const { processes, containers, dirs } = running();
  for (const child of processes.reverse()) {
    if (child.exitCode === null && child.signalCode === null) {
      const exited = new Promise((resolve) => child.once("exit", resolve));
      child.kill();
      await Promise.race([exited, new Promise((resolve) => setTimeout(resolve, 10_000))]);
      if (child.exitCode === null && child.signalCode === null) child.kill("SIGKILL");
    }
  }
  processes.length = 0;
  await Promise.allSettled(containers.reverse().map((c) => c.stop({ timeout: 10_000 })));
  containers.length = 0;
  // Süreçler durduktan sonra: Windows'ta açık dosya silinemez.
  for (const dir of dirs) rmSync(dir, { recursive: true, force: true });
  dirs.length = 0;
}

export default async function globalTeardown() {
  await stopAll();
}
