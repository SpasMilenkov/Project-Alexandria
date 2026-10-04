import type { MediaFileDto } from "@/api/streaming";

import { RANGE_SIZE } from "@/api/shuffle";
import {
  type ScannedRange,
  alignRangeStart,
  isRangeCovered,
  markRangeScanned,
  rangesToRetain,
  shouldPrefetchRange,
} from "@/utils/player-shuffle-buffer";

import type { Order, RangeBufferHooks, RangeLoader, RangeResult } from "../types";

export interface RangeOrder extends Order {
  prefetch: (position: number) => void;
  evictAround: (position: number) => void;
}

const merge = (ranges: ScannedRange[]): ScannedRange[] =>
  ranges.reduce<ScannedRange[]>(
    (merged, range) => markRangeScanned(merged, range.from, range.to),
    [],
  );

const retainRanges = (
  entries: Map<number, MediaFileDto>,
  scanned: ScannedRange[],
  keep: Set<number>,
): { entries: Map<number, MediaFileDto>; scanned: ScannedRange[] } => {
  const retainedEntries = new Map<number, MediaFileDto>();
  for (const [position, file] of entries) {
    if (keep.has(alignRangeStart(position))) retainedEntries.set(position, file);
  }
  const clipped: ScannedRange[] = [];
  for (const start of keep) {
    for (const range of scanned) {
      const from = Math.max(start, range.from);
      const to = Math.min(start + RANGE_SIZE, range.to);
      if (from < to) clipped.push({ from, to });
    }
  }
  return { entries: retainedEntries, scanned: merge(clipped) };
};

export const createRangeBuffer = (load: RangeLoader, hooks: RangeBufferHooks = {}): RangeOrder => {
  let entries = new Map<number, MediaFileDto>();
  let scanned: ScannedRange[] = [];
  let total = 0;
  let disposed = false;
  const inflight = new Map<string, Promise<void>>();

  const publish = () => {
    hooks.onEntries?.(new Map(entries));
    hooks.onTotal?.(total);
  };

  const apply = (result: RangeResult) => {
    total = result.total;
    for (let position = result.offset; position < result.offset + result.scannedCount; position++) {
      entries.delete(position);
    }
    for (const item of result.items) entries.set(item.position, item.file);
    scanned = markRangeScanned(scanned, result.offset, result.offset + result.scannedCount);
  };

  const fetchRange = (start: number): Promise<void> => {
    const key = String(start);
    const pending = inflight.get(key);
    if (pending) return pending;
    const task = load(start)
      .then((result) => {
        if (disposed) return;
        apply(result);
        publish();
      })
      .finally(() => {
        inflight.delete(key);
      });
    inflight.set(key, task);
    return task;
  };

  const at = (position: number) => entries.get(position) ?? null;

  const locate = (match: (file: MediaFileDto) => boolean): MediaFileDto | null => {
    for (const file of entries.values()) {
      if (match(file)) return file;
    }
    return null;
  };

  const ensure = async (position: number): Promise<MediaFileDto | null> => {
    if (disposed || position < 0) return null;
    if (total > 0 && position >= total) return null;
    const buffered = entries.get(position);
    if (buffered) return buffered;
    const start = alignRangeStart(position);
    if (isRangeCovered(scanned, start, Math.min(start + RANGE_SIZE, total || start + RANGE_SIZE))) {
      return null;
    }
    await fetchRange(start);
    return entries.get(position) ?? null;
  };

  return {
    get total() {
      return total;
    },
    at,
    isScanned: (position) => isRangeCovered(scanned, position, position + 1),
    locate,
    ensure,
    seed: (result) => {
      if (disposed) return;
      apply(result);
      publish();
    },
    prefetch: (position) => {
      if (disposed || total === 0) return;
      const start = alignRangeStart(position);
      const nextStart = shouldPrefetchRange(position, start, scanned, total);
      if (nextStart === null) return;
      void fetchRange(nextStart).catch(() => undefined);
    },
    evictAround: (position) => {
      if (disposed) return;
      const { keep, dropStarts } = rangesToRetain(entries, position);
      if (!dropStarts.length) return;
      const retained = retainRanges(entries, scanned, keep);
      entries = retained.entries;
      scanned = retained.scanned;
      publish();
    },
    dispose: () => {
      disposed = true;
      inflight.clear();
      entries = new Map();
      scanned = [];
      total = 0;
      publish();
      return Promise.resolve();
    },
  };
};
