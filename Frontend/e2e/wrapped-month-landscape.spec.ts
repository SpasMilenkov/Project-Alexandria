import { type Locator, type Page, expect, test } from "@playwright/test";

import { WrappedCardType } from "../src/enums/wrapped-card-type.js";
import { fixture, seed } from "./helpers/home-dashboard.js";

interface ListeningDay {
  date: string;
  seconds: number;
  plays: number;
}

interface FixtureFacts {
  series?: ListeningDay[];
  seconds?: number;
  from?: string;
  to?: string;
}

const makeCard = (type: WrappedCardType, facts: FixtureFacts) => ({
  type,
  headline: "Your time with music",
  subline: "Every listen adds up.",
  entries: [],
  visual: {
    colorKey: "daylight",
    saturation: "balanced",
    contrast: "crisp",
    density: 0,
    fillLevel: 0,
    shapeGrammar: "landscape",
  },
  facts: { seconds: 0, count: 0, share: 0, baselineSeconds: 0, weights: [], series: [], ...facts },
});

const makeDeck = (month: string, through: string, days: ListeningDay[], bookends: boolean) => {
  const seconds = days.reduce((sum, day) => sum + day.seconds, 0);
  const plays = days.reduce((sum, day) => sum + day.plays, 0);
  const cards = [
    makeCard(WrappedCardType.ListeningTime, {
      seconds,
      series: [{ date: `${month}-01`, seconds, plays }],
    }),
  ];

  if (bookends)
    cards.push(
      makeCard(WrappedCardType.Bookends, {
        series: days,
        from: days[0]?.date,
        to: days.at(-1)?.date,
      }),
    );

  return {
    from: `${month}-01T00:00:00Z`,
    to: through,
    generatedAt: through,
    visualIdentity: "monthly-landscape",
    summary: {
      seconds,
      sessions: plays,
      qualifiedPlayCount: plays,
      tracks: 1,
      artists: 1,
      activeDays: days.filter((day) => day.seconds > 0).length,
      knownArtistShare: 1,
      hasPriorHistory: true,
    },
    deck: { cards },
  };
};

