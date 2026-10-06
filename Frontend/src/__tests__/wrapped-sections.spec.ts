import { describe, expect, it } from "vitest";

import type { WrappedCardResponse } from "@/api/stats";

import { WrappedCardType } from "@/enums/wrapped-card-type";
import { buildWrappedSections } from "@/utils/wrapped-sections.utils";

const card = (type: WrappedCardType, headline: string): WrappedCardResponse => ({
  type,
  headline,
  subline: null,
  entries: [],

  visual: {
    colorKey: "ember",
    saturation: "medium",
    contrast: "balanced",
    density: 0.2,
    fillLevel: 0,
    shapeGrammar: "aura",
  },
});

describe("buildWrappedSections", () => {
  it("pulls hero and countdowns out, keeps the rest in order", () => {
    const sections = buildWrappedSections([
      card(WrappedCardType.TopArtists, "artists"),
      card(WrappedCardType.ListeningTime, "time"),
      card(WrappedCardType.Streak, "streak"),
      card(WrappedCardType.TopSongs, "songs"),
      card(WrappedCardType.Bookends, "ends"),
    ]);

    expect(sections.hero?.headline).toBe("time");
    expect(sections.artists?.headline).toBe("artists");
    expect(sections.songs?.headline).toBe("songs");
    expect(sections.spotlight.map((item) => item.headline)).toEqual(["streak", "ends"]);
  });

  it("leaves everything in the spotlight when the deck is moments-only", () => {
    const sections = buildWrappedSections([
      card(WrappedCardType.Discoveries, "finds"),
      card(WrappedCardType.NewArtists, "new"),
    ]);

    expect(sections.hero).toBeUndefined();
    expect(sections.spotlight).toHaveLength(2);
  });
});
