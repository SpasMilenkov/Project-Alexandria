import { type Page, type Route, expect } from "@playwright/test";

interface Widget {
  instanceId: string;
  type: number;
  size: number;
  x: number;
  y: number;
  width: number;
  height: number;
  showTitle: boolean | null;
  options: {
    greetingStyle?: number;
    greetingDecoration?: number;
    greetingColorMode?: number;
    greetingAlignment?: number;
    greetingShowName?: boolean;
    greetingShowDate?: boolean;
    greetingHeading?: string | null;
    greetingSubtitle?: string;
    storageStyle?: number;
    recentFilesStyle?: number;
    recentFilesSource?: number;
    quickAccessStyle?: number;
    quickUploadStyle?: number;
    libraryStyle?: number;
    shortcuts?: { destination: number; label: string | null }[];
    featuredDestination?: number | null;
    playerStyle?: number;
    statusStyle?: number;
    activityStyle?: number;
    recapStyle?: number;
    recapPeriod?: number;
    recapPalette?: [string, string, string] | null;
    countdownStyle?: number;
    countdownLabel?: string;
    countdownTarget?: string | null;
    focusStyle?: number;
    focusMinutes?: number;
    shortBreakMinutes?: number;
    longBreakMinutes?: number;
    focusSessions?: number;
    dailyCoverStyle?: number;
    timeZone: string;
    shortcutGroup: number;
    clockStyle?: number;
    hourFormat?: number;
    showSeconds?: boolean;
    horizontalAlignment?: number;
    verticalAlignment?: number;
    textAlignment?: number;
    textVerticalAlignment?: number;
  };
}

interface HomeDocument {
  initialized: boolean;
  useSeparateMobileLayout: boolean;
  showWidgetTitles: boolean;
  showHeaderGreeting?: boolean;
  desktop: { columns: 4 | 12; widgets: Widget[] };
  mobile: { columns: 4 | 12; widgets: Widget[] } | null;
}

export const seed = (): HomeDocument => ({
  initialized: true,
  useSeparateMobileLayout: false,
  showWidgetTitles: false,
  desktop: {
    columns: 12,
    widgets: [
      {
        instanceId: "default-clock",
        type: 0,
        size: 1,
        x: 0,
        y: 0,
        width: 6,
        height: 4,
        showTitle: null,
        options: {
          storageStyle: 0,
          recentFilesStyle: 0,
          recentFilesSource: 0,
          quickAccessStyle: 0,
          quickUploadStyle: 0,
          playerStyle: 0,
          statusStyle: 0,
          activityStyle: 0,
          recapStyle: 0,
          recapPeriod: 0,
          timeZone: "local",
          shortcutGroup: 0,
          clockStyle: 0,
          hourFormat: 0,
          showSeconds: false,
          horizontalAlignment: 0,
          verticalAlignment: 0,
          textAlignment: 0,
          textVerticalAlignment: 2,
        },
      },
      {
        instanceId: "default-library",
        type: 1,
        size: 2,
        x: 0,
        y: 4,
        width: 12,
        height: 6,
        showTitle: null,
        options: {
          storageStyle: 0,
          recentFilesStyle: 0,
          recentFilesSource: 0,
          quickAccessStyle: 0,
          quickUploadStyle: 0,
          playerStyle: 0,
          statusStyle: 0,
          activityStyle: 0,
          recapStyle: 0,
          recapPeriod: 0,
          timeZone: "local",
          shortcutGroup: 0,
          clockStyle: 0,
          hourFormat: 0,
          showSeconds: false,
          horizontalAlignment: 0,
          verticalAlignment: 0,
          textAlignment: 0,
          textVerticalAlignment: 2,
        },
      },
    ],
  },
  mobile: null,
});

interface WidgetRow extends Omit<Widget, "instanceId" | "size"> {
  id: string;
  layout: number;
  optionsVersion: number;
}

interface BoardChanges {
  creates: WidgetRow[];
  updates: { id: string; changes: Partial<Omit<WidgetRow, "id" | "layout" | "type">> }[];
  deletes: string[];
  preferences?: {
    useSeparateMobileLayout?: boolean;
    showWidgetTitles?: boolean;
    showHeaderGreeting?: boolean;
  };
  initializeMobileLayout: boolean;
}

const wireBoard = (document: HomeDocument) => ({
  initialized: document.initialized,
  hasMobileLayout: document.mobile !== null,
  preferences: {
    useSeparateMobileLayout: document.useSeparateMobileLayout,
    showWidgetTitles: document.showWidgetTitles,
    showHeaderGreeting: document.showHeaderGreeting ?? true,
  },
  widgets: [document.desktop, document.mobile].flatMap((layout, index) =>
    (layout?.widgets ?? []).map(({ instanceId, size: _size, ...widget }) => ({
      ...widget,
      id: instanceId,
      layout: index,
      optionsVersion: 1,
    })),
  ),
});

