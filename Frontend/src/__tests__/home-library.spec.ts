import { describe, expect, it } from "vitest";

import type { HomeBoard } from "@/api/home";

import {
  HomeLibraryDestination,
  HomeLibraryStyle,
  HomeShortcutGroup,
  HomeWidgetType,
} from "@/enums/home-widget";
import { cloneHomeSettings, setSeparateMobileLayout } from "@/utils/home-layout";
import {
  HOME_LIBRARY_DESTINATIONS,
  homeLibraryCustomChange,
  homeLibraryDisplayOrder,
  homeLibraryLinks,
  homeLibraryPresetChange,
  homeLibrarySelection,
} from "@/utils/home-library";
import { homeWidgetHasStyles, homeWidgetStyleLabel } from "@/utils/home-widget-editor";
import { defaultHomeWidgetOptions } from "@/utils/home-widget-options";
import { buildHomeChanges, homeBoardToSettings } from "@/utils/home-widget-persistence";

const existingBoard = (): HomeBoard => ({
  initialized: true,
  hasMobileLayout: false,
  preferences: { useSeparateMobileLayout: false, showWidgetTitles: false },
  widgets: [
    {
      id: "11111111-1111-4111-8111-111111111111",
      layout: 0,
      type: HomeWidgetType.LibraryShortcuts,
      x: 0,
      y: 0,
      width: 6,
      height: 4,
      showTitle: null,
      optionsVersion: 1,
      options: { shortcutGroup: HomeShortcutGroup.Media },
    },
  ],
});

