import { type Page, expect, test } from "@playwright/test";

import { WrappedCardType } from "../src/enums/wrapped-card-type.js";
import { fixture, seed } from "./helpers/home-dashboard.js";

const currentDate = "2026-10-09T12:00:00Z";

const recap = (month: string, now: string, empty = false, daily = true) => {
  const start = new Date(`${month}-01T00:00:00Z`);
  const end = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + 1, 1));
  const through = new Date(Math.min(end.getTime(), Date.parse(now))).toISOString();
  const series = [
    { date: `${month}-03`, seconds: 1200, plays: 2 },
    { date: `${month}-06`, seconds: 2400, plays: 3 },
  ];
  const card = (type: WrappedCardType) => ({
    type,
    headline: "Your month in music",
    subline: "Every listen adds up.",
    entries: [
      { rank: 2, title: "The runner up", subtitle: "10 min", share: 0.2 },
      {
        rank: 1,
        title: "Your favorite with a title that can wrap on smaller screens",
        subtitle: "50 min",
        share: 0.8,
      },
    ],
    visual: {
      colorKey: "daylight",
      saturation: "balanced",
      contrast: "crisp",
      density: 0,
      fillLevel: 0,
      shapeGrammar: "landscape",
    },
    facts: {
      seconds: 3600,
      count: 5,
      share: 1,
      baselineSeconds: 0,
      weights: [],
      series:
        type === WrappedCardType.Bookends
          ? series
          : [{ date: `${month}-01`, seconds: 3600, plays: 5 }],
    },
  });
  const types = [
    WrappedCardType.ListeningTime,
    WrappedCardType.TopArtists,
    WrappedCardType.TopSongs,
  ];

  if (daily) types.push(WrappedCardType.Bookends);

  return {
    from: start.toISOString(),
    to: through,
    generatedAt: through,
    visualIdentity: "monthly-recap",
    schemaVersion: 3,
    deck: { cards: empty ? [] : types.map(card) },
    summary: {
      seconds: empty ? 0 : 3600,
      sessions: 17,
      qualifiedPlayCount: 5,
      tracks: 12,
      artists: 4,
      activeDays: 2,
      knownArtistShare: 1,
      hasPriorHistory: true,
    },
  };
};

const openListening = async (
  page: Page,
  options: {
    now?: string;
    empty?: boolean;
    missingDaily?: boolean;
    holdCurrent?: boolean;
    failCurrent?: boolean;
    failArchive?: boolean;
  } = {},
) => {
  const now = options.now ?? currentDate;
  const current = now.slice(0, 7);
  const requests: URL[] = [];
  let failed = Boolean(options.failCurrent);
  let release = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });

  await page.clock.setFixedTime(new Date(now));
  await fixture(page, seed(), {}, undefined, async (route) => {
    const url = new URL(route.request().url());

    if (!url.pathname.startsWith("/api/stats/")) return false;

    requests.push(url);

    if (url.pathname === "/api/stats/summaries") {
      await route.fulfill({ status: options.failArchive ? 500 : 200, json: [] });
    } else if (url.pathname === "/api/stats/wrapped") {
      const month = url.searchParams.get("from")!.slice(0, 7);

      if (month === current && options.holdCurrent) await held;

      if (month === current && failed) {
        await route.fulfill({ status: 500, json: { message: "Unavailable" } });
      } else {
        await route.fulfill({ json: recap(month, now, options.empty, !options.missingDaily) });
      }
    } else if (url.pathname === "/api/stats/timeline") {
      const to = url.searchParams.get("to") ?? now;
      const date = new Date(Date.parse(to) - 1).toISOString().slice(0, 10);

      await route.fulfill({
        json: {
          from: url.searchParams.get("from"),
          to,
          days: [
            {
              date,
              seconds: 3600,
              plays: 5,
              sessionCount: 17,
              top: [
                {
                  title: "The song from this listening day",
                  artist: "The Headliners",
                  seconds: 3600,
                  plays: 5,
                },
              ],
            },
          ],
          months: [],
          totalSeconds: 3600,
          activeDays: 1,
          sessionCount: 17,
          qualifiedPlayCount: 5,
        },
      });
    } else {
      const annual = recap(`${now.slice(0, 4)}-01`, now);

      await route.fulfill({
        json: {
          id: "annual",
          schemaVersion: 3,
          periodStart: annual.from,
          periodEnd: now,
          generatedAt: now,
          isFinal: false,
          payload: {
            type: "wrapped",
            deck: annual.deck,
            summary: annual.summary,
            visualIdentity: annual.visualIdentity,
            recipeVersion: 2,
            catalogVersion: "fixture",
          },
        },
      });
    }

    return true;
  });
  await page.goto("/stats");
  await expect(page.getByRole("heading", { name: "Your listening", exact: true })).toBeVisible();

  return {
    requests,
    release: () => release(),
    recover: () => {
      failed = false;
    },
  };
};

