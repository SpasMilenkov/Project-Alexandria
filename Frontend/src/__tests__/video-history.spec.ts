import { describe, expect, it } from "vitest";

import type { StreamHistoryResponse } from "@/api/streaming";

import { isWatchedEntry, progressPercentOf } from "@/composables/useVideoHistory";

const history = (hasFinished = false): StreamHistoryResponse => ({
  id: "history",
  fileId: "movie",
  title: "Movie",

  positionSeconds: 180,
  maxPositionReachedSeconds: 180,
  totalListenedSeconds: 180,
  qualifiedPlayCount: 6,
  hasFinished,

  lastPlayedAt: "2026-01-01T12:00:00Z",
  lastAccessedAt: "2026-01-01T12:00:00Z",
  createdAt: "2026-01-01T12:00:00Z",
  updatedAt: null,
});

describe("video watch history", () => {
  it("keeps qualified plays resumable before finishing", () => {
    const entry = history();

    expect(isWatchedEntry(entry)).toBe(false);
    expect(progressPercentOf(entry, 600)).toBe(30);
  });

  it("shows watched status only after finishing", () => {
    const entry = history(true);

    expect(isWatchedEntry(entry)).toBe(true);
    expect(progressPercentOf(entry, 600)).toBeNull();
  });

  it("does not infer finishing from the final position alone", () => {
    const entry = { ...history(), positionSeconds: 599 };

    expect(isWatchedEntry(entry)).toBe(false);
    expect(progressPercentOf(entry, 600)).toBe(99);
  });
});
