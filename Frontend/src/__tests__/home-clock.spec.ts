import { mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { HomeWidget } from "@/types/home";

import HomeClockFlipFace from "@/components/dashboard/home/widgets/HomeClockFlipFace.vue";
import HomeClockWidget from "@/components/dashboard/home/widgets/HomeClockWidget.vue";
import { HomeClockHourFormat, HomeClockStyle, HomeWidgetType } from "@/enums/home-widget";
import {
  homeClockDateLabels,
  homeClockDateWindow,
  homeClockHandAngles,
  homeClockReadout,
  homeClockRingProgress,
  homeClockWords,
} from "@/utils/home-clock";
import { createHomeWidget } from "@/utils/home-layout";

const mounted: ReturnType<typeof mount>[] = [];

const mountClock = (widget: HomeWidget) => {
  const wrapper = mount(HomeClockWidget, { props: { widget } });
  mounted.push(wrapper);

  return wrapper;
};

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("Home clock appearances", () => {
  it.each([
    ["2026-01-15T12:00:00Z", "UTC", 0, 0],
    ["2026-01-15T12:00:00Z", "Asia/Kathmandu", 172.5, 270],
    ["2026-07-15T12:00:00Z", "Europe/Bucharest", 90, 0],
    ["2026-01-15T12:00:00Z", "Europe/Bucharest", 60, 0],
    ["2026-01-15T10:08:30Z", "UTC", 304.25, 51],
  ])("positions hands for %s in %s", (instant, zone, hour, minute) => {
    const clock = homeClockReadout(Date.parse(instant), zone);
    const hands = homeClockHandAngles(clock.hour24, clock.minuteNumber, clock.secondNumber);

    expect(hands.hour).toBeCloseTo(hour);
    expect(hands.minute).toBeCloseTo(minute);
  });

  it("changes renderers without changing geometry and advances analog hands", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T12:00:00Z"));
    vi.stubGlobal(
      "matchMedia",
      vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
    );

    const widget = createHomeWidget(HomeWidgetType.Clock, "clock");
    widget.options.timeZone = "Asia/Kathmandu";
    const wrapper = mountClock(widget);

    expect(wrapper.find(".clock-dial").exists()).toBe(false);

    await wrapper.setProps({
      widget: { ...widget, options: { ...widget.options, clockStyle: HomeClockStyle.Analog } },
    });

    expect(wrapper.get(".clock-hour-hand").attributes("transform")).toBe("rotate(172.5 100 100)");
    expect(wrapper.get(".clock-minute-hand").attributes("transform")).toBe("rotate(270 100 100)");

    await vi.advanceTimersByTimeAsync(60000);

    expect(wrapper.get(".clock-minute-hand").attributes("transform")).toBe("rotate(276 100 100)");

    await wrapper.setProps({
      widget: { ...widget, options: { ...widget.options, clockStyle: HomeClockStyle.Material } },
    });

    expect(wrapper.find(".clock-dial").exists()).toBe(false);
    expect(wrapper.get(".clock-material-time").text()).toContain("46");
    expect(widget).toMatchObject({ width: 6, height: 4 });

    wrapper.unmount();
  });

  it("uses device time for an unavailable analog zone and keeps its saved identifier", () => {
    const widget = createHomeWidget(HomeWidgetType.Clock, "clock");
    widget.options.clockStyle = HomeClockStyle.Analog;
    widget.options.timeZone = "invalid/saved-zone";
    const wrapper = mountClock(widget);

    expect(wrapper.find(".clock-dial").exists()).toBe(true);
    expect(wrapper.get('[role="status"]').text()).toContain("Showing device time.");
    expect(widget.options.timeZone).toBe("invalid/saved-zone");

    wrapper.unmount();
  });
});