const applyChanges = (document: HomeDocument, batch: BoardChanges): HomeDocument => {
  const next = JSON.parse(JSON.stringify(document)) as HomeDocument;
  next.initialized = true;
  Object.assign(next, batch.preferences);

  if (batch.initializeMobileLayout && !next.mobile) next.mobile = { columns: 4, widgets: [] };

  for (const layout of [next.desktop, next.mobile].filter((value) => value !== null)) {
    layout.widgets = layout.widgets.filter((widget) => !batch.deletes.includes(widget.instanceId));

    for (const widget of layout.widgets) {
      const changes = batch.updates.find((update) => update.id === widget.instanceId)?.changes;

      if (changes) {
        const { options, ...fields } = changes;
        Object.assign(widget, fields);
        Object.assign(widget.options, options);
      }
    }
  }

  for (const { id, layout, optionsVersion: _version, ...widget } of batch.creates) {
    const target = layout === 0 ? next.desktop : next.mobile;

    if (!target) throw new Error("A mobile create must initialize its layout.");

    target.widgets.push({
      ...widget,
      instanceId: id,
      size: 1,
      options: { ...seed().desktop.widgets[0]!.options, ...widget.options },
    });
  }

  for (const layout of [next.desktop, next.mobile]) {
    for (const widget of layout?.widgets ?? []) {
      const width = (widget.width * 12) / layout!.columns;
      const area = width * widget.height;
      widget.size = 1;

      if (area < 24) widget.size = 0;
      if (area >= 72 && width >= 6 && widget.height >= 4) widget.size = 2;
    }
  }

  return next;
};

export const fixture = async (
  page: Page,
  initial: HomeDocument = seed(),
  appearance: Record<string, unknown> = {},
  timeZones?: (route: Route) => Promise<void>,
  apiHandler?: (route: Route) => Promise<boolean>,
) => {
  const state = {
    saved: initial,
    writes: [] as HomeDocument[],
    batches: [] as BoardChanges[],
    failNextSave: false,
  };

  await page.addInitScript(() => {
    Reflect.deleteProperty(Object.getPrototypeOf(navigator), "serviceWorker");
    localStorage.setItem(
      "auth",
      JSON.stringify({
        user: {
          success: true,
          user: { id: "home-fixture", name: "Alex", email: "alex@example.test" },
          userRoles: ["User"],
          onboardingStep: 3,
        },
      }),
    );
  });

  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (apiHandler && (await apiHandler(route))) return;

    let body: unknown = {};

    if (path === "/api/home/widgets") {
      if (route.request().method() === "PATCH") {
        const batch = route.request().postDataJSON() as BoardChanges;
        const proposed = applyChanges(state.saved, batch);
        state.batches.push(batch);
        state.writes.push(proposed);

        if (state.failNextSave) {
          state.failNextSave = false;

          await route.fulfill({ status: 503, json: { message: "Temporarily unavailable" } });

          return;
        }

        state.saved = proposed;
      }

      body = wireBoard(state.saved);
    } else if (path === "/api/settings/home/time-zones") {
      if (timeZones) {
        await timeZones(route);

        return;
      }

      body = [
        "local",
        "UTC",
        "Europe/Bucharest",
        "Asia/Tokyo",
        "Asia/Kathmandu",
        "Pacific/Chatham",
      ];
    } else if (path === "/api/users/onboarding") {
      body = { onboardingStep: 3 };
    } else if (path === "/api/settings/appearance") {
      body = {
        accentColor: "orange",
        backgroundColor: "#f5f5f4",
        backgroundImageKey: null,
        backgroundImageUpdatedAt: null,
        backgroundImageOpacity: 1,
        gridIconSize: 64,
        listIconSize: 24,
        backgroundBlurEnabled: true,
        backgroundBlurAmount: 8,
        disableBlurOnMobile: false,
        transparencyEnabled: true,
        surfaceOpacity: 80,
        thumbnailsEnabled: true,
        fontFamily: "system",
        cornerRadius: 0.5,
        ...appearance,
      };
    } else if (path === "/api/settings/behavior") {
      body = {
        skipDeleteConfirmation: false,
        toastLevel: 0,
        allowAutoTagRegression: false,
        autoPlaylistMinTracks: 10,
      };
    }

    await route.fulfill({ json: body });
  });

  await page.goto("/home");

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  return state;
};