const expectMonthRequests = (requests: URL[]) => {
  expect(requests.filter((url) => url.pathname === "/api/stats/wrapped")).toHaveLength(2);
  expect(
    requests
      .find((url) => url.searchParams.get("from")?.startsWith("2026-09"))
      ?.searchParams.get("to"),
  ).toBe("2026-10-01T00:00:00.000Z");
  expect(
    requests
      .find((url) => url.searchParams.get("from")?.startsWith("2026-10"))
      ?.searchParams.has("to"),
  ).toBe(false);
};

const inspectTimelineDay = async (page: Page, touch = false) => {
  const timeline = page.getByRole("region", { name: "Your listening timeline", exact: true });
  const day = timeline.locator('[data-day="2026-10-09"]');

  await day.scrollIntoViewIfNeeded();

  if (touch) await day.tap();
  else await day.click();

  const drawer = page.getByRole("dialog");

  await expect(drawer).toBeVisible();
  await expect(drawer).toContainText("The song from this listening day");
  await page.keyboard.press("Escape");
  await expect(drawer).toBeHidden();
};

test("recent months lead the page and open cached monthly recaps with annual access below", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  const { requests } = await openListening(page);
  const current = page.getByRole("article", { name: "October 2026 recap", exact: true });
  const previous = page.getByRole("article", { name: "September 2026 recap", exact: true });

  await expect(current).toContainText("So far");
  await expect(previous).toContainText("Complete");
  await expect(current).toContainText("1 hr");
  await expect(current).toContainText("5 plays");
  await expect(current).not.toContainText("17 plays");
  await expect(current).not.toContainText("The runner up");
  await expect(current.locator(".monthly-future")).toHaveCount(22);
  await expect(previous.locator(".monthly-future")).toHaveCount(0);
  const a = (await current.boundingBox())!;
  const b = (await previous.boundingBox())!;

  expect(a.height).toBe(b.height);
  expect(a.y).toBe(b.y);
  expectMonthRequests(requests);
  await page
    .getByRole("region", { name: "Your recaps", exact: true })
    .screenshot({ path: "/tmp/listening-months-desktop.png" });
  await current.getByRole("link", { name: "Open October 2026 recap", exact: true }).click();
  await expect(page).toHaveURL(/month=2026-10/u);
  await expect(page.getByRole("figure", { name: "Your month in music, day by day" })).toBeVisible();
  expect(requests.filter((url) => url.pathname === "/api/stats/wrapped")).toHaveLength(2);
  await page.goBack();
  await page.getByRole("link", { name: "2026 so far", exact: true }).click();
  await expect(page.getByRole("heading", { name: "2026 Wrapped", exact: true })).toBeVisible();
});

for (const colorScheme of ["light", "dark"] as const) {
  test(`monthly cards stack without overflow on phones in ${colorScheme} mode`, async ({
    browser,
  }) => {
    const context = await browser.newContext({
      viewport: { width: 390, height: 1000 },
      isMobile: true,
      hasTouch: true,
      colorScheme,
      baseURL: test.info().project.use.baseURL,
    });

    try {
      const page = await context.newPage();

      await openListening(page);
      const current = page.getByRole("article", { name: "October 2026 recap", exact: true });
      const previous = page.getByRole("article", { name: "September 2026 recap", exact: true });

      await expect(previous.getByRole("link", { name: "Open September 2026 recap" })).toBeVisible();
      const a = (await current.boundingBox())!;
      const b = (await previous.boundingBox())!;

      expect(b.y).toBeGreaterThan(a.y + a.height);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      );
      await current.screenshot({ path: `/tmp/listening-months-phone-${colorScheme}.png` });
      await inspectTimelineDay(page, true);
      await previous.getByRole("link", { name: "Open September 2026 recap" }).tap();
      await expect(
        page.getByRole("heading", { name: "September 2026 Wrapped", exact: true }),
      ).toBeVisible();
    } finally {
      await context.close();
    }
  });
}

