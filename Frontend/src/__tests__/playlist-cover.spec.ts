import { describe, expect, it } from "vitest";

import { AutoGroupKind } from "@/enums/auto-group-kind";
import {
  contrastRatio,
  coverLayoutFor,
  coverPalette,
  decadeDigits,
  hashSeed,
  hslToRgb,
  INK_DARK,
  INK_LIGHT,
  monogramFor,
  moodGlyphFor,
  relativeLuminance,
} from "@/utils/playlist-cover.utils";

describe("hashSeed", () => {
  it("is deterministic and id-sensitive", () => {
    expect(hashSeed("abc")).toBe(hashSeed("abc"));
    expect(hashSeed("abc")).not.toBe(hashSeed("abd"));
  });

  it("returns an unsigned 32-bit integer", () => {
    const seed = hashSeed("some-playlist-id");
    expect(Number.isInteger(seed)).toBe(true);
    expect(seed).toBeGreaterThanOrEqual(0);
    expect(seed).toBeLessThan(2 ** 32);
  });
});

describe("hslToRgb and luminance", () => {
  it("maps primary hues to expected channels", () => {
    expect(hslToRgb(0, 100, 50)).toEqual([255, 0, 0]);
    expect(hslToRgb(120, 100, 50)).toEqual([0, 255, 0]);
    expect(hslToRgb(240, 100, 50)).toEqual([0, 0, 255]);
  });

  it("ranks white above black", () => {
    expect(relativeLuminance(255, 255, 255)).toBeGreaterThan(relativeLuminance(0, 0, 0));
    expect(contrastRatio(1, 0)).toBeCloseTo(21, 0);
  });
});

describe("coverPalette", () => {
  it("is stable for one id across calls and themes apart", () => {
    const dark = coverPalette("id-one", true);
    expect(coverPalette("id-one", true)).toEqual(dark);
    expect(coverPalette("id-one", false).hue).toBe(dark.hue);
  });

  it("spreads hues across ids and bounds ranges", () => {
    const hues = new Set(["a", "b", "c", "d", "e", "f"].map((id) => coverPalette(id, true).hue));
    expect(hues.size).toBeGreaterThan(1);
    const palette = coverPalette("a", true);
    expect(palette.hue).toBeGreaterThanOrEqual(0);
    expect(palette.hue).toBeLessThan(360);
    expect([INK_LIGHT, INK_DARK]).toContain(palette.ink);
  });
});

describe("moodGlyphFor", () => {
  it("maps all eight taxonomy moods", () => {
    const names = [
      "Aggressive",
      "Happy",
      "Party",
      "Relaxed",
      "Sad",
      "Acoustic",
      "Instrumental",
      "Voice",
    ];
    for (const name of names) {
      expect(moodGlyphFor(name)).not.toBeNull();
    }
  });

  it("matches case-insensitively and rejects the rest", () => {
    expect(moodGlyphFor("  PARTY ")).toBe("confetti");
    expect(moodGlyphFor("Rock")).toBeNull();
    expect(moodGlyphFor("")).toBeNull();
  });
});

describe("decadeDigits", () => {
  it("pulls the first four-digit year", () => {
    expect(decadeDigits("These are the 2010s")).toBe("2010");
    expect(decadeDigits("90s Throwback")).toBeNull();
    expect(decadeDigits("No digits here")).toBeNull();
  });
});

describe("monogramFor", () => {
  it("takes initials of the first two words", () => {
    expect(monogramFor("Arcane")).toBe("A");
    expect(monogramFor("deep house cuts")).toBe("DH");
    expect(monogramFor("  spaced   out ")).toBe("SO");
  });

  it("falls back for empty names", () => {
    expect(monogramFor("")).toBe("•");
  });
});

describe("coverLayoutFor", () => {
  it("sends manual playlists to monogram", () => {
    expect(coverLayoutFor(false, null, "Mine")).toBe("monogram");
  });

  it("sends decades to numerals when digits exist", () => {
    expect(coverLayoutFor(true, AutoGroupKind.Decade, "These are the 2010s")).toBe("decade");
    expect(coverLayoutFor(true, AutoGroupKind.Decade, "Mystery")).toBe("wordmark");
  });

  it("sends artists and albums to the injected layout", () => {
    expect(coverLayoutFor(true, AutoGroupKind.Artist, "Arcane")).toBe("monogram");
    expect(coverLayoutFor(true, AutoGroupKind.Album, "Arcane", "wordmark")).toBe("wordmark");
  });

  it("sends mood tags to mood and other tags to wordmark", () => {
    expect(coverLayoutFor(true, AutoGroupKind.Tag, "Party")).toBe("mood");
    expect(coverLayoutFor(true, AutoGroupKind.Tag, "focus")).toBe("wordmark");
  });

  it("sends both genre kinds to wordmark", () => {
    expect(coverLayoutFor(true, AutoGroupKind.Genre, "Dark Ambient")).toBe("wordmark");
    expect(coverLayoutFor(true, AutoGroupKind.ParentGenre, "Rock")).toBe("wordmark");
  });
});