describe("Home clock formatting", () => {
  it("reuses zoned-day captions across minute changes and updates at zoned midnight", () => {
    const before = Date.parse("2026-01-15T18:14:00Z");
    const captions = homeClockDateLabels(before, "Asia/Kathmandu", "en-US");

    expect(homeClockDateLabels(before + 30_000, "Asia/Kathmandu", "en-US")).toBe(captions);
    expect(homeClockDateLabels(before + 60_000, "Asia/Kathmandu", "en-US").date).toBe(
      "Friday, January 16",
    );
    expect(homeClockDateLabels(before, "UTC", "en-US").date).toBe("Thursday, January 15");
    expect(homeClockDateLabels(before, "Asia/Kathmandu", "fr-FR").date).not.toBe(captions.date);
  });

  it.each([
    [HomeClockHourFormat.TwelveHour, "12:05:09 AM"],
    [HomeClockHourFormat.TwentyFourHour, "00:05:09"],
  ])("includes seconds in the saved-zone time for format %s", (format, time) => {
    const clock = homeClockReadout(
      Date.parse("2026-01-15T18:20:09Z"),
      "Asia/Kathmandu",
      format,
      "en-US",
      true,
    );

    expect(clock.time).toBe(time);
    expect(clock.second).toBe("09");
  });

  it.each([
    [HomeClockHourFormat.TwelveHour, "12", "AM"],
    [HomeClockHourFormat.TwentyFourHour, "00", ""],
  ])("formats Kathmandu midnight with hour format %s", (format, hour, period) => {
    const clock = homeClockReadout(
      Date.parse("2026-01-15T18:20:00Z"),
      "Asia/Kathmandu",
      format,
      "en-US",
    );

    expect(clock).toMatchObject({
      hour,
      minute: "05",
      period,
      hour24: 0,
      greeting: "Good night",
    });
    const timestamp = Date.parse("2026-01-15T18:20:00Z");
    expect(homeClockDateLabels(timestamp, "Asia/Kathmandu", "en-US").date).toBe(
      "Friday, January 16",
    );
    expect(homeClockDateWindow(timestamp, "Asia/Kathmandu", "en-US")).toBe("16 FRI");
  });

  it("preserves the device locale hour cycle by default", () => {
    const instant = Date.parse("2026-01-15T18:20:00Z");
    const american = homeClockReadout(instant, "UTC", HomeClockHourFormat.DeviceDefault, "en-US");
    const british = homeClockReadout(instant, "UTC", HomeClockHourFormat.DeviceDefault, "en-GB");

    expect(american).toMatchObject({ hour: "06", period: "PM" });
    expect(british).toMatchObject({ hour: "18", period: "" });
  });

  it("reuses formatters across ticks and clocks with identical preferences", () => {
    const constructors = vi.spyOn(Intl, "DateTimeFormat");
    const instant = Date.parse("2026-01-15T18:20:00Z");

    homeClockReadout(instant, "Pacific/Chatham", HomeClockHourFormat.TwelveHour, "en-NZ");
    const first = constructors.mock.calls.length;

    for (let tick = 1; tick <= 10; tick++) {
      homeClockReadout(
        instant + tick * 1000,
        "Pacific/Chatham",
        HomeClockHourFormat.TwelveHour,
        "en-NZ",
      );
    }

    expect(first).toBeGreaterThan(0);
    expect(constructors).toHaveBeenCalledTimes(first);
  });

  it("adds fractional seconds without changing timezone hand positions", () => {
    const stepped = homeClockHandAngles(10, 8, 30);
    const smooth = homeClockHandAngles(10, 8, 30.5);

    expect(stepped.second).toBe(180);
    expect(smooth.second).toBe(183);
    expect(smooth.minute).toBeCloseTo(51.05);
    expect(smooth.hour).toBeCloseTo(304.2541667);
  });
});

