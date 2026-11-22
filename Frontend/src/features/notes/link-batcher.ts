interface Waiter<V> {
  resolve: (value: V) => void;
  reject: (reason: unknown) => void;
}

interface PendingKey<K, V> {
  key: K;
  waiters: Waiter<V>[];
}

export interface BatcherOptions<K, V> {
  // Identity of a key, and of the value answering it.
  keyOf: (key: K) => string;
  valueKeyOf: (value: V) => string;
  loadMany: (keys: K[]) => Promise<V[]>;
  maxBatch: number;
  // How long to wait for more keys before sending, so chips mounting together share a request.
  waitMs: number;
}

const chunk = <T>(items: T[], size: number): T[][] => {
  const chunks: T[][] = [];

  for (let start = 0; start < items.length; start += size) {
    chunks.push(items.slice(start, start + size));
  }

  return chunks;
};

/**
 * Collects single-key loads made close together into batched requests. Each distinct key is
 * sent once per batch; a key the response leaves out rejects only its own callers.
 */
export const createBatcher = <K, V>({
  keyOf,
  valueKeyOf,
  loadMany,
  maxBatch,
  waitMs,
}: BatcherOptions<K, V>) => {
  let pending = new Map<string, PendingKey<K, V>>();
  let timer: ReturnType<typeof setTimeout> | null = null;

  const settle = async (entries: PendingKey<K, V>[]) => {
    try {
      const values = await loadMany(entries.map((entry) => entry.key));
      const byKey = new Map(values.map((value) => [valueKeyOf(value), value]));

      for (const entry of entries) {
        const value = byKey.get(keyOf(entry.key));

        for (const waiter of entry.waiters) {
          if (value === undefined) waiter.reject(new Error("The response left this key out."));
          else waiter.resolve(value);
        }
      }
    } catch (error) {
      for (const entry of entries) {
        for (const waiter of entry.waiters) waiter.reject(error);
      }
    }
  };

  const flush = () => {
    timer = null;

    const batch = [...pending.values()];
    pending = new Map();

    for (const entries of chunk(batch, maxBatch)) void settle(entries);
  };

  const load = (key: K): Promise<V> =>
    new Promise<V>((resolve, reject) => {
      const id = keyOf(key);
      const existing = pending.get(id);

      if (existing) existing.waiters.push({ reject, resolve });
      else pending.set(id, { key, waiters: [{ reject, resolve }] });

      timer ??= setTimeout(flush, waitMs);
    });

  return { load };
};
