import { createPinia, setActivePinia } from "pinia";
import { describe, expect, it } from "vitest";

import type { HomeBoard } from "@/api/home";
import type { HomeSettings } from "@/types/home";

import { HomeLayoutKind, HomeWidgetType } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { cloneHomeSettings, defaultHomeLayout, setSeparateMobileLayout } from "@/utils/home-layout";
import {
  buildHomeChanges,
  homeBoardToSettings,
  homeWidgetToRow,
} from "@/utils/home-widget-persistence";

const settings = (): HomeSettings => ({
  initialized: true,
  showWidgetTitles: false,
  showHeaderGreeting: true,
  useSeparateMobileLayout: false,
  desktop: defaultHomeLayout(),
  mobile: null,
});

const emptyBoard = (initialized: boolean): HomeBoard => ({
  initialized,
  hasMobileLayout: initialized,
  preferences: { useSeparateMobileLayout: false, showWidgetTitles: false },
  widgets: [],
});

describe("Home widget persistence", () => {
  it("sends only edited scalar fields and type-specific options, including a cleared title", () => {
    const baseline = settings();
    baseline.desktop.widgets[0]!.showTitle = true;
    const draft = cloneHomeSettings(baseline);
    const clock = draft.desktop.widgets[0]!;
    clock.x = 6;
    clock.showTitle = null;
    clock.options.timeZone = "UTC";
    clock.options.storageStyle = 1;
    draft.desktop.widgets.pop();

    expect(buildHomeChanges(baseline, draft)).toEqual({
      creates: [],
      updates: [
        { id: clock.instanceId, changes: { x: 6, showTitle: null, options: { timeZone: "UTC" } } },
      ],
      deletes: [baseline.desktop.widgets[1]!.instanceId],
      initializeMobileLayout: false,
    });
  });

  it("keeps a fixed editing baseline when another device's board arrives", () => {
    setActivePinia(createPinia());
    const store = useHomeStore();
    const original = settings();
    store.setOwner("alice");
    store.hydrate("alice", original);
    store.beginEditing("desktop");

    const remote = cloneHomeSettings(original);
    remote.showWidgetTitles = true;
    remote.desktop.widgets[0]!.x = 6;
    remote.desktop.widgets.push({
      ...remote.desktop.widgets[0]!,
      instanceId: crypto.randomUUID(),
      y: 12,
    });
    store.hydrate("alice", remote);

    expect(store.isDirty).toBe(false);
    expect(store.baseline).toEqual(original);
    store.draft!.desktop.widgets[0]!.options.showSeconds = true;

    expect(buildHomeChanges(store.baseline!, store.draft!)).toEqual({
      creates: [],
      updates: [
        {
          id: original.desktop.widgets[0]!.instanceId,
          changes: { options: { showSeconds: true } },
        },
      ],
      deletes: [],
      initializeMobileLayout: false,
    });
    expect(store.settings).toEqual(remote);

    store.cancelEditing();
    expect(store.settings).toEqual(remote);
    expect(store.baseline).toBeNull();
  });

  it("creates virtual starter widgets on the first Done and gives mobile copies distinct GUIDs", () => {
    const baseline = homeBoardToSettings(emptyBoard(false));
    const draft = cloneHomeSettings(baseline);
    setSeparateMobileLayout(draft, true);
    const changes = buildHomeChanges(baseline, draft);
    const ids = changes.creates.map((widget) => widget.id);

    expect(changes.creates).toHaveLength(4);
    expect(new Set(ids).size).toBe(4);
    expect(ids.every((id) => /^[0-9a-f-]{36}$/u.test(id))).toBe(true);
    expect(changes.updates).toEqual([]);
    expect(changes.deletes).toEqual([]);
    expect(changes.preferences).toEqual({ useSeparateMobileLayout: true });
    expect(changes.creates[0]!.options).not.toHaveProperty("storageStyle");
    expect(changes.creates[1]!.options).toEqual({
      libraryStyle: 0,
      shortcutGroup: 0,
      shortcuts: [],
      featuredDestination: null,
    });
  });

  it("preserves deliberately empty layouts and restores common fields from rows", () => {
    const board = emptyBoard(true);
    const empty = homeBoardToSettings(board);

    expect(empty.desktop.widgets).toEqual([]);
    expect(empty.mobile?.widgets).toEqual([]);
    expect(buildHomeChanges(empty, cloneHomeSettings(empty)).creates).toEqual([]);

    const clock = defaultHomeLayout().widgets[0]!;
    board.widgets.push(homeWidgetToRow(clock, HomeLayoutKind.Desktop));
    const restored = homeBoardToSettings(board);

    expect(restored.desktop.widgets[0]).toEqual(clock);
    expect(restored.mobile?.widgets).toEqual([]);
  });

  it("compares recap palettes by value and sends only a changed mix or reset", () => {
    const baseline = settings();
    const recap = baseline.desktop.widgets[0]!;
    recap.type = HomeWidgetType.LatestRecap;
    recap.options.recapPalette = ["#286789", "#4cced4", "#ffe3b1"];

    const draft = cloneHomeSettings(baseline);
    expect(buildHomeChanges(baseline, draft).updates).toEqual([]);

    draft.desktop.widgets[0]!.options.recapPalette![0] = "#123456";
    expect(buildHomeChanges(baseline, draft).updates).toEqual([
      {
        id: recap.instanceId,
        changes: { options: { recapPalette: ["#123456", "#4cced4", "#ffe3b1"] } },
      },
    ]);

    draft.desktop.widgets[0]!.options.recapPalette = null;
    expect(buildHomeChanges(baseline, draft).updates[0]!.changes.options).toEqual({
      recapPalette: null,
    });
  });
});
