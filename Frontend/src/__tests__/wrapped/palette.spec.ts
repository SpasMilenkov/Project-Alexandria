import { describe, expect, it } from "vitest";

import { WrappedTimeScene } from "@/enums/wrapped-story";
import {
  parseWrappedPalette,
  wrappedCssColors,
  wrappedExportColors,
  wrappedPalettePresets,
} from "@/utils/wrapped-palette.utils";
import { getWrappedSkyPreset, skyPresets } from "@/utils/wrapped-sky.utils";

const relativeLuminance = (hex: string) => {
  const channels = [1, 3, 5].map((offset) => {
    const value = parseInt(hex.slice(offset, offset + 2), 16) / 255;

    if (value <= 0.04045) return value / 12.92;

    return ((value + 0.055) / 1.055) ** 2.4;
  });

  return channels[0]! * 0.2126 + channels[1]! * 0.7152 + channels[2]! * 0.0722;
};

describe("Wrapped palettes", () => {
  it("rejects corrupt browser preferences and accepts three hex colors", () => {
    for (const value of [null, {}, ["#123456"], ["red", "#123456", "#abcdef"]])
      expect(parseWrappedPalette(value)).toBeNull();

    expect(parseWrappedPalette(["#ABCDEF", "#123456", "#ffffff"])).toEqual([
      "#abcdef",
      "#123456",
      "#ffffff",
    ]);
  });

  it.each([false, true])("uses the same inks for live art and export (dark: %s)", (isDark) => {
    for (const preset of wrappedPalettePresets) {
      const css = wrappedCssColors(isDark, preset.colors);
      const exported = wrappedExportColors(isDark, preset.colors);

      for (const key of ["blue", "purple", "leaf", "teal", "rose", "gold"] as const)
        expect(css[`--wrapped-${key}`]).toBe(exported[key]);

      expect(exported.blue).not.toBe(wrappedExportColors(isDark).blue);
    }
  });

  it.each([false, true])("keeps even black and white custom inks legible (dark: %s)", (isDark) => {
    const css = wrappedCssColors(isDark, ["#000000", "#ffffff", "#ffe3b1"]);
    const paper = relativeLuminance(css["--wrapped-cutout"]!);

    for (const key of ["blue", "purple", "leaf"]) {
      const ink = relativeLuminance(css[`--wrapped-${key}`]!);

      expect((Math.max(ink, paper) + 0.05) / (Math.min(ink, paper) + 0.05)).toBeGreaterThanOrEqual(
        4.5,
      );
    }
  });

  it("preserves scene lighting and geometry while tinting sky artwork", () => {
    for (const scene of [WrappedTimeScene.Morning, WrappedTimeScene.Night]) {
      const original = skyPresets[scene];
      const tinted = getWrappedSkyPreset(scene, wrappedPalettePresets[0]!.colors);

      expect(getWrappedSkyPreset(scene)).toEqual(original);
      expect(tinted.sunX).toBe(original.sunX);
      expect(tinted.stars).toBe(original.stars);
      expect(tinted.scrimOpacity).toBe(original.scrimOpacity);
      expect(tinted.skyStops).not.toEqual(original.skyStops);

      tinted.skyStops.forEach((stop, index) => {
        expect(relativeLuminance(stop.color)).toBeCloseTo(
          relativeLuminance(original.skyStops[index]!.color),
          2,
        );
      });

      expect(skyPresets[scene]).toEqual(original);
    }
  });

  it("restores the original art and export colors on reset", () => {
    expect(wrappedCssColors(false, null)["--wrapped-blue"]).toBe("#245a91");
    expect(wrappedCssColors(true, null)["--wrapped-blue"]).toBe("#8ebfff");
    expect(wrappedExportColors(false, null).background).toBe("#f4f1e9");
  });
});
