import type {
  HomeBoard,
  HomeBoardChanges,
  HomeWidgetFieldChanges,
  HomeWidgetRow,
} from "@/api/home";
import type { HomeLayout, HomeSettings, HomeWidget, HomeWidgetOptions } from "@/types/home";

import { HomeLayoutKind, HomeWidgetType } from "@/enums/home-widget";
import { defaultHomeLayout } from "@/utils/home-layout";
import { cloneHomeWidgetOptions, defaultHomeWidgetOptions } from "@/utils/home-widget-options";
import { homeWidgetDensity } from "@/utils/home-widget-sizing";

const optionFields: Record<HomeWidgetType, readonly (keyof HomeWidgetOptions)[]> = {
  [HomeWidgetType.Greeting]: [
    "greetingStyle",
    "greetingDecoration",
    "greetingColorMode",
    "greetingAlignment",
    "greetingShowName",
    "greetingShowDate",
    "greetingHeading",
    "greetingSubtitle",
  ],
  [HomeWidgetType.Clock]: [
    "timeZone",
    "clockStyle",
    "hourFormat",
    "showSeconds",
    "horizontalAlignment",
    "verticalAlignment",
    "textAlignment",
    "textVerticalAlignment",
  ],
  [HomeWidgetType.LibraryShortcuts]: [
    "libraryStyle",
    "shortcutGroup",
    "shortcuts",
    "featuredDestination",
  ],
  [HomeWidgetType.Storage]: ["storageStyle"],
  [HomeWidgetType.RecentFiles]: ["recentFilesStyle", "recentFilesSource"],
  [HomeWidgetType.QuickAccess]: ["quickAccessStyle"],
  [HomeWidgetType.QuickUpload]: ["quickUploadStyle"],
  [HomeWidgetType.Player]: ["playerStyle"],
  [HomeWidgetType.SystemStatus]: ["statusStyle"],
  [HomeWidgetType.TopSongs]: [],
  [HomeWidgetType.RecentActivity]: ["activityStyle"],
  [HomeWidgetType.TranscodingJobs]: [],
  [HomeWidgetType.LatestRecap]: ["recapStyle", "recapPeriod", "recapPalette"],
  [HomeWidgetType.Collections]: [],
  [HomeWidgetType.Countdown]: ["countdownStyle", "countdownLabel", "countdownTarget"],
  [HomeWidgetType.FocusTimer]: [
    "focusStyle",
    "focusMinutes",
    "shortBreakMinutes",
    "longBreakMinutes",
    "focusSessions",
  ],
  [HomeWidgetType.DailyCover]: ["dailyCoverStyle"],
};

export const homeWidgetToRow = (widget: HomeWidget, layout: HomeLayoutKind): HomeWidgetRow => ({
  id: widget.instanceId,
  layout,
  type: widget.type,
  x: widget.x,
  y: widget.y,
  width: widget.width,
  height: widget.height,
  showTitle: widget.showTitle,
  optionsVersion: 1,
  options: Object.fromEntries(optionFields[widget.type].map((key) => [key, widget.options[key]])),
});

export const homeBoardToSettings = (board: HomeBoard): HomeSettings => {
  const rows = (layout: HomeLayoutKind) =>
    board.widgets
      .filter((row) => row.layout === layout)
      .map((row): HomeWidget => ({
        instanceId: row.id,
        type: row.type,
        x: row.x,
        y: row.y,
        width: row.width,
        height: row.height,
        size: homeWidgetDensity(row.width, row.height, layout === HomeLayoutKind.Desktop ? 12 : 4),
        showTitle: row.showTitle,
        options: cloneHomeWidgetOptions({ ...defaultHomeWidgetOptions(), ...row.options }),
      }));

  let desktop: HomeLayout = { columns: 12, widgets: rows(HomeLayoutKind.Desktop) };

  if (!board.initialized) desktop = defaultHomeLayout();

  return {
    initialized: board.initialized,
    ...board.preferences,
    showHeaderGreeting: board.preferences.showHeaderGreeting ?? true,
    desktop,
    mobile: board.hasMobileLayout ? { columns: 4, widgets: rows(HomeLayoutKind.Mobile) } : null,
  };
};

const widgetFields = ["x", "y", "width", "height", "showTitle"] as const;

const diffWidget = (before: HomeWidget, after: HomeWidget): HomeWidgetFieldChanges => {
  const changes: HomeWidgetFieldChanges = Object.fromEntries(
    widgetFields.filter((key) => before[key] !== after[key]).map((key) => [key, after[key]]),
  );
  const options = Object.fromEntries(
    optionFields[after.type]
      .filter((key) => JSON.stringify(before.options[key]) !== JSON.stringify(after.options[key]))
      .map((key) => [key, after.options[key]]),
  );

  if (Object.keys(options).length) changes.options = options;

  return changes;
};

export const buildHomeChanges = (baseline: HomeSettings, draft: HomeSettings): HomeBoardChanges => {
  const batch: HomeBoardChanges = {
    creates: [],
    updates: [],
    deletes: [],
    initializeMobileLayout: baseline.mobile === null && draft.mobile !== null,
  };
  const layouts = [
    { kind: HomeLayoutKind.Desktop, before: baseline.desktop, after: draft.desktop },
    { kind: HomeLayoutKind.Mobile, before: baseline.mobile, after: draft.mobile },
  ];

  for (const { kind, before, after } of layouts) {
    const previous = new Map<string, HomeWidget>();

    if (baseline.initialized) {
      for (const widget of before?.widgets ?? []) previous.set(widget.instanceId, widget);
    }

    for (const widget of after?.widgets ?? []) {
      const original = previous.get(widget.instanceId);

      if (original) {
        const changes = diffWidget(original, widget);

        if (Object.keys(changes).length) batch.updates.push({ id: widget.instanceId, changes });
      } else {
        batch.creates.push(homeWidgetToRow(widget, kind));
      }

      previous.delete(widget.instanceId);
    }

    batch.deletes.push(...previous.keys());
  }

  const preferences = Object.fromEntries(
    (["useSeparateMobileLayout", "showWidgetTitles", "showHeaderGreeting"] as const)
      .filter((key) => baseline[key] !== draft[key])
      .map((key) => [key, draft[key]]),
  );

  if (Object.keys(preferences).length) batch.preferences = preferences;

  return batch;
};
