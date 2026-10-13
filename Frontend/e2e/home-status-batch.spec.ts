import { type Locator, type Page, type Route, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  closeWidgetSettings,
  dashboardSettings,
  openWidgetTab,
  selectWidget,
} from "./helpers/home-grid-editor.js";

const variants = [
  { id: "beacon", type: 7, statusStyle: 0, activityStyle: 0 },
  { id: "services", type: 7, statusStyle: 1, activityStyle: 0 },
  { id: "history", type: 7, statusStyle: 2, activityStyle: 0 },
  { id: "songs", type: 8, statusStyle: 0, activityStyle: 0 },
  { id: "feed", type: 9, statusStyle: 0, activityStyle: 0 },
  { id: "days", type: 9, statusStyle: 0, activityStyle: 1 },
];

const gallery = (width = 6, height = 4) => {
  const value = seed();
  const original = value.desktop.widgets[0]!;
  const perRow = Math.floor(12 / width);
  value.desktop.widgets = variants.map((variant, index) => ({
    ...original,
    type: variant.type,
    instanceId: variant.id,
    x: (index % perRow) * width,
    y: Math.floor(index / perRow) * height,
    width,
    height,
    options: {
      ...original.options,
      statusStyle: variant.statusStyle,
      activityStyle: variant.activityStyle,
    },
  }));

  return value;
};

const songs = () =>
  [12, 7, 3, 2, 1].map((qualifiedPlayCount, index) => ({
    qualifiedPlayCount,
    totalListenedSeconds: qualifiedPlayCount * 180,
    file: {
      fileId: `song-${index}`,
      fileName: `evening-song-${index}.mp3`,
      mimeType: "audio/mpeg",
      currentVersionId: `current-${index}`,
      playbackVersionId: `playback-${index}`,
      duration: 180,
      title: `Evening song ${index + 1}`,
      artist: "The Night Ensemble",
      album: "After hours",
      genre: null,
      year: null,
      transpilationJobId: `job-${index}`,
      isVideo: false,
      segmentPrefix: null,
    },
  }));

const events = () =>
  Array.from({ length: 12 }, (_, index) => ({
    operationType: index % 2 === 0 ? 1 : 2,
    entityType: 0,
    userId: "home-fixture",
    entityId: `activity-file-${index}`,
    eventCode: index % 2 === 0 ? 0 : 1,
    metadata: JSON.stringify({ name: `Report-${index}.pdf`, old: "Old.pdf", new: "Renamed.pdf" }),
    timestamp: new Date(Date.UTC(2026, 9, 8, 12) - index * 12 * 3_600_000).toISOString(),
    logSource: 2,
  }));

const batchFixture = async (
  page: Page,
  value = gallery(),
  flags: { fail?: boolean; empty?: boolean; thumbnails?: boolean; wait?: Promise<void> } = {},
) => {
  const requests = {
    status: 0,
    history: 0,
    songs: [] as URL[],
    activity: [] as URL[],
    thumbnails: [] as string[],
  };

  await page.clock.install({ time: new Date("2026-10-08T14:00:00Z") });

  const handler = async (route: Route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const feed = [
      "/api/status",
      "/api/status/history",
      "/api/stats/top-songs",
      "/api/activity/user",
    ].includes(path);

    if (feed && flags.wait) await flags.wait;

    if (feed && flags.fail) {
      await route.fulfill({ status: 503, json: { message: "Unavailable" } });

      return true;
    }

    if (path === "/api/status") {
      requests.status += 1;
      await route.fulfill({
        json: Array.from({ length: 7 }, (_, serviceType) => ({ serviceType, status: "Healthy" })),
      });
    } else if (path === "/api/status/history") {
      requests.history += 1;
      await route.fulfill({
        json: Array.from({ length: 7 }, (_, serviceType) => ({
          serviceType,
          days: Array.from({ length: 30 }, (entry, index) => ({
            date: new Date(Date.UTC(2026, 8, 9) + index * 86_400_000).toISOString().slice(0, 10),
            status: serviceType === 0 && index === 29 ? "Down" : "Healthy",
          })),
        })),
      });
    } else if (path === "/api/stats/top-songs") {
      requests.songs.push(url);
      await route.fulfill({
        json: {
          from: url.searchParams.get("from"),
          to: url.searchParams.get("to"),
          items: flags.empty ? [] : songs(),
        },
      });
    } else if (path === "/api/activity/user") {
      requests.activity.push(url);
      const items = flags.empty ? [] : events();
      await route.fulfill({
        json: { items, totalCount: items.length, currentPage: 1, pageSize: 20, totalPages: 1 },
      });
    } else if (path.endsWith("/thumbnail")) {
      requests.thumbnails.push(path);
      await route.fulfill({
        contentType: "image/svg+xml",
        body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="#527a79"/><circle cx="50" cy="50" r="28" fill="#d8c9a7"/></svg>',
      });
    } else if (path.startsWith("/api/streaming/manifest/")) {
      await route.fulfill({ contentType: "application/vnd.apple.mpegurl", body: "" });
    } else {
      return false;
    }

    return true;
  };

  const state = await fixture(
    page,
    value,
    { thumbnailsEnabled: flags.thumbnails ?? true },
    undefined,
    handler,
  );

  return { state, requests };
};

