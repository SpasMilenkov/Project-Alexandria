import { createPinia, setActivePinia } from "pinia";
import { describe, expect, it } from "vitest";

import type { HomeBoard } from "@/api/home";

import {
  HomeGreetingColorMode,
  HomeGreetingDecoration,
  HomeGreetingStyle,
  HomeWidgetType,
} from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import {
  HOME_GREETING_DECORATIONS,
  HOME_GREETING_EMOJIS,
  homeGreetingHeading,
} from "@/utils/home-greeting";
import { cloneHomeSettings, createHomeWidget, setSeparateMobileLayout } from "@/utils/home-layout";
import { homeWidgetHasStyles, homeWidgetStyleLabel } from "@/utils/home-widget-editor";
import { defaultHomeWidgetOptions } from "@/utils/home-widget-options";
import {
  buildHomeChanges,
  homeBoardToSettings,
  homeWidgetToRow,
} from "@/utils/home-widget-persistence";

const board = (): HomeBoard => ({
  initialized: true,
  hasMobileLayout: false,
  preferences: { useSeparateMobileLayout: false, showWidgetTitles: false },
  widgets: [],
});

describe("Greeting widget", () => {
  it.each([
    [0, "Good night"],
    [4, "Good night"],
    [5, "Good morning"],
    [11, "Good morning"],
    [12, "Good afternoon"],
    [17, "Good afternoon"],
    [18, "Good evening"],
    [23, "Good evening"],
  ])("follows device-local time at hour %i", (hour, expected) => {
    const timestamp = new Date(2026, 9, 10, hour).getTime();

    expect(homeGreetingHeading(timestamp, defaultHomeWidgetOptions(), " Alex ")).toBe(
      `${expected}, Alex`,
    );
  });

  it("handles a custom heading, optional name, missing user and blank override", () => {
    const timestamp = new Date(2026, 9, 10, 10).getTime();
    const options = defaultHomeWidgetOptions();
    options.greetingHeading = "  Welcome back  ";

    expect(homeGreetingHeading(timestamp, options, "Alex")).toBe("Welcome back, Alex");
    expect(homeGreetingHeading(timestamp, options, undefined)).toBe("Welcome back");
    options.greetingShowName = false;
    expect(homeGreetingHeading(timestamp, options, "Alex")).toBe("Welcome back");
    options.greetingHeading = "  ";
    expect(homeGreetingHeading(timestamp, options, "Alex")).toBe("Good morning");
  });

  it("defaults older boards without inserting a greeting or producing changes", () => {
    const settings = homeBoardToSettings(board());

    expect(settings.showHeaderGreeting).toBe(true);
    expect(settings.desktop.widgets).toEqual([]);
    expect(buildHomeChanges(settings, cloneHomeSettings(settings))).toMatchObject({
      creates: [],
      updates: [],
      deletes: [],
    });
    expect(buildHomeChanges(settings, cloneHomeSettings(settings)).preferences).toBeUndefined();
  });

  it("sends only greeting options on create and keeps its two styles discoverable", () => {
    const widget = createHomeWidget(HomeWidgetType.Greeting, crypto.randomUUID());
    const row = homeWidgetToRow(widget, 0);

    expect(row.options).toEqual({
      greetingStyle: HomeGreetingStyle.Minimal,
      greetingDecoration: HomeGreetingDecoration.WaveEmoji,
      greetingColorMode: HomeGreetingColorMode.Monochrome,
      greetingAlignment: 0,
      greetingShowName: true,
      greetingShowDate: false,
      greetingHeading: null,
      greetingSubtitle: "Everything in your library, arranged your way.",
    });
    expect(homeWidgetHasStyles(widget.type)).toBe(true);
    expect(homeWidgetStyleLabel(widget)).toBe("Minimal");
    widget.options.greetingStyle = HomeGreetingStyle.Welcome;
    expect(homeWidgetStyleLabel(widget)).toBe("Welcome");
  });

  it("retains existing numeric values and offers each decoration exactly once", () => {
    expect([
      HomeGreetingDecoration.None,
      HomeGreetingDecoration.WaveEmoji,
      HomeGreetingDecoration.CoffeeEmoji,
      HomeGreetingDecoration.BooksEmoji,
    ]).toEqual([0, 1, 2, 3]);
    expect(HOME_GREETING_DECORATIONS.map((choice) => choice.value).sort((a, b) => a - b)).toEqual(
      Array.from({ length: 19 }, (_, index) => index),
    );
    expect(Object.values(HOME_GREETING_EMOJIS)).toEqual([
      "👋",
      "☕",
      "📚",
      "☀️",
      "🌙",
      "🪴",
      "🐈",
      "🐕",
      "🦆",
      "💿",
    ]);
  });

  it.each([HomeGreetingColorMode.Monochrome, HomeGreetingColorMode.Colored])(
    "persists artwork mode %i separately from mobile and defaults older records to monochrome",
    (mode) => {
      const source = board();
      source.widgets.push(
        homeWidgetToRow(createHomeWidget(HomeWidgetType.Greeting, crypto.randomUUID()), 0),
      );
      delete source.widgets[0]!.options.greetingColorMode;
      const baseline = homeBoardToSettings(source);
      expect(baseline.desktop.widgets[0]!.options.greetingColorMode).toBe(
        HomeGreetingColorMode.Monochrome,
      );
      setSeparateMobileLayout(baseline, true);
      const draft = cloneHomeSettings(baseline);
      draft.desktop.widgets[0]!.options.greetingColorMode = mode;

      expect(homeWidgetToRow(draft.desktop.widgets[0]!, 0).options.greetingColorMode).toBe(mode);
      expect(draft.mobile!.widgets[0]!.options.greetingColorMode).toBe(
        HomeGreetingColorMode.Monochrome,
      );
      expect(buildHomeChanges(baseline, draft).updates).toEqual(
        mode === HomeGreetingColorMode.Colored
          ? [{ id: source.widgets[0]!.id, changes: { options: { greetingColorMode: mode } } }]
          : [],
      );
    },
  );

  it.each(HOME_GREETING_DECORATIONS)(
    "round trips $label as a numeric choice and patches only that field",
    ({ value }) => {
      const source = board();
      const widget = createHomeWidget(HomeWidgetType.Greeting, crypto.randomUUID());
      widget.options.greetingDecoration = value;
      source.widgets.push(homeWidgetToRow(widget, 0));
      const saved = homeBoardToSettings(source);
      const draft = cloneHomeSettings(saved);

      expect(saved.desktop.widgets[0]!.options.greetingDecoration).toBe(value);
      draft.desktop.widgets[0]!.options.greetingDecoration = HomeGreetingDecoration.WaveEmoji;
      const next = cloneHomeSettings(draft);
      next.desktop.widgets[0]!.options.greetingDecoration = value;

      if (value === HomeGreetingDecoration.WaveEmoji) {
        expect(buildHomeChanges(draft, next).updates).toEqual([]);
      } else {
        expect(buildHomeChanges(draft, next).updates).toEqual([
          { id: widget.instanceId, changes: { options: { greetingDecoration: value } } },
        ]);
      }
    },
  );

  it("saves a selective header toggle and independent desktop and mobile options", () => {
    const value = board();
    value.widgets.push(
      homeWidgetToRow(createHomeWidget(HomeWidgetType.Greeting, crypto.randomUUID()), 0),
    );
    const baseline = homeBoardToSettings(value);
    setSeparateMobileLayout(baseline, true);
    const draft = cloneHomeSettings(baseline);
    draft.showHeaderGreeting = false;
    draft.desktop.widgets[0]!.options.greetingHeading = "Desktop";
    draft.mobile!.widgets[0]!.options.greetingDecoration = HomeGreetingDecoration.BooksEmoji;

    expect(baseline.showHeaderGreeting).toBe(true);
    expect(draft.mobile!.widgets[0]!.options.greetingHeading).toBeNull();
    expect(buildHomeChanges(baseline, draft)).toMatchObject({
      preferences: { showHeaderGreeting: false },
      creates: [],
      updates: [
        {
          id: baseline.desktop.widgets[0]!.instanceId,
          changes: { options: { greetingHeading: "Desktop" } },
        },
        {
          id: baseline.mobile!.widgets[0]!.instanceId,
          changes: { options: { greetingDecoration: 3 } },
        },
      ],
    });
  });

  it("cancels header and greeting drafts without mutating saved settings", () => {
    setActivePinia(createPinia());
    const store = useHomeStore();
    const settings = homeBoardToSettings(board());
    store.setOwner("alex");
    store.hydrate("alex", settings);
    store.beginEditing("desktop");
    store.draft!.showHeaderGreeting = false;
    store.draft!.desktop.widgets.push(
      createHomeWidget(HomeWidgetType.Greeting, crypto.randomUUID()),
    );

    expect(store.isDirty).toBe(true);
    store.cancelEditing();
    expect(store.settings).toEqual(settings);
    expect(store.draft).toBeNull();
  });
});