describe("Home clock lab presentation", () => {
  it.each([
    [10, 2, "ten", "o’clock"],
    [10, 3, "five past", "ten"],
    [10, 15, "quarter past", "ten"],
    [10, 30, "half past", "ten"],
    [10, 33, "twenty-five to", "eleven"],
    [11, 45, "quarter to", "twelve"],
    [23, 57, "five to", "twelve"],
    [23, 58, "twelve", "o’clock"],
    [0, 0, "twelve", "o’clock"],
  ])("rounds %i:%i to the spoken five-minute phrase", (hour, minute, lead, ending) => {
    expect(homeClockWords(hour, minute)).toMatchObject({ lead, ending });
  });

  it("derives spoken time from the saved zone across a DST jump", () => {
    const before = homeClockReadout(Date.parse("2026-03-08T06:59:00Z"), "America/New_York");
    const after = homeClockReadout(Date.parse("2026-03-08T07:00:00Z"), "America/New_York");

    expect(homeClockWords(before.hour24, before.minuteNumber)).toEqual({
      lead: "two",
      ending: "o’clock",
      period: "at night",
    });
    expect(homeClockWords(after.hour24, after.minuteNumber)).toEqual({
      lead: "three",
      ending: "o’clock",
      period: "at night",
    });
  });

  it("keeps ring progress correct at noon, midnight and fractional seconds", () => {
    expect(homeClockRingProgress(12, 0, 0)).toEqual({ hour: 0, minute: 0, second: 0 });
    expect(homeClockRingProgress(0, 0, 0)).toEqual({ hour: 0, minute: 0, second: 0 });

    const progress = homeClockRingProgress(18, 30, 30.5);

    expect(progress.hour).toBeCloseTo((6.508472222 / 12) * 100);
    expect(progress.minute).toBeCloseTo((30.508333333 / 60) * 100);
    expect(progress.second).toBeCloseTo((30.5 / 60) * 100);
  });
});

describe("Home clock seconds", () => {
  it("updates the seconds pair in Flip without animating on entry or adding a timer", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:08Z"));
    vi.stubGlobal("matchMedia", () => ({
      matches: false,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }));

    const widget = createHomeWidget(HomeWidgetType.Clock, "flip-seconds");
    widget.options = {
      ...widget.options,
      clockStyle: HomeClockStyle.Flip,
      timeZone: "UTC",
      hourFormat: HomeClockHourFormat.TwentyFourHour,
      showSeconds: true,
    };
    const wrapper = mountClock(widget);
    await wrapper.vm.$nextTick();

    expect(wrapper.get('[data-clock-part="second"]').text()).toBe("08");
    expect(wrapper.findAll(".clock-flip-digit--changing")).toHaveLength(0);
    expect(vi.getTimerCount()).toBe(1);

    await vi.advanceTimersByTimeAsync(1000);

    expect(wrapper.get(".clock-time").text()).toBe("10:08:09");
    expect(wrapper.get('[data-clock-part="second"]').text()).toBe("09");
    expect(wrapper.findAll(".clock-flip-digit--changing")).toHaveLength(1);

    await wrapper.setProps({
      widget: { ...widget, options: { ...widget.options, showSeconds: false } },
    });

    expect(wrapper.find('[data-clock-part="second"]').exists()).toBe(false);
    expect(wrapper.get(".clock-time").text()).toBe("10:08");
    expect(vi.getTimerCount()).toBe(1);
  });
});