test("each month loads and retries independently from the other month and annual archive", async ({
  page,
}) => {
  const { release, recover } = await openListening(page, {
    holdCurrent: true,
    failCurrent: true,
    failArchive: true,
  });
  const current = page.getByRole("article", { name: "October 2026 recap", exact: true });
  const previous = page.getByRole("article", { name: "September 2026 recap", exact: true });

  await expect(current.getByRole("status")).toBeVisible();
  await expect(previous.getByRole("link", { name: "Open September 2026 recap" })).toBeVisible();
  release();
  await expect(current).toContainText("Could not load this month", { timeout: 20_000 });
  await expect(page.getByText("Could not load past recaps", { exact: true })).toBeVisible();
  recover();
  await current.getByRole("button", { name: "Try again", exact: true }).click();
  await expect(current.getByRole("link", { name: "Open October 2026 recap" })).toBeVisible();
});

test("empty months and unavailable daily detail do not invent data", async ({ page }) => {
  await openListening(page, { empty: true });
  await expect(page.getByText("No recorded listening", { exact: true })).toHaveCount(2);
  await expect(page.getByRole("link", { name: "Browse music", exact: true })).toHaveCount(1);
  await expect(page.locator(".monthly-bar")).toHaveCount(0);
  await page.unroute("**/api/**");
  await openListening(page, { missingDaily: true });
  await expect(
    page.getByText("Daily detail isn't available for this recap.", { exact: true }),
  ).toHaveCount(2);
  await expect(page.locator(".monthly-bar")).toHaveCount(0);
});

test("January includes December and inline Timeline keeps its range while revisiting recaps", async ({
  page,
}) => {
  const { requests } = await openListening(page, { now: "2026-01-09T12:00:00Z" });

  await expect(page.getByRole("article", { name: "January 2026 recap" })).toBeVisible();
  await expect(page.getByRole("article", { name: "December 2025 recap" })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Listening sections" })).toHaveCount(0);
  await page.getByRole("link", { name: "Jump to timeline", exact: true }).click();
  await page.getByRole("button", { name: "Previous 90 days", exact: true }).click();
  const timeline = page.getByRole("region", { name: "Your listening timeline", exact: true });
  const range = await timeline.locator("header p").textContent();

  await page.getByRole("article", { name: "January 2026 recap" }).scrollIntoViewIfNeeded();
  await expect(page.getByRole("article", { name: "January 2026 recap" })).toBeVisible();
  await expect(timeline.locator("header p")).toHaveText(range!);
  expect(requests.filter((url) => url.pathname === "/api/stats/wrapped")).toHaveLength(2);
});

test("the unified page keeps day details and legacy timeline bookmarks usable", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await openListening(page);
  const timeline = page.getByRole("region", { name: "Your listening timeline", exact: true });

  await inspectTimelineDay(page);
  await page.screenshot({ path: "/tmp/listening-unified-timeline.png", fullPage: true });
  await page.goto("/stats/timeline?source=bookmark");
  await expect(page).toHaveURL(/\/stats\?source=bookmark&view=timeline/u);
  await expect(
    timeline.getByRole("heading", { name: "Listening timeline", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("article", { name: "October 2026 recap" })).toHaveCount(1);
  await expect.poll(async () => (await timeline.boundingBox())!.y).toBeLessThan(900);
  await page.getByRole("link", { name: "Jump to timeline", exact: true }).click();
  await expect(page.locator("#timeline")).toBeFocused();
  await page.mouse.wheel(0, -2000);
  await page.getByRole("link", { name: "Jump to timeline", exact: true }).click();
  await expect.poll(async () => (await timeline.boundingBox())!.y).toBeLessThan(900);
});
