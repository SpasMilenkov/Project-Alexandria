import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { describe, expect, it, vi } from "vitest";

import { useHomeFocusTimer } from "@/composables/useHomeFocusTimer";
import { HomeLayoutKind, HomeWidgetType } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { homeDailyCover } from "@/utils/home-daily-cover";
import {
  homeFocusIdle,
  homeFocusNext,
  homeFocusRemaining,
  homeFocusToggle,
  parseHomeFocusState,
} from "@/utils/home-focus";
import { homeCalendarDate, homeCountdown } from "@/utils/home-general";
import { createHomeWidget } from "@/utils/home-layout";
import { defaultHomeWidgetOptions } from "@/utils/home-widget-options";
import { homeWidgetToRow } from "@/utils/home-widget-persistence";

const plan = defaultHomeWidgetOptions();

describe("General Home widgets", () => {
  it("starts a browser-local timer and isolates it when the owner changes", async () => {
    const pinia = createPinia();
    const home = useHomeStore(pinia);
    home.setOwner("timer-owner");
    let timer: ReturnType<typeof useHomeFocusTimer> | undefined = undefined;
    const widget = createHomeWidget(HomeWidgetType.FocusTimer, "local-timer");
    const wrapper = mount(
      {
        setup: () => {
          timer = useHomeFocusTimer(() => widget);
          return () => null;
        },
      },
      { global: { plugins: [pinia] } },
    );

    try {
      timer!.toggle();
      expect(timer!.state.value.status).toBe("running");
      expect(
        JSON.parse(localStorage.getItem("alexandria:home-focus:timer-owner:local-timer")!).status,
      ).toBe("running");
      home.setOwner("other-owner");
      await vi.waitFor(() => expect(timer!.state.value.status).toBe("idle"));
      expect(localStorage.getItem("alexandria:home-focus:other-owner:local-timer")).toBeNull();
    } finally {
      wrapper.unmount();
      localStorage.removeItem("alexandria:home-focus:timer-owner:local-timer");
    }
  });

  it("counts local calendar days across seasonal clock changes and keeps Today all day", () => {
    expect(homeCountdown("2026-03-30", new Date(2026, 2, 28, 12))?.days).toBe(2);
    expect(homeCountdown("2026-10-26", new Date(2026, 9, 24, 12))?.days).toBe(2);
    expect(homeCountdown("2026-10-09", new Date(2026, 9, 9, 23, 59))?.value).toBe("Today");
    expect(homeCountdown("2026-10-09", new Date(2026, 9, 12, 0, 1))).toMatchObject({
      value: "3",
      unit: "days ago",
      progress: 1,
    });
    expect(homeCountdown("2026-10-10", new Date(2026, 9, 9, 12))).toMatchObject({
      days: 1,
      fullDays: 0,
      hours: 12,
    });
  });

  it("rejects impossible dates and accepts leap days without rolling them into another month", () => {
    expect(homeCalendarDate("2026-02-30")).toBeNull();
    expect(homeCalendarDate("2026-02-29")).toBeNull();
    expect(homeCalendarDate("0000-01-01")).toBeNull();
    expect(homeCalendarDate("2028-02-29")?.getDate()).toBe(29);
    expect(homeCalendarDate("0001-01-01")?.getFullYear()).toBe(1);
    expect(homeCountdown(null, new Date())).toBeNull();
  });

  it("recovers from a deadline, pauses exactly and resumes without consuming paused time", () => {
    const running = homeFocusToggle(homeFocusIdle(plan), 1000);
    const recovered = parseHomeFocusState(JSON.parse(JSON.stringify(running)));

    expect(homeFocusRemaining(recovered!, 31_000)).toBe(1470);
    const paused = homeFocusToggle(running, 31_000);
    expect(paused).toMatchObject({ status: "paused", remaining: 1470, endsAt: null });
    expect(homeFocusRemaining(paused, 300_000)).toBe(1470);
    expect(homeFocusToggle(paused, 300_000).endsAt).toBe(1_770_000);
    expect(homeFocusRemaining(running, 2_000_000)).toBe(0);
  });

  it("starts phases manually, takes a long break after the final session and snapshots the plan", () => {
    const options = { ...plan, focusSessions: 2 };
    const first = homeFocusIdle(options);
    options.focusMinutes = 50;
    expect(first.plan.focusMinutes).toBe(25);

    const shortBreak = homeFocusNext(first);
    expect(shortBreak).toMatchObject({
      phase: "shortBreak",
      status: "idle",
      remaining: 300,
      session: 1,
    });
    const second = homeFocusNext(shortBreak);
    expect(second).toMatchObject({ phase: "focus", session: 2 });
    const longBreak = homeFocusNext(second);
    expect(longBreak).toMatchObject({ phase: "longBreak", remaining: 900 });
    expect(homeFocusNext(longBreak)).toMatchObject({ phase: "focus", session: 1 });
  });

  it("discards corrupted local timers instead of recovering invalid phases or durations", () => {
    const state = homeFocusIdle(plan);
    expect(parseHomeFocusState(state)).toEqual(state);
    expect(parseHomeFocusState({ ...state, session: 99 })).toBeNull();
    expect(parseHomeFocusState({ ...state, status: "running", endsAt: null })).toBeNull();
    expect(parseHomeFocusState({ ...state, plan: { ...plan, focusMinutes: 0 } })).toBeNull();
    expect(parseHomeFocusState({ ...state, remaining: Infinity })).toBeNull();
  });

  it("generates identical artwork for a date and changes it on the next day", () => {
    expect(homeDailyCover("2026-10-09")).toEqual(homeDailyCover("2026-10-09"));
    expect(homeDailyCover("2026-10-10")).not.toEqual(homeDailyCover("2026-10-09"));
    expect(homeDailyCover("2026-10-09", 3)).not.toEqual(homeDailyCover("2026-10-09"));
  });

  it("persists only each widget's settings, with no runtime or generated artwork", () => {
    const countdown = createHomeWidget(HomeWidgetType.Countdown, "countdown");
    countdown.options.countdownTarget = "2026-10-21";
    expect(homeWidgetToRow(countdown, HomeLayoutKind.Desktop).options).toEqual({
      countdownStyle: 0,
      countdownLabel: "Countdown",
      countdownTarget: "2026-10-21",
    });
    expect(
      homeWidgetToRow(createHomeWidget(HomeWidgetType.FocusTimer, "focus"), HomeLayoutKind.Mobile)
        .options,
    ).toEqual({
      focusStyle: 0,
      focusMinutes: 25,
      shortBreakMinutes: 5,
      longBreakMinutes: 15,
      focusSessions: 4,
    });
    expect(
      homeWidgetToRow(createHomeWidget(HomeWidgetType.DailyCover, "cover"), HomeLayoutKind.Desktop)
        .options,
    ).toEqual({ dailyCoverStyle: 0 });
  });
});
