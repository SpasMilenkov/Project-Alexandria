import type { MediaFileDto } from "@/api/streaming";

export const SHUFFLE_RANGE_SIZE = 50;
export const SHUFFLE_MAX_RANGES = 10;
export const SHUFFLE_PREFETCH_DISTANCE = 5;

export interface ScannedRange {
  from: number;
  to: number;
}

export const alignRangeStart = (position: number): number =>
  Math.floor(Math.max(0, position) / SHUFFLE_RANGE_SIZE) * SHUFFLE_RANGE_SIZE;

export const rangeKey = (start: number, sessionId: string): string => `${sessionId}:${start}`;

export const isRangeCovered = (scanned: ScannedRange[], from: number, to: number): boolean =>
  scanned.some((r) => r.from <= from && r.to >= to);

export const markRangeScanned = (
  scanned: ScannedRange[],
  from: number,
  to: number,
): ScannedRange[] => {
  const merged: ScannedRange[] = [];
  let next = { from, to };
  const ordered = [...scanned, next].sort((a, b) => a.from - b.from);
  for (const range of ordered) {
    const last = merged[merged.length - 1];
    if (last && range.from <= last.to) {
      last.to = Math.max(last.to, range.to);
    } else {
      const fresh = { from: range.from, to: range.to };
      merged.push(fresh);
      next = fresh;
    }
  }
  return merged;
};

export const nextBufferedPosition = (
  entries: Map<number, MediaFileDto>,
  from: number,
  totalCount: number,
): number | null => {
  for (let pos = from; pos < totalCount; pos++) {
    if (entries.has(pos)) return pos;
  }
  return null;
};

export const previousBufferedPosition = (
  entries: Map<number, MediaFileDto>,
  from: number,
): number | null => {
  for (let pos = from; pos >= 0; pos--) {
    if (entries.has(pos)) return pos;
  }
  return null;
};

export const rangesToRetain = (
  entries: Map<number, MediaFileDto>,
  around: number,
  maxRanges: number = SHUFFLE_MAX_RANGES,
): { keep: Set<number>; dropStarts: number[] } => {
  const starts = new Set<number>();
  for (const position of entries.keys()) starts.add(alignRangeStart(position));
  const ordered = [...starts].sort((a, b) => Math.abs(a - around) - Math.abs(b - around) || a - b);
  const keep = new Set(ordered.slice(0, maxRanges));
  const dropStarts = ordered.slice(maxRanges);
  return { keep, dropStarts };
};

export const shouldPrefetchRange = (
  position: number,
  rangeStart: number,
  scanned: ScannedRange[],
  totalCount: number,
): number | null => {
  const nextStart = rangeStart + SHUFFLE_RANGE_SIZE;
  if (nextStart >= totalCount) return null;
  if (position < nextStart - SHUFFLE_PREFETCH_DISTANCE) return null;
  if (isRangeCovered(scanned, nextStart, Math.min(nextStart + SHUFFLE_RANGE_SIZE, totalCount))) {
    return null;
  }
  return nextStart;
};

export const isNearBottom = (
  scrollTop: number,
  clientHeight: number,
  scrollHeight: number,
  threshold = 200,
): boolean => scrollTop + clientHeight > scrollHeight - threshold;
