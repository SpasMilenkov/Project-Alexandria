import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";

import type { WrappedComparison } from "@/api/stats";

import WrappedMilestone from "@/components/streaming/wrapped/WrappedMilestone.vue";
import {
  WrappedDurationCategory,
  WrappedDurationRelationship,
  WrappedDurationTier,
} from "@/enums/wrapped-story";

const comparison: WrappedComparison = {
  key: "ghibli_spirited_away",
  name: "Spirited Away",
  category: WrappedDurationCategory.Animation,
  tier: WrappedDurationTier.TwoToThree,
  durationMinutes: 125,
  durationLabel: "2h 5m",
  exact: true,
  relationship: WrappedDurationRelationship.Approximately,
  copy: "About as much time as Spirited Away.",
  description: "Enough time to work a shift at the bathhouse and still find your way home.",
  catalogVersion: "2026-10-06.1",
};

describe("Wrapped milestone descriptions", () => {
  it.each([
    [WrappedDurationCategory.Animation, "Animation", 9],
    [WrappedDurationCategory.Theatre, "Theatre", 10],
    [WrappedDurationCategory.Audiobook, "Audiobooks", 11],
  ] as const)("labels category %i and shows its reference caption", (category, label, ordinal) => {
    const wrapper = mount(WrappedMilestone, {
      props: { comparison: { ...comparison, category }, ink: "#22647e" },
    });

    expect(category).toBe(ordinal);
    expect(wrapper.text()).toContain(`${label} · a milestone in time`);
    expect(wrapper.text()).toContain("About as much time as");
    expect(wrapper.get("blockquote").text()).toContain(comparison.description);

    wrapper.unmount();
  });

  it("switches the caption with its alternative and accepts older summaries without one", async () => {
    const alternative: WrappedComparison = {
      ...comparison,
      key: "hp_cursed_child_play",
      name: "Harry Potter and the Cursed Child",
      category: WrappedDurationCategory.Theatre,
      description: "Longer than the original two-part Cursed Child, intermissions included.",
    };
    const wrapper = mount(WrappedMilestone, {
      props: { comparison, alternatives: [alternative], ink: "#22647e" },
    });

    await wrapper.findAll("button")[1]!.trigger("click");

    expect(wrapper.get("blockquote").text()).toContain(alternative.description);
    expect(wrapper.text()).toContain("Theatre · a milestone in time");

    await wrapper.setProps({
      comparison: { ...comparison, key: "older-summary", description: undefined },
    });

    expect(wrapper.find("blockquote").exists()).toBe(false);
    expect(wrapper.text()).toContain("Spirited Away");

    wrapper.unmount();
  });
});
