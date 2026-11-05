import type { WorkerInMessage, WorkerOutMessage } from "@/workers/blake3.worker";

interface HashJob {
  file: Blob;
  id: string;
  onAbort?: () => void;
  onProgress: (percent: number) => void;
  reject: (reason: unknown) => void;
  resolve: (hash: string) => void;
  signal: AbortSignal;
}

interface PooledWorker {
  job: HashJob | null;
  worker: Worker;
}

// Matches the upload concurrency, so every upload slot can hash without waiting on another.
const POOL_SIZE = 4;

// Workers sitting unused this long are released; the next upload starts them again.
const IDLE_RELEASE_MS = 30_000;

const pool: PooledWorker[] = [];
const queue: HashJob[] = [];

let idleTimer: ReturnType<typeof setTimeout> | undefined = undefined;

const cancelled = () => new DOMException("Cancelled", "AbortError");

const detachJob = (entry: PooledWorker): HashJob | null => {
  const { job } = entry;

  entry.job = null;

  if (job?.onAbort) job.signal.removeEventListener("abort", job.onAbort);

  return job;
};

// Terminating is the only way to stop a worker mid-file, so an interrupted or crashed
// worker leaves the pool instead of being handed the next job.
const retire = (entry: PooledWorker) => {
  entry.worker.terminate();

  const index = pool.indexOf(entry);

  if (index >= 0) pool.splice(index, 1);
};

const scheduleIdleRelease = () => {
  clearTimeout(idleTimer);

  if (queue.length > 0 || pool.some((entry) => entry.job)) return;

  idleTimer = setTimeout(() => {
    for (const entry of [...pool]) retire(entry);
  }, IDLE_RELEASE_MS);
};

const handleMessage = (entry: PooledWorker, data: WorkerOutMessage) => {
  const { job } = entry;

  if (!job || data.id !== job.id) return;

  if (data.type === "progress") {
    job.onProgress(data.percent);
    return;
  }

  detachJob(entry);

  if (data.type === "done") {
    job.resolve(data.hash);
  } else {
    job.reject(new Error(data.message));
  }

  pump();
};

const spawn = (): PooledWorker => {
  const entry: PooledWorker = {
    job: null,
    worker: new Worker(new URL("@/workers/blake3.worker.ts", import.meta.url), {
      type: "module",
    }),
  };

  entry.worker.onmessage = ({ data }: MessageEvent<WorkerOutMessage>) => handleMessage(entry, data);

  entry.worker.onerror = (event) => {
    const job = detachJob(entry);

    retire(entry);
    job?.reject(new Error(event.message || "Hashing worker failed"));
    pump();
  };

  pool.push(entry);

  return entry;
};

const acquire = (): PooledWorker | undefined => {
  const idle = pool.find((entry) => !entry.job);

  if (idle) return idle;
  if (pool.length < POOL_SIZE) return spawn();

  return undefined;
};

const pump = () => {
  while (queue.length > 0) {
    const entry = acquire();

    if (!entry) break;

    const job = queue.shift()!;

    entry.job = job;
    entry.worker.postMessage({ file: job.file, id: job.id } satisfies WorkerInMessage);
  }

  scheduleIdleRelease();
};

const abortJob = (job: HashJob) => {
  const queuedAt = queue.indexOf(job);

  if (queuedAt >= 0) {
    queue.splice(queuedAt, 1);
  } else {
    const entry = pool.find((candidate) => candidate.job === job);

    if (entry) {
      detachJob(entry);
      retire(entry);
    }
  }

  job.reject(cancelled());
  pump();
};

/** BLAKE3 hex digest of `file`, computed on a small shared pool of reusable workers. */
export const hashFile = (
  file: Blob,
  id: string,
  onProgress: (percent: number) => void,
  signal: AbortSignal,
): Promise<string> =>
  new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(cancelled());
      return;
    }

    const job: HashJob = { file, id, onProgress, reject, resolve, signal };

    job.onAbort = () => abortJob(job);
    signal.addEventListener("abort", job.onAbort, { once: true });

    queue.push(job);
    pump();
  });