const fits = async (card: Locator) => {
  await expect
    .poll(() =>
      card.evaluate(
        (element) =>
          element.scrollHeight <= element.clientHeight + 1 &&
          element.scrollWidth <= element.clientWidth + 1,
      ),
    )
    .toBe(true);
};

for (const dark of [false, true]) {
  for (const size of [
    { width: 2, height: 2 },
    { width: 6, height: 2 },
    { width: 2, height: 6 },
    { width: 6, height: 4 },
    { width: 12, height: 4 },
  ]) {
    test(`status batch fits ${size.width} by ${size.height} in ${dark ? "dark" : "light"} mode`, async ({
      page,
    }) => {
      await page.emulateMedia({ colorScheme: dark ? "dark" : "light", reducedMotion: "reduce" });
      await page.setViewportSize({ width: 1440, height: 1000 });
      await batchFixture(page, gallery(size.width, size.height));

      await expect(page.locator('[data-widget-id$="beacon"]')).toContainText("No active incidents");
      await expect(page.locator('[data-widget-id$="songs"]')).toContainText("Evening song 1");
      await expect(page.locator('[data-widget-id$="feed"]')).toContainText("Report-0.pdf");

      const serviceList = page.locator('[data-widget-id$="beacon"] .status-service-list');

      if (size.width === 2 && size.height === 2) {
        await expect(serviceList).toHaveCount(0);

        if (dark) {
          await page.locator('[data-widget-id$="beacon"]').screenshot({
            path: "/tmp/alexandria-status-beacon-compact.png",
          });
        }
      } else {
        await expect(serviceList).toBeVisible();
      }

      for (const variant of variants) await fits(page.locator(`[data-widget-id$="${variant.id}"]`));

      if (size.width === 6 && size.height === 4) {
        await page.screenshot({
          path: `/tmp/alexandria-status-batch-${dark ? "dark" : "light"}.png`,
          fullPage: true,
        });
      }
    });
  }
}

test("styles and titles save independently for desktop and mobile", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = gallery();
  value.useSeparateMobileLayout = true;
  value.mobile = {
    columns: 4,
    widgets: value.desktop.widgets.map((widget, index) => ({
      ...widget,
      instanceId: `mobile-${widget.instanceId}`,
      x: 0,
      y: index * 4,
      width: 4,
    })),
  };
  const { state } = await batchFixture(page, value);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, page.locator('[data-widget-id$="beacon"]'));
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "History strip", exact: true }).click();
  await closeWidgetSettings(page);
  await selectWidget(page, page.locator('[data-widget-id$="feed"]'));
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "By day", exact: true }).click();
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Show widget titles", exact: true }).click();

  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.statusStyle).toBe(2);
  expect(state.saved.desktop.widgets[4]!.options.activityStyle).toBe(1);
  expect(state.saved.mobile!.widgets[0]!.options.statusStyle).toBe(0);
  expect(state.saved.mobile!.widgets[4]!.options.activityStyle).toBe(0);

  await page.reload();
  await expect(page.locator('[data-widget-id$="beacon"] header')).toContainText("System status");
});

