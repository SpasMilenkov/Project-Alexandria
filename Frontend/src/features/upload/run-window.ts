import { logger } from "@/utils/logger";

export interface WindowOptions<T> {
  // Admitted regardless of the byte limit, so a few large files still run side by side.
  alwaysAdmit: number;
  maxBytes: number;
  maxItems: number;
  shouldStop?: () => boolean;
  sizeOf: (item: T) => number;
}

/**
 * Runs one task per item, in order, while the running tasks stay inside the limits. A file holds
 * its place from hashing through finalize, and a signed URL can wait on the files ahead of it,
 * so this also bounds how long a URL can sit unused.
 */
export const runWindow = <T>(
  items: readonly T[],
  run: (item: T) => Promise<unknown>,
  { alwaysAdmit, maxBytes, maxItems, shouldStop = () => false, sizeOf }: WindowOptions<T>,
): Promise<void> =>
  new Promise((resolve) => {
    let cursor = 0;
    let active = 0;
    let activeBytes = 0;

    const fits = (size: number) => {
      if (active < alwaysAdmit) return true;

      return active < maxItems && activeBytes + size <= maxBytes;
    };

    const start = async (item: T, size: number) => {
      try {
        await run(item);
      } catch (err) {
        // Tasks report their own failures, so one reaching here is a bug in the task.
        logger.error("An upload task failed unexpectedly", err);
      }

      active -= 1;
      activeBytes -= size;
      pump();
    };

    const pump = () => {
      while (cursor < items.length && !shouldStop()) {
        const item = items[cursor];
        const size = sizeOf(item);

        if (!fits(size)) break;

        cursor += 1;
        active += 1;
        activeBytes += size;

        void start(item, size);
      }

      if (active === 0) resolve();
    };

    pump();
  });
