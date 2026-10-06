import { describe, expect, it, vi } from "vitest";

import { WrappedCardType } from "@/enums/wrapped-card-type";
import {
  encodeWrappedExport,
  exportFilename,
  renderWrappedExport,
  wrapExportText,
} from "@/utils/wrapped-export.utils";
import { wrappedFixtures } from "@/utils/wrapped-fixtures";
import { monthlyLandscape, rankedRecords, shareLabel } from "@/utils/wrapped-ranking.utils";

describe("Wrapped polish and export", () => {
  it("keeps tied record areas and mixtape bars equal", () => {
    const entries = [0.4, 0.1, 0.1].map((share, i) => ({ title: String(i), rank: i + 1, share }));
    const records = rankedRecords(entries);

    expect(records[1]!.ratio).toBe(0.25);
    expect(records[1]!.radius).toBe(records[0]!.radius / 2);
    expect(records[2]!.radius).toBe(records[1]!.radius);
    expect(shareLabel(0.031)).toContain("3.1");
  });

  it("gives monthly mounds a common linear scale without fake zero peaks", () => {
    const peaks = monthlyLandscape(
      [0, 1800, 3600].map((seconds, i) => ({ date: `2026-0${i + 1}-01`, seconds, plays: i })),
    );

    expect(peaks.map((peak) => peak.height)).toEqual([0, 75, 150]);
  });

  it("wraps long filenames and non-Latin titles without discarding characters", () => {
    const title = "これは長い曲名です_filename_without_spaces_12345";
    const lines = wrapExportText(title, 12, (text) => Array.from(text).length);

    expect(lines.join("")).toBe(title);
    expect(lines.every((line) => Array.from(line).length <= 12)).toBe(true);

    expect(wrapExportText("A familiar song\nOne more time", 15, (text) => text.length)).toEqual([
      "A familiar song",
      "One more time",
    ]);
  });

  it("uses the requested MIME type and filename", async () => {
    const toBlob = vi.fn((callback: BlobCallback, type: string) =>
      callback(new Blob(["image"], { type })),
    );

    const canvas = { toBlob } as unknown as HTMLCanvasElement;

    expect((await encodeWrappedExport(canvas, "png")).type).toBe("image/png");
    expect((await encodeWrappedExport(canvas, "jpeg")).type).toBe("image/jpeg");
    expect(exportFilename(2026, "jpeg")).toBe("alexandria-wrapped-2026.jpeg");
  });

  it.each([0, 2])("exports %i qualified plays separately from raw sessions", async (plays) => {
    const fixture = wrappedFixtures[0]!.response;
    const hero = fixture.deck.cards.find((card) => card.type === WrappedCardType.ListeningTime)!;
    const fillText = vi.fn();
    const context = {
      fillText,
      fillRect: vi.fn(),
      measureText: (value: string) => ({ width: value.length * 8 }),
    };
    const canvas = { getContext: () => context, width: 0, height: 0 };

    const fontsDescriptor = Object.getOwnPropertyDescriptor(document, "fonts");
    const createElement = vi
      .spyOn(document, "createElement")
      .mockReturnValue(canvas as HTMLCanvasElement);

    Object.defineProperty(document, "fonts", {
      configurable: true,
      value: { ready: Promise.resolve() },
    });

    try {
      await renderWrappedExport(
        {
          ...fixture,
          summary: { ...fixture.summary!, sessions: 4, qualifiedPlayCount: plays },
          deck: {
            cards: [{ ...hero, facts: { ...hero.facts!, series: [], comparison: null } }],
          },
        },
        false,
      );

      const text = fillText.mock.calls.map(([value]) => value).join("\n");

      expect(text).toContain(`${plays} plays`);
      expect(text).not.toContain("4 plays");
      expect(canvas.height).toBeGreaterThan(0);
    } finally {
      createElement.mockRestore();

      if (fontsDescriptor) Object.defineProperty(document, "fonts", fontsDescriptor);
      else Reflect.deleteProperty(document, "fonts");
    }
  });

  it("reports encoding failure instead of offering a broken download", async () => {
    const canvas = { toBlob: (callback: BlobCallback) => callback(null) } as HTMLCanvasElement;

    await expect(encodeWrappedExport(canvas, "png")).rejects.toThrow("could not encode");
  });

  it("exports the catalog caption alongside the calculated duration comparison", async () => {
    const fixture = wrappedFixtures[0]!.response;
    const hero = fixture.deck.cards.find((card) => card.type === WrappedCardType.ListeningTime)!;
    const comparison = {
      ...hero.facts!.comparison!,
      copy: "About as much time as Spirited Away.",
      description: "Enough time to work a shift at the bathhouse.",
    };
    const fillText = vi.fn();
    const context = {
      fillText,
      fillRect: vi.fn(),
      measureText: (value: string) => ({ width: value.length * 8 }),
    };
    const canvas = { getContext: () => context, width: 0, height: 0 };
    const fontsDescriptor = Object.getOwnPropertyDescriptor(document, "fonts");
    const createElement = vi
      .spyOn(document, "createElement")
      .mockReturnValue(canvas as HTMLCanvasElement);

    Object.defineProperty(document, "fonts", {
      configurable: true,
      value: { ready: Promise.resolve() },
    });

    try {
      await renderWrappedExport(
        {
          ...fixture,
          deck: { cards: [{ ...hero, facts: { ...hero.facts!, series: [], comparison } }] },
        },
        false,
      );

      const text = fillText.mock.calls.map(([value]) => value).join("\n");

      expect(text).toContain(comparison.copy);
      expect(text).toContain("At this milestone");
      expect(text).toContain(comparison.description);
    } finally {
      createElement.mockRestore();

      if (fontsDescriptor) Object.defineProperty(document, "fonts", fontsDescriptor);
      else Reflect.deleteProperty(document, "fonts");
    }
  });
});
