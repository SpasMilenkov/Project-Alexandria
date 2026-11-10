import { describe, expect, it } from "vitest";

import type { MediaFileDto } from "@/api/streaming";

import { heroTint } from "@/features/media/cover-color";
import { coverSeed, generatedCover } from "@/features/media/generated-cover";
import {
  addedThisWeekLabel,
  buildWeekDays,
  formatListeningTime,
  groupByDay,
} from "@/features/media/overview";

const NOW = Date.UTC(2026, 9, 31, 12);

const file = (createdAt: string | null): MediaFileDto => ({
  fileId: createdAt ?? "none",
  fileName: "song.mp3",
  title: null,
  artist: null,
  album: null,
  genre: null,
  year: null,
  duration: null,
  mimeType: "audio/mpeg",
  currentVersionId: "v",
  transpilationJobId: "j",
  isVideo: false,
  segmentPrefix: null,
  createdAt,
});

describe("generated covers", () => {
  it("returns the same artwork for the same seed and shares it across an album", () => {
    expect(generatedCover("seed")).toEqual(generatedCover("seed"));
    expect(coverSeed({ fileId: "a", artist: "Band", album: "Record" })).toBe(
      coverSeed({ fileId: "b", artist: "Band", album: "Record" }),
    );
    expect(coverSeed({ fileId: "a", artist: "Band", album: null })).toBe("a");
  });

  it("pulls the hero tint toward the theme base", () => {
    expect(heroTint("#ffffff", true)).toBe("#808080");
    expect(heroTint("#000000", false)).toBe("#808080");
  });
});

describe("media overview labels", () => {
  it("counts this week's additions and marks a full page as possibly more", () => {
    const recent = new Date(NOW - 86_400_000).toISOString();
    const old = new Date(NOW - 30 * 86_400_000).toISOString();

    expect(addedThisWeekLabel([file(recent), file(old)], 2, NOW)).toBe("1 added this week");
    expect(addedThisWeekLabel([file(recent), file(recent)], 40, NOW)).toBe("2+ added this week");
    expect(addedThisWeekLabel([file(old), file(null)], 2, NOW)).toBeNull();
  });

  it("formats listening time", () => {
    expect(formatListeningTime(0)).toBe("0m");
    expect(formatListeningTime(3600)).toBe("1h");
    expect(formatListeningTime(15_120)).toBe("4h 12m");
  });

  it("fills missing UTC days so the week always has seven bars", () => {
    const days = buildWeekDays("2026-10-25T00:00:00.000Z", [{ date: "2026-10-27", seconds: 90 }]);

    expect(days.map((day) => day.date)).toEqual([
      "2026-10-25",
      "2026-10-26",
      "2026-10-27",
      "2026-10-28",
      "2026-10-29",
      "2026-10-30",
      "2026-10-31",
    ]);
    expect(days[2]!.seconds).toBe(90);
    expect(days.filter((day) => day.isToday).map((day) => day.date)).toEqual(["2026-10-31"]);
  });

  it("carries plays for listened days and zeroes the gaps", () => {
    const days = buildWeekDays("2026-10-25T00:00:00.000Z", [
      { date: "2026-10-27", plays: 4, seconds: 90 },
    ]);

    expect(days[2]).toMatchObject({ plays: 4, seconds: 90 });
    expect(days[3]).toMatchObject({ plays: 0, seconds: 0 });
  });

  it("groups consecutive history entries under one day label", () => {
    const today = new Date(NOW).toISOString();
    const yesterday = new Date(NOW - 86_400_000).toISOString();

    const groups = groupByDay(
      [{ lastAccessedAt: today }, { lastAccessedAt: today }, { lastAccessedAt: yesterday }],
      NOW,
    );

    expect(groups.map((group) => [group.label, group.items.length])).toEqual([
      ["Today", 2],
      ["Yesterday", 1],
    ]);
  });
});