describe("Library shortcut preferences", () => {
  it("defaults existing rows without rewriting their group or producing a save", () => {
    const settings = homeBoardToSettings(existingBoard());
    const widget = settings.desktop.widgets[0]!;

    expect(widget.options.libraryStyle).toBe(HomeLibraryStyle.Launchpad);
    expect(widget.options.shortcutGroup).toBe(HomeShortcutGroup.Media);
    expect(widget.options.shortcuts).toEqual([]);
    expect(widget.options.featuredDestination).toBeNull();
    expect(buildHomeChanges(settings, cloneHomeSettings(settings)).updates).toEqual([]);
    expect(homeWidgetHasStyles(widget.type)).toBe(true);
    expect(homeWidgetStyleLabel(widget)).toBe("Launchpad");
  });

  it("sends only the changed style while keeping the mobile copy independent", () => {
    const baseline = homeBoardToSettings(existingBoard());
    setSeparateMobileLayout(baseline, true);
    const draft = cloneHomeSettings(baseline);
    draft.desktop.widgets[0]!.options.libraryStyle = HomeLibraryStyle.Icons;

    expect(draft.mobile!.widgets[0]!.options.libraryStyle).toBe(HomeLibraryStyle.Launchpad);
    expect(draft.desktop.widgets[0]!.options.shortcutGroup).toBe(HomeShortcutGroup.Media);
    expect(buildHomeChanges(baseline, draft).updates).toEqual([
      {
        id: baseline.desktop.widgets[0]!.instanceId,
        changes: { options: { libraryStyle: HomeLibraryStyle.Icons } },
      },
    ]);

    draft.mobile!.widgets[0]!.options.libraryStyle = HomeLibraryStyle.Directory;
    expect(draft.desktop.widgets[0]!.options.libraryStyle).toBe(HomeLibraryStyle.Icons);
    expect(homeWidgetStyleLabel(draft.desktop.widgets[0]!)).toBe("Icons");
    expect(homeWidgetStyleLabel(draft.mobile!.widgets[0]!)).toBe("Directory");
  });

  it("retains file navigation with streaming disabled and offers media links when enabled", () => {
    const options = defaultHomeWidgetOptions();

    expect(homeLibraryLinks(options, false).map((link) => link.to)).toEqual([
      "/dashboard",
      "/dashboard/tags",
      "/access-history",
      "/my-storage",
    ]);
    options.shortcutGroup = HomeShortcutGroup.Media;

    expect(homeLibraryLinks(options, false)).toEqual([]);
    expect(homeLibraryLinks(options, true).map((link) => link.to)).toEqual([
      "/streaming/music",
      "/streaming/videos",
      "/streaming/playlists",
      "/stats",
    ]);
  });

  it("offers fourteen unique allowlisted destinations with working listening anchors", () => {
    expect(HOME_LIBRARY_DESTINATIONS).toHaveLength(14);
    expect(new Set(HOME_LIBRARY_DESTINATIONS.map((target) => target.destination)).size).toBe(14);
    expect(
      HOME_LIBRARY_DESTINATIONS.find(
        (target) => target.destination === HomeLibraryDestination.Timeline,
      )?.to,
    ).toBe("/stats#timeline");
    expect(
      HOME_LIBRARY_DESTINATIONS.find(
        (target) => target.destination === HomeLibraryDestination.Recaps,
      )?.to,
    ).toBe("/stats#recaps");
  });

  it("keeps saved custom choices and labels when streaming temporarily hides media", () => {
    const options = {
      ...defaultHomeWidgetOptions(),
      shortcutGroup: HomeShortcutGroup.Custom,
      shortcuts: [
        { destination: HomeLibraryDestination.Music, label: " Listen " },
        { destination: HomeLibraryDestination.Trash, label: null },
        { destination: HomeLibraryDestination.FileExplorer, label: "Files" },
      ],
      featuredDestination: HomeLibraryDestination.Music,
    };
    const saved = JSON.stringify(options);

    expect(homeLibraryLinks(options, false).map((link) => link.label)).toEqual(["Trash", "Files"]);
    expect(homeLibraryLinks(options, true).map((link) => link.label)).toEqual([
      "Listen",
      "Trash",
      "Files",
    ]);
    expect(JSON.stringify(options)).toBe(saved);
    expect(
      homeLibraryDisplayOrder(
        homeLibraryLinks(options, false),
        HomeLibraryStyle.Spotlight,
        options.featuredDestination,
      )[0]?.destination,
    ).toBe(HomeLibraryDestination.Trash);
  });

  it("switches presets without stale overrides and clears an unavailable feature", () => {
    const options = defaultHomeWidgetOptions();
    options.featuredDestination = HomeLibraryDestination.Storage;
    const change = homeLibraryPresetChange(options, HomeShortcutGroup.Mixed);

    Object.assign(options, change);
    expect(change).toEqual({
      shortcutGroup: HomeShortcutGroup.Mixed,
      shortcuts: [],
      featuredDestination: null,
    });
    expect(homeLibrarySelection(options).map((item) => item.destination)).toEqual([0, 6, 1, 8]);

    const custom = homeLibraryPresetChange(options, HomeShortcutGroup.Custom);
    expect(custom.shortcuts).toHaveLength(4);
    Object.assign(options, custom);
    options.shortcuts[0]!.label = "My files";

    expect(homeLibraryPresetChange(options, HomeShortcutGroup.Files)).toEqual({
      shortcutGroup: HomeShortcutGroup.Files,
      shortcuts: [],
      featuredDestination: null,
    });
  });

  it("features a selected target only in Spotlight without mutating custom order", () => {
    const options = defaultHomeWidgetOptions();
    const links = homeLibraryLinks(options, true);

    expect(
      homeLibraryDisplayOrder(links, HomeLibraryStyle.Directory, HomeLibraryDestination.Storage),
    ).toBe(links);
    expect(
      homeLibraryDisplayOrder(links, HomeLibraryStyle.Launchpad, HomeLibraryDestination.Storage),
    ).toBe(links);
    expect(
      homeLibraryDisplayOrder(links, HomeLibraryStyle.Icons, HomeLibraryDestination.Storage),
    ).toBe(links);
    expect(
      homeLibraryDisplayOrder(
        links,
        HomeLibraryStyle.Spotlight,
        HomeLibraryDestination.Storage,
      ).map((link) => link.destination),
    ).toEqual([3, 0, 1, 2]);
    expect(links.map((link) => link.destination)).toEqual([0, 1, 2, 3]);
  });

  it("clears a removed featured target in the same customization patch", () => {
    const options = defaultHomeWidgetOptions();
    options.featuredDestination = HomeLibraryDestination.Storage;
    const selection = homeLibrarySelection(options);

    expect(homeLibraryCustomChange(options, selection).featuredDestination).toBe(
      HomeLibraryDestination.Storage,
    );
    expect(homeLibraryCustomChange(options, selection.slice(0, 2))).toEqual({
      shortcutGroup: HomeShortcutGroup.Custom,
      shortcuts: selection.slice(0, 2),
      featuredDestination: null,
    });
  });

  it("isolates nested labels between server rows, baseline, desktop draft and mobile draft", () => {
    const board = existingBoard();
    board.widgets[0]!.options = {
      shortcutGroup: HomeShortcutGroup.Custom,
      shortcuts: [{ destination: HomeLibraryDestination.Recaps, label: "Highlights" }],
      featuredDestination: HomeLibraryDestination.Recaps,
    };
    const baseline = homeBoardToSettings(board);
    setSeparateMobileLayout(baseline, true);
    const draft = cloneHomeSettings(baseline);
    draft.desktop.widgets[0]!.options.shortcuts[0]!.label = "Memories";

    expect(baseline.desktop.widgets[0]!.options.shortcuts[0]!.label).toBe("Highlights");
    expect(baseline.mobile!.widgets[0]!.options.shortcuts[0]!.label).toBe("Highlights");
    expect(draft.mobile!.widgets[0]!.options.shortcuts[0]!.label).toBe("Highlights");
    expect(board.widgets[0]!.options.shortcuts).toEqual([{ destination: 13, label: "Highlights" }]);
    expect(buildHomeChanges(baseline, draft).updates).toEqual([
      {
        id: baseline.desktop.widgets[0]!.instanceId,
        changes: { options: { shortcuts: [{ destination: 13, label: "Memories" }] } },
      },
    ]);
  });
});
