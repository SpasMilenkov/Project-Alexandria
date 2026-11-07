import { isApiError } from "@/api/errors";
import { logger } from "@/utils/logger";

export interface MicroBatcherOptions<TItem, TResult> {
  lingerMs: number;
  maxInFlight: number;
  maxItems: number;
  maxWeight: number;
  minIntervalMs: number;
  retryDelaysMs: readonly number[];
  // Results line up with the items. An Error in a slot fails only that item.
  send: (items: TItem[]) => Promise<(TResult | Error)[]>;
}

export interface SubmitOptions {
  signal?: AbortSignal;
  weight?: number;
}

interface QueuedItem<TItem, TResult> {
  item: TItem;
  onAbort?: () => void;
  queuedAt: number;
  reject: (reason: unknown) => void;
  resolve: (value: TResult) => void;
  signal?: AbortSignal;
  weight: number;
}

const RETRIABLE_STATUSES = new Set([429, 502, 503, 504]);

// A timeout is left out on purpose: the server may still be working through that batch.
export const isRetriableBatchFailure = (err: unknown): boolean => {
  if (!isApiError(err)) return false;
  if (err.kind === "network") return true;

  return err.status !== undefined && RETRIABLE_STATUSES.has(err.status);
};

const wait = (ms: number) =>
  new Promise<void>((resolve) => {
    setTimeout(resolve, ms);
  });

const cancelled = () => new DOMException("Cancelled", "AbortError");

/**
 * Collects individual requests into paced batches. The minimum interval between sends is what
 * keeps a caller under an API rate limit, however quickly items arrive.
 */
export const createMicroBatcher = <TItem, TResult>(options: MicroBatcherOptions<TItem, TResult>) => {
  const queue: QueuedItem<TItem, TResult>[] = [];

  let inFlight = 0;
  let lastSentAt = Number.NEGATIVE_INFINITY;
  let timer: ReturnType<typeof setTimeout> | undefined = undefined;

  const queuedWeight = () => queue.reduce((total, entry) => total + entry.weight, 0);

  const isFull = () => queue.length >= options.maxItems || queuedWeight() >= options.maxWeight;

  const takeBatch = () => {
    const batch: QueuedItem<TItem, TResult>[] = [];
    let weight = 0;

    while (queue.length > 0 && batch.length < options.maxItems) {
      const [next] = queue;

      // A single item over the limit still goes out, as a batch of its own.
      if (batch.length > 0 && weight + next.weight > options.maxWeight) break;

      batch.push(next);
      queue.shift();
      weight += next.weight;
    }

    return batch;
  };

  const sendWithRetry = async (items: TItem[], attempt = 0): Promise<(TResult | Error)[]> => {
    try {
      return await options.send(items);
    } catch (err) {
      const delay = options.retryDelaysMs[attempt];

      if (delay === undefined || !isRetriableBatchFailure(err)) throw err;

      logger.warn(`Upload batch failed, retrying in ${delay}ms`, err);
      await wait(delay);

      return sendWithRetry(items, attempt + 1);
    }
  };

  const settle = (batch: QueuedItem<TItem, TResult>[], results: (TResult | Error)[]) => {
    batch.forEach((entry, index) => {
      const result = results[index];

      if (result === undefined) {
        entry.reject(new Error("The server did not return a result for this file"));
      } else if (result instanceof Error) {
        entry.reject(result);
      } else {
        entry.resolve(result);
      }
    });
  };

  const dispatch = async (batch: QueuedItem<TItem, TResult>[]) => {
    try {
      const results = await sendWithRetry(batch.map((entry) => entry.item));

      settle(batch, results);
    } catch (err) {
      batch.forEach((entry) => entry.reject(err));
    } finally {
      inFlight -= 1;
      schedule();
    }
  };

  const flush = () => {
    timer = undefined;

    if (queue.length === 0) return;

    const batch = takeBatch();

    // Past this point the server may act on the item, so its result has to be awaited.
    for (const entry of batch) {
      if (entry.onAbort) entry.signal?.removeEventListener("abort", entry.onAbort);
    }

    lastSentAt = Date.now();
    inFlight += 1;

    void dispatch(batch);
    schedule();
  };

  const schedule = () => {
    clearTimeout(timer);
    timer = undefined;

    if (queue.length === 0 || inFlight >= options.maxInFlight) return;

    const earliestByLinger = isFull() ? 0 : queue[0].queuedAt + options.lingerMs;
    const earliest = Math.max(earliestByLinger, lastSentAt + options.minIntervalMs);

    timer = setTimeout(flush, Math.max(earliest - Date.now(), 0));
  };

  const submit = (item: TItem, { signal, weight = 0 }: SubmitOptions = {}) =>
    new Promise<TResult>((resolve, reject) => {
      if (signal?.aborted) {
        reject(cancelled());
        return;
      }

      const entry: QueuedItem<TItem, TResult> = {
        item,
        queuedAt: Date.now(),
        reject,
        resolve,
        signal,
        weight,
      };

      entry.onAbort = () => {
        queue.splice(queue.indexOf(entry), 1);
        reject(cancelled());
        schedule();
      };

      signal?.addEventListener("abort", entry.onAbort, { once: true });

      queue.push(entry);
      schedule();
    });

  return { submit };
};
