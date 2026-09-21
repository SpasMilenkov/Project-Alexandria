import { describe, expect, it } from "vitest";

import type { MediaFileDto } from "@/api/streaming";

import {
  alignRangeStart,
  isNearBottom,
  isRangeCovered,
  markRangeScanned,
  nextBufferedPosition,
  previousBufferedPosition,
  rangesToRetain,
  shouldPrefetchRange,
} from "@/utils/player-shuffle-buffer";

const file = (fileId: string): MediaFileDto => ({
  fileId,
  fileName: `${fileId}.mp3`,
  mimeType: "audio/mpeg",
  currentVersionId: "v1",
  duration: 180,
  artist: null,
  album: null,
  title: null,
  genre: null,
  year: null,
  transpilationJobId: "job",
  playlistItemId: null,
  isVideo: false,
  segmentPrefix: null,
});

describe("player-shuffle-buffer", () => {
  it("aligns positions to 50-slot ranges", () => {
    expect(alignRangeStart(0)).toBe(0);
    expect(alignRangeStart(49)).toBe(0);
    expect(alignRangeStart(50)).toBe(50);
    expect(alignRangeStart(137)).toBe(100);
  });

  it("tracks scanned coverage independently of playable entries", () => {
    const scanned = markRangeScanned([], 0, 50);
    expect(isRangeCovered(scanned, 0, 50)).toBe(true);
    expect(isRangeCovered(scanned, 0, 51)).toBe(false);
    const merged = markRangeScanned(scanned, 50, 100);
    expect(merged).toEqual([{ from: 0, to: 100 }]);
    expect(isRangeCovered(merged, 25, 75)).toBe(true);
  });

  it("navigates forward through gaps without page-size dependence", () => {
    const entries = new Map([
      [0, file("a")],
      [52, file("b")],
      [97, file("c")],
    ]);
    expect(nextBufferedPosition(entries, 0, 100)).toBe(0);
    expect(nextBufferedPosition(entries, 1, 100)).toBe(52);
    expect(nextBufferedPosition(entries, 53, 100)).toBe(97);
    expect(nextBufferedPosition(entries, 98, 100)).toBeNull();
  });

  it("navigates backward through earlier ranges", () => {
    const entries = new Map([
      [0, file("a")],
      [52, file("b")],
    ]);
    expect(previousBufferedPosition(entries, 60)).toBe(52);
    expect(previousBufferedPosition(entries, 52)).toBe(52);
    expect(previousBufferedPosition(entries, 51)).toBe(0);
    expect(previousBufferedPosition(entries, -1)).toBeNull();
  });

  it("evicts ranges far from the cursor and keeps neighbors", () => {
    const entries = new Map<number, MediaFileDto>();
    for (const position of [0, 10, 50, 60, 100, 110, 150, 200]) {
      entries.set(position, file(`f-${position}`));
    }
    const { keep, dropStarts } = rangesToRetain(entries, 55, 3);
    expect([...keep].sort((a, b) => a - b)).toEqual([0, 50, 100]);
    expect(dropStarts).toContain(150);
    expect(dropStarts).toContain(200);
  });

  it("retains up to ten ranges for deliberate queue viewing", () => {
    const entries = new Map<number, MediaFileDto>();
    for (let range = 0; range < 11; range++) {
      entries.set(range * 50, file(`f-${range}`));
    }
    const { keep, dropStarts } = rangesToRetain(entries, 0);
    expect(keep.size).toBe(10);
    expect(dropStarts).toHaveLength(1);
  });

  it("prefetches the next range only near its boundary", () => {
    const scanned = [{ from: 0, to: 50 }];
    expect(shouldPrefetchRange(44, 0, scanned, 200)).toBeNull();
    expect(shouldPrefetchRange(45, 0, scanned, 200)).toBe(50);
    expect(shouldPrefetchRange(49, 0, scanned, 200)).toBe(50);
    expect(shouldPrefetchRange(45, 0, [{ from: 0, to: 100 }], 200)).toBeNull();
    expect(shouldPrefetchRange(48, 0, scanned, 50)).toBeNull();
  });

  it("detects the scroll position near the list bottom", () => {
    expect(isNearBottom(400, 300, 600)).toBe(true);
    expect(isNearBottom(100, 300, 600)).toBe(false);
    expect(isNearBottom(0, 300, 300)).toBe(true);
    expect(isNearBottom(0, 300, 900, 100)).toBe(false);
  });
});