describe("Home clock timing", () => {
  it("animates only a changed Flip digit and stops that animation under reduced motion", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:59Z"));
    let reduced = false;
    let motionChanged = () => {};

    vi.stubGlobal("matchMedia", () => ({
      get matches() {
        return reduced;
      },
      addEventListener: (_event: string, listener: () => void) => {
        motionChanged = listener;
      },
      removeEventListener: vi.fn(),
    }));

    const widget = createHomeWidget(HomeWidgetType.Clock, "flip");
    widget.options.clockStyle = HomeClockStyle.Flip;
    widget.options.timeZone = "UTC";
    widget.options.hourFormat = HomeClockHourFormat.TwentyFourHour;
    const wrapper = mountClock(widget);
    await wrapper.vm.$nextTick();

    expect(wrapper.findAll(".clock-flip-digit--changing")).toHaveLength(0);
    expect(vi.getTimerCount()).toBe(1);

    await vi.advanceTimersByTimeAsync(1000);

    expect(wrapper.get(".clock-time").text()).toBe("10:09");
    expect(wrapper.findAll(".clock-flip-digit--changing")).toHaveLength(1);
    expect(wrapper.get(".clock-flip-digit--changing").text()).toBe("9");

    reduced = true;
    motionChanged();
    await wrapper.vm.$nextTick();

    expect(wrapper.findAll(".clock-flip-digit--changing")).toHaveLength(0);
    await vi.advanceTimersByTimeAsync(60000);
    expect(wrapper.get(".clock-time").text()).toBe("10:10");
    expect(wrapper.findAll(".clock-flip-digit--changing")).toHaveLength(0);
  });

  it("shares Watch and Rings animation frames without formatting on each frame", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:00Z"));
    const parts = vi.spyOn(Intl.DateTimeFormat.prototype, "formatToParts");
    const widget = createHomeWidget(HomeWidgetType.Clock, "watch");
    widget.options.clockStyle = HomeClockStyle.Analog;
    widget.options.timeZone = "UTC";
    const watchClock = mountClock(widget);
    const ringsClock = mountClock({
      ...widget,
      instanceId: "rings",
      options: { ...widget.options, clockStyle: HomeClockStyle.Rings },
    });
    await ringsClock.vm.$nextTick();

    const initialCalls = parts.mock.calls.length;
    const seconds = ringsClock.get('[data-ring="second"]');
    const initialProgress = seconds.attributes("stroke-dasharray");

    expect(vi.getTimerCount()).toBe(2);
    await vi.advanceTimersByTimeAsync(250);
    expect(seconds.attributes("stroke-dasharray")).not.toBe(initialProgress);
    expect(parts).toHaveBeenCalledTimes(initialCalls);

    watchClock.unmount();
    expect(vi.getTimerCount()).toBe(2);

    ringsClock.unmount();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("shares one second timer across Quiet and Tile and clears it after the last clock", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:59Z"));
    const quietWidget = createHomeWidget(HomeWidgetType.Clock, "quiet");
    quietWidget.options.timeZone = "UTC";
    quietWidget.options.hourFormat = HomeClockHourFormat.TwentyFourHour;
    const tileWidget = {
      ...quietWidget,
      instanceId: "tile",
      options: { ...quietWidget.options, clockStyle: HomeClockStyle.Material },
    };
    const quiet = mountClock(quietWidget);
    const tile = mountClock(tileWidget);
    await quiet.vm.$nextTick();

    expect(vi.getTimerCount()).toBe(1);
    expect(quiet.get(".clock-time").text()).toBe("10:08");
    expect(tile.get(".clock-time").text()).toBe("10:08");

    quiet.unmount();
    expect(vi.getTimerCount()).toBe(1);

    tile.unmount();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("sweeps only Watch hands while formatting once per second", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:00Z"));
    const widget = createHomeWidget(HomeWidgetType.Clock, "watch");
    widget.options.timeZone = "UTC";
    widget.options.clockStyle = HomeClockStyle.Analog;
    const parts = vi.spyOn(Intl.DateTimeFormat.prototype, "formatToParts");
    const wrapper = mountClock(widget);
    await wrapper.vm.$nextTick();

    const initialCalls = parts.mock.calls.length;
    const hourTransform = wrapper.get(".clock-hour-hand").attributes("transform");
    const minuteTransform = wrapper.get(".clock-minute-hand").attributes("transform");

    await vi.advanceTimersByTimeAsync(250);

    expect(wrapper.get(".clock-second-hand").attributes("transform")).not.toBe("rotate(0 100 100)");
    expect(parts).toHaveBeenCalledTimes(initialCalls);
    expect(wrapper.get(".clock-hour-hand").attributes("transform")).toBe(hourTransform);
    expect(wrapper.get(".clock-minute-hand").attributes("transform")).toBe(minuteTransform);

    await wrapper.setProps({
      widget: { ...widget, options: { ...widget.options, clockStyle: HomeClockStyle.Minimal } },
    });
    expect(vi.getTimerCount()).toBe(1);
  });

  it("steps Watch once a second for reduced motion", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:00Z"));
    vi.stubGlobal(
      "matchMedia",
      vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
    );
    const widget = createHomeWidget(HomeWidgetType.Clock, "watch");
    widget.options.clockStyle = HomeClockStyle.Analog;
    widget.options.timeZone = "UTC";
    const watchClock = mountClock(widget);
    const quietClock = mountClock({
      ...widget,
      instanceId: "quiet",
      options: { ...widget.options, clockStyle: HomeClockStyle.Minimal },
    });

    await watchClock.vm.$nextTick();
    expect(watchClock.get(".clock-alignment").attributes("data-motion-active")).toBe("false");
    expect(quietClock.get(".clock-colon").text()).toBe(":");
    expect(vi.getTimerCount()).toBe(1);

    await vi.advanceTimersByTimeAsync(500);
    expect(watchClock.get(".clock-second-hand").attributes("transform")).toBe("rotate(0 100 100)");

    await vi.advanceTimersByTimeAsync(500);
    expect(watchClock.get(".clock-second-hand").attributes("transform")).toBe("rotate(6 100 100)");
  });

  it("stops continuous frames offscreen and resumes at the current instant", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:00Z"));
    let intersection: IntersectionObserverCallback = () => {};
    const observer = { observe: vi.fn(), disconnect: vi.fn() };

    vi.stubGlobal(
      "IntersectionObserver",
      class {
        constructor(callback: IntersectionObserverCallback) {
          intersection = callback;
        }
        observe = observer.observe;
        disconnect = observer.disconnect;
      },
    );

    const widget = createHomeWidget(HomeWidgetType.Clock, "watch");
    widget.options.clockStyle = HomeClockStyle.Analog;
    widget.options.timeZone = "UTC";
    const wrapper = mountClock(widget);

    intersection(
      [{ isIntersecting: false } as IntersectionObserverEntry],
      observer as unknown as IntersectionObserver,
    );
    await wrapper.vm.$nextTick();
    expect(wrapper.get(".clock-alignment").attributes("data-motion-active")).toBe("false");
    expect(vi.getTimerCount()).toBe(1);

    await vi.advanceTimersByTimeAsync(1500);
    intersection(
      [{ isIntersecting: true } as IntersectionObserverEntry],
      observer as unknown as IntersectionObserver,
    );
    await wrapper.vm.$nextTick();
    expect(wrapper.get(".clock-second-hand").attributes("transform")).toBe("rotate(9 100 100)");
    expect(vi.getTimerCount()).toBe(2);

    wrapper.unmount();
    expect(observer.disconnect).toHaveBeenCalledOnce();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("suspends hidden-tab timing and catches up dates on return", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T23:59:59Z"));
    const widget = createHomeWidget(HomeWidgetType.Clock, "watch");
    widget.options.timeZone = "UTC";
    widget.options.clockStyle = HomeClockStyle.Analog;
    const wrapper = mountClock(widget);
    const visibility = vi.spyOn(document, "visibilityState", "get");

    visibility.mockReturnValue("hidden");
    document.dispatchEvent(new Event("visibilitychange"));
    await wrapper.vm.$nextTick();
    expect(vi.getTimerCount()).toBe(0);

    await vi.advanceTimersByTimeAsync(2000);
    visibility.mockReturnValue("visible");
    document.dispatchEvent(new Event("visibilitychange"));
    await wrapper.vm.$nextTick();

    expect(wrapper.get(".clock-date").text()).toContain("January 16");
    expect(wrapper.get(".clock-second-hand").attributes("transform")).toBe("rotate(6 100 100)");
    expect(vi.getTimerCount()).toBe(2);
  });

  it("keeps Quiet's colon solid while normal motion is enabled", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:00Z"));
    const widget = createHomeWidget(HomeWidgetType.Clock, "quiet");
    const wrapper = mountClock(widget);

    await wrapper.vm.$nextTick();
    expect(wrapper.get(".clock-colon").attributes("class")).toBe("clock-colon");

    await vi.advanceTimersByTimeAsync(500);
    expect(wrapper.get(".clock-colon").text()).toBe(":");
    expect(wrapper.get(".clock-colon").attributes("class")).toBe("clock-colon");
  });
});

describe("Home clock readout cadence", () => {
  it("does no repeated time formatting for a minute-only Flip clock until rollover", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-15T10:08:00Z"));
    const widget = createHomeWidget(HomeWidgetType.Clock, "minute-flip");
    widget.options.timeZone = "UTC";
    widget.options.clockStyle = HomeClockStyle.Flip;
    const wrapper = mountClock(widget);
    await wrapper.vm.$nextTick();
    const initial = wrapper.getComponent(HomeClockFlipFace).props("clock");

    await vi.advanceTimersByTimeAsync(59_000);
    expect(wrapper.getComponent(HomeClockFlipFace).props("clock")).toBe(initial);

    await vi.advanceTimersByTimeAsync(1000);
    expect(wrapper.getComponent(HomeClockFlipFace).props("clock")).not.toBe(initial);
    expect(wrapper.get(".clock-time").text()).toContain("09");
  });
});