const openRecap = async (page: Page, month: string, days: ListeningDay[], bookends = true) => {
  const requests: URL[] = [];
  const through = month === "2026-09" ? "2026-10-01T00:00:00Z" : "2026-10-09T12:00:00Z";
  const response = makeDeck(month, through, days, bookends);

  await page.clock.setFixedTime(new Date("2026-10-09T12:00:00Z"));
  await fixture(page, seed(), {}, undefined, async (route) => {
    const url = new URL(route.request().url());

    if (!url.pathname.startsWith("/api/stats/")) return false;

    requests.push(url);

    if (url.pathname === "/api/stats/summaries/Wrapped") {
      const annual = makeDeck(
        "2026-01",
        "2026-02-01T00:00:00Z",
        [{ date: "2026-01-03", seconds: 1800, plays: 8 }],
        true,
      );

      await route.fulfill({
        json: {
          id: "annual",
          schemaVersion: 3,
          periodStart: annual.from,
          periodEnd: annual.to,
          generatedAt: annual.to,
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
    } else {
      await route.fulfill({ json: response });
    }

    return true;
  });
  await page.goto(`/stats/wrapped/2026?month=${month}`);
  await expect(page.getByRole("figure", { name: "Your month in music, day by day" })).toBeVisible();

  return requests;
};

const inspectDaysByKeyboard = async (figure: Locator) => {
  const last = figure.locator('[data-day="2026-10-09"]');

  await last.focus();
  await last.press("ArrowLeft");
  await expect(figure.locator('[data-day="2026-10-08"]')).toBeFocused();
  await expect(figure.locator("figcaption")).toContainText("0 sec · 0 plays");
  await figure.locator('[data-day="2026-10-08"]').press("Home");
  await expect(figure.locator('[data-day="2026-10-01"]')).toBeFocused();
  await expect(
    figure.getByRole("button", { name: "Previous listening day", exact: true }),
  ).toBeDisabled();
  await figure.getByRole("button", { name: "Next listening day", exact: true }).click();
  await expect(figure.locator('[data-day="2026-10-02"]')).toHaveAttribute("aria-pressed", "true");
  await figure.locator('[data-day="2026-10-02"]').press("End");
  await expect(last).toBeFocused();
  await expect(
    figure.getByRole("button", { name: "Next listening day", exact: true }),
  ).toBeDisabled();
  await last.press("ArrowRight");
  await expect(last).toBeFocused();
};

test("current month plots daily totals and supports keyboard inspection without extra data requests", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const requests = await openRecap(page, "2026-10", [
    { date: "2026-10-03", seconds: 600, plays: 2 },
    { date: "2026-10-06", seconds: 1200, plays: 4 },
    { date: "2026-10-09", seconds: 1800, plays: 6 },
  ]);
  const figure = page.getByRole("figure", { name: "Your month in music, day by day" });

  await expect(figure.locator("[data-day]")).toHaveCount(31);
  await expect(figure.locator('[data-future="true"]')).toHaveCount(22);
  await expect(figure.locator('[data-day="2026-10-10"]')).toBeDisabled();
  await expect(figure.locator("figcaption")).toContainText("Friday, October 9 · your busiest day");
  const short = (await figure.locator('[data-day="2026-10-03"] .daily-bar').boundingBox())!;
  const tall = (await figure.locator('[data-day="2026-10-09"] .daily-bar').boundingBox())!;

  expect(short.height / tall.height).toBeCloseTo(1 / 3, 2);
  await inspectDaysByKeyboard(figure);
  await figure.locator('[data-day="2026-10-06"]').click();
  await expect(figure.locator("figcaption")).toContainText("20 min · 4 plays");
  expect(requests.map((url) => url.pathname)).toEqual(["/api/stats/wrapped"]);
  await figure.screenshot({ path: "/tmp/wrapped-daily-desktop.png" });
});

for (const colorScheme of ["light", "dark"] as const) {
  test(`closed sparse month fits a phone and supports touch in ${colorScheme} mode`, async ({
    browser,
  }) => {
    const context = await browser.newContext({
      viewport: { width: 390, height: 1000 },
      hasTouch: true,
      isMobile: true,
      colorScheme,
      baseURL: test.info().project.use.baseURL,
    });

    try {
      const page = await context.newPage();
      const requests = await openRecap(page, "2026-09", [
        { date: "2026-09-06", seconds: 1860, plays: 16 },
      ]);
      const figure = page.getByRole("figure", { name: "Your month in music, day by day" });

      await expect(figure.locator("[data-day]")).toHaveCount(30);
      await expect(figure.locator('[data-future="true"]')).toHaveCount(0);
      await expect(figure.locator(".daily-bar")).toHaveCount(1);
      await expect(figure.locator("figcaption")).toContainText(
        "Sunday, September 6 · your busiest day",
      );
      await figure.locator('[data-day="2026-09-05"]').tap();
      await expect(figure.locator("figcaption")).toContainText("0 sec · 0 plays");
      await figure.getByRole("button", { name: "Next listening day", exact: true }).tap();
      await expect(figure.locator("figcaption")).toContainText("31 min · 16 plays");
      expect(requests[0]!.searchParams.get("to")).toBe("2026-10-01T00:00:00.000Z");
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      );
      await figure.screenshot({ path: `/tmp/wrapped-daily-phone-${colorScheme}.png` });
    } finally {
      await context.close();
    }
  });
}

test("tied busiest days share the same height and use truthful copy", async ({ page }) => {
  await openRecap(page, "2026-10", [
    { date: "2026-10-03", seconds: 600, plays: 2 },
    { date: "2026-10-06", seconds: 600, plays: 2 },
  ]);
  const figure = page.getByRole("figure", { name: "Your month in music, day by day" });

  await expect(figure.locator('[data-day="2026-10-03"]')).toHaveAttribute("aria-pressed", "true");
  await expect(figure.locator("figcaption")).toContainText("one of your busiest days");
  await figure.locator('[data-day="2026-10-06"]').click();
  await expect(figure.locator("figcaption")).toContainText("one of your busiest days");
  await expect(figure.locator(".daily-bar--busiest")).toHaveCount(2);
});

test("all-zero daily facts keep quiet markers without naming a busiest day", async ({ page }) => {
  await openRecap(page, "2026-10", [
    { date: "2026-10-01", seconds: 0, plays: 0 },
    { date: "2026-10-02", seconds: 0, plays: 0 },
  ]);
  const figure = page.getByRole("figure", { name: "Your month in music, day by day" });

  await expect(figure.locator("figcaption")).toContainText("Thursday, October 1");
  await expect(figure.locator("figcaption")).toContainText("0 sec · 0 plays");
  await expect(figure.locator("figcaption")).not.toContainText("busiest");
  await expect(figure.locator(".daily-bar")).toHaveCount(0);
  await expect(figure.locator(".daily-quiet")).toHaveCount(9);
  await figure.getByRole("button", { name: "Next listening day", exact: true }).click();
  await expect(figure.locator("figcaption")).toContainText("Friday, October 2");
});

test("missing daily facts show an unavailable message and annual January keeps the monthly landscape", async ({
  page,
}) => {
  await openRecap(page, "2026-10", [{ date: "2026-10-03", seconds: 600, plays: 2 }], false);

  await expect(
    page.getByText("Daily listening isn't available for this recap.", { exact: true }),
  ).toBeVisible();
  await expect(page.locator(".daily-column")).toHaveCount(0);
  await expect(
    page.getByRole("group", { name: "Monthly listening landscape. Choose a month for its total." }),
  ).toHaveCount(0);
  await page.goto("/stats/wrapped/2026");
  await expect(
    page.getByRole("group", { name: "Monthly listening landscape. Choose a month for its total." }),
  ).toBeVisible();
  await expect(page.getByText("January 2026 · your busiest month", { exact: true })).toBeVisible();
  await expect(page.getByRole("figure", { name: "Your month in music, day by day" })).toHaveCount(
    0,
  );
});
