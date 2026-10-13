import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";

import HomeClockWidget from "@/components/dashboard/home/widgets/HomeClockWidget.vue";
import { defaultHomeLayout } from "@/utils/home-layout";
import {
  homeTimeZoneChoices,
  homeTimeZoneLabel,
  resolveHomeTimeZone,
} from "@/utils/home-time-zones";

const format = (identifier: string, date: string) => {
  const zone = resolveHomeTimeZone(identifier);

  return new Intl.DateTimeFormat("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZone: zone.timeZone,
  }).format(new Date(date));
};

describe("Home timezone compatibility", () => {
  it("uses named-zone rules across daylight saving and quarter-hour offsets", () => {
    expect(format("Europe/Bucharest", "2026-01-15T12:00:00Z")).toBe("14:00");
    expect(format("Europe/Bucharest", "2026-07-15T12:00:00Z")).toBe("15:00");
    expect(format("Asia/Kathmandu", "2026-01-15T12:00:00Z")).toBe("17:45");
    expect(format("Pacific/Chatham", "2026-01-15T00:00:00Z")).toBe("13:45");
  });

  it("accepts browser-supported aliases without changing the stored string", () => {
    const saved = "US/Eastern";
    expect(resolveHomeTimeZone(saved).unavailable).toBe(false);
    expect(format(saved, "2026-01-15T12:00:00Z")).toBe("07:00");
    expect(saved).toBe("US/Eastern");
  });

  it("keeps device-local separate from an unavailable identifier", () => {
    expect(resolveHomeTimeZone("local")).toEqual({ timeZone: undefined, unavailable: false });
    expect(resolveHomeTimeZone("Europe/RemovedZone")).toEqual({
      timeZone: undefined,
      unavailable: true,
    });
    expect(resolveHomeTimeZone(null)).toEqual({ timeZone: undefined, unavailable: true });
  });

  it("offers only server-provided browser-compatible choices and retains unavailable saved values", () => {
    const identifiers = ["local", "UTC", "Asia/Kathmandu", "Asia/Kathmandu", "Europe/RemovedZone"];
    const choices = homeTimeZoneChoices(identifiers, "Europe/SavedZone");

    expect(choices.map((choice) => choice.value)).toEqual([
      "Europe/SavedZone",
      "local",
      "UTC",
      "Asia/Kathmandu",
    ]);
    expect(choices[0]).toMatchObject({ value: "Europe/SavedZone", disabled: true });
    expect(choices.some((choice) => choice.value === "Europe/Bucharest")).toBe(false);
    expect(identifiers).toHaveLength(5);
  });

  it("labels identifiers without a separate city list", () => {
    expect(homeTimeZoneLabel("local")).toBe("Your device's time zone");
    expect(homeTimeZoneLabel("UTC")).toBe("UTC");
    expect(homeTimeZoneLabel("America/Argentina/Buenos_Aires")).toBe(
      "America / Argentina / Buenos Aires",
    );
  });

  it("renders a labelled fallback and preserves the saved widget options", () => {
    const widget = defaultHomeLayout().widgets[0]!;
    widget.options.timeZone = "Europe/RemovedZone";
    const wrapper = mount(HomeClockWidget, { props: { widget } });

    try {
      expect(wrapper.get('[role="status"]').text()).toContain("unavailable. Showing device time.");
      expect(wrapper.get(".clock-time").text()).not.toBe("");
      expect(widget.options.timeZone).toBe("Europe/RemovedZone");
    } finally {
      wrapper.unmount();
    }
  });
});
