import { describe, expect, it } from "vitest";

import type { OverviewSummaryDtoResponse } from "@/api/stats";

import {
  SUMMARY_KIND_WRAPPED,
  summaryToWrappedDeck,
  summaryYear,
} from "@/utils/wrapped-summary.utils";

const summary = (overrides = {}): OverviewSummaryDtoResponse => ({
  id: "00000000-0000-0000-0000-000000000001",
  kind: 1,
  periodStart: "2025-01-01T00:00:00.000Z",
  periodEnd: "2026-01-01T00:00:00.000Z",
  generatedAt: "2026-01-02T00:00:00.000Z",
  finalizedAt: "2026-01-02T00:00:00.000Z",
  isFinal: true,
  schemaVersion: 3,

  payload: {
    type: "wrapped",

    summary: {
      seconds: 3600,
      sessions: 4,
      qualifiedPlayCount: 2,
      tracks: 3,
      artists: 2,
      activeDays: 2,
      knownArtistShare: 0.5,
      hasPriorHistory: true,
      historyFrom: "2024-06-01T00:00:00.000Z",
    },

    deck: { cards: [] },
    visualIdentity: "abcdef1234567890",
    recipeVersion: 2,
    catalogVersion: "2026-09-13.1",
  },

  ...overrides,
});

describe("summaryToWrappedDeck", () => {
  it("maps a frozen wrapped payload onto the deck contract", () => {
    const deck = summaryToWrappedDeck(summary());

    expect(deck?.from).toBe("2025-01-01T00:00:00.000Z");
    expect(deck?.to).toBe("2026-01-01T00:00:00.000Z");
    expect(deck?.schemaVersion).toBe(3);
    expect(deck?.recipeVersion).toBe(2);
    expect(deck?.visualIdentity).toBe("abcdef1234567890");
    expect(deck?.summary?.seconds).toBe(3600);
    expect(deck?.summary?.sessions).toBe(4);
    expect(deck?.summary?.qualifiedPlayCount).toBe(2);
    expect(deck?.summary?.hasPriorHistory).toBe(true);
  });

  it("returns null for unknown future payload kinds", () => {
    const unknown = summary({
      payload: { type: "seasonal-mixtape", deck: { cards: [] } },
    }) as unknown as OverviewSummaryDtoResponse;

    expect(summaryToWrappedDeck(unknown)).toBeNull();
  });

  it("reads the recap year from the stored period", () => {
    expect(summaryYear("2025-01-01T00:00:00.000Z")).toBe(2025);
    expect(SUMMARY_KIND_WRAPPED).toBe("Wrapped");
  });
});