test("mobile cards fit and queries stay bounded", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const value = gallery(2, 2);
  value.useSeparateMobileLayout = true;
  value.mobile = {
    columns: 4,
    widgets: value.desktop.widgets.map((widget, index) => ({
      ...widget,
      instanceId: `mobile-${widget.instanceId}`,
      x: (index % 2) * 2,
      y: Math.floor(index / 2) * 2,
    })),
  };
  const { requests } = await batchFixture(page, value);

  await expect(page.locator('[data-widget-id$="songs"]')).toContainText("Evening song 1");
  await expect(page.locator('[data-widget-id$="feed"]')).toContainText("Report-0.pdf");
  for (const variant of variants) await fits(page.locator(`[data-widget-id$="${variant.id}"]`));

  await expect(page.locator('[data-widget-id$="beacon"] .status-service-list')).toHaveCount(0);

  expect(requests.activity[0]!.searchParams.get("pageSize")).toBe("20");
  expect(requests.activity[0]!.searchParams.get("userId")).toBe("home-fixture");
  expect(requests.songs[0]!.searchParams.get("limit")).toBe("5");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: "/tmp/alexandria-status-batch-mobile.png", fullPage: true });
});

test("loading, unavailable and empty states fit the compact cards", async ({ page }) => {
  let release = () => {};
  const wait = new Promise<void>((resolve) => {
    release = resolve;
  });
  await batchFixture(page, gallery(2, 2), { wait, empty: true });
  await expect(page.getByRole("status", { name: "Loading widget data" }).first()).toBeVisible();

  release();
  await expect(page.locator('[data-widget-id$="songs"]')).toContainText("No songs played yet");
  await expect(page.locator('[data-widget-id$="feed"]')).toContainText("No recent activity");
  for (const variant of variants) await fits(page.locator(`[data-widget-id$="${variant.id}"]`));
});

test("unavailable data displays unknown status and offers retry without compact overflow", async ({
  page,
}) => {
  await batchFixture(page, gallery(2, 2), { fail: true });
  await expect(page.locator('[data-widget-id$="beacon"]')).toContainText("Status unknown", {
    timeout: 20_000,
  });
  await expect(
    page.locator('[data-widget-id$="songs"]').getByRole("button", { name: "Try again" }),
  ).toBeVisible();

  for (const variant of variants) await fits(page.locator(`[data-widget-id$="${variant.id}"]`));
});

for (const thumbnails of [true, false]) {
  test(`Top songs honors thumbnails ${thumbnails} and shares its query between cards`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    const value = gallery();
    const song = value.desktop.widgets.find((widget) => widget.type === 8)!;
    value.desktop.widgets = [song, { ...song, instanceId: "songs-second", x: 0, y: 0 }];
    const { requests } = await batchFixture(page, value, { thumbnails });

    await expect(page.locator('[data-widget-id$="songs"]')).toContainText("Evening song 1");
    await expect(page.locator('[data-widget-id$="songs-second"]')).toContainText("Evening song 1");
    expect(requests.songs).toHaveLength(1);

    if (thumbnails) {
      await expect.poll(() => requests.thumbnails.length).toBeGreaterThan(0);
      expect(requests.thumbnails.every((path) => path.includes("/versions/playback-"))).toBe(true);
    } else {
      expect(requests.thumbnails).toHaveLength(0);
    }
  });
}

test("Top songs uses the shared player and disables playback while arranging", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await batchFixture(page);
  const song = page
    .locator('[data-widget-id$="songs"]')
    .getByRole("button", { name: /^Play Evening song 1 /u });

  await expect(song).toBeEnabled();
  await song.click();
  await expect
    .poll(() =>
      page.evaluate(() => {
        const app = (
          document.getElementById("app") as HTMLElement & {
            __vue_app__: {
              config: {
                globalProperties: {
                  $pinia: { _s: Map<string, { activeFile: { fileId: string } | null }> };
                };
              };
            };
          }
        ).__vue_app__;

        const player = app.config.globalProperties.$pinia._s.get("player");

        return player?.activeFile?.fileId;
      }),
    )
    .toBe("song-0");

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await expect(song).toBeDisabled();
});
