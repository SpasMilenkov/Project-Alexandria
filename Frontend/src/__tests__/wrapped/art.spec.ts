import { describe, expect, it } from "vitest";

import type { WrappedCardResponse } from "@/api/stats";

import { WrappedCardType as Type } from "@/enums/wrapped-card-type";
import { buildWrappedRecipe } from "@/utils/wrapped-art.utils";

const card = (type: Type): WrappedCardResponse => ({
  type,
  headline: "Test",
  entries: [],

  visual: {
    colorKey: "daylight",
    saturation: "balanced",
    contrast: "crisp",
    density: 0,
    fillLevel: 0,
    shapeGrammar: "",
  },

  facts: { seconds: 100, count: 18, share: 0.5, baselineSeconds: 0, weights: [], series: [] },
});

describe("shared Wrapped artwork after cleanup", () => {
  it("does not draw retired artwork for cards with dedicated renderers", () => {
    for (const type of [
      Type.ListeningTime,
      Type.TopSongs,
      Type.TopArtists,
      Type.Persona,
      Type.Streak,
      Type.MostReplayed,
      Type.BusiestDay,
      Type.Bookends,
      Type.ReturningFavorite,
    ]) {
      expect(buildWrappedRecipe(card(type), "same-seed")).toBeNull();
    }
  });

  it("preserves the remaining exploration, discovery and chapter recipes", () => {
    expect(buildWrappedRecipe(card(Type.Exploration), "same-seed")?.family).toBe("orbit");
    expect(buildWrappedRecipe(card(Type.RetainedDiscovery), "same-seed")?.circles).toHaveLength(18);
    expect(buildWrappedRecipe(card(Type.Chapters), "same-seed")?.family).toBe("ribbons");

    expect(buildWrappedRecipe(card(Type.RetainedDiscovery), "same-seed")).toEqual(
      buildWrappedRecipe(card(Type.RetainedDiscovery), "same-seed"),
    );
  });
});
