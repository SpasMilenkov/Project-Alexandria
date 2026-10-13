import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { openWidgetTab, selectWidget } from "./helpers/home-grid-editor.js";

test.use({ timezoneId: "Europe/Bucharest" });

const gallery = (width = 3, height = 4) => {
  const value = seed();
  const base = value.desktop.widgets[0]!;
  const perRow = Math.floor(12 / width);
  value.showWidgetTitles = true;
  value.desktop.widgets = [13, 14, 15]
    .flatMap((type) =>
      Array.from({ length: type === 14 ? 3 : 4 }, (_, variant) => ({
        ...base,
        instanceId: `general-${type}-${variant}`,
        type,
        width,
        height,
        options: {
          ...base.options,
          countdownStyle: variant,
          countdownLabel: "A much anticipated holiday",
          countdownTarget: "2026-10-21",
          focusStyle: variant,
          focusMinutes: 1,
          shortBreakMinutes: 1,
          longBreakMinutes: 1,
          focusSessions: 2,
          dailyCoverStyle: variant,
        },
      })),
    )
    .map((widget, index) => ({
      ...widget,
      x: (index % perRow) * width,
      y: Math.floor(index / perRow) * height,
    }));

  return value;
};

const verifyCountdownDots = async (card: Locator) => {
  const dots = card.locator(".countdown-dot-grid span");

  if (!(await dots.count())) return;

  await expect(dots).toHaveCount(30);

  const bounds = (await card.locator(".countdown-dots").boundingBox())!;
  const circles = await dots.evaluateAll((elements) =>
    elements.map((element) => {
      const { x, y, width, height } = element.getBoundingClientRect();

      return { x, y, width, height };
    }),
  );

  for (const circle of circles) {
    expect(circle.width).toBeGreaterThan(0);
    expect(Math.abs(circle.width - circle.height)).toBeLessThan(1);
    expect(circle.x).toBeGreaterThanOrEqual(bounds.x - 1);
    expect(circle.y).toBeGreaterThanOrEqual(bounds.y - 1);
    expect(circle.x + circle.width).toBeLessThanOrEqual(bounds.x + bounds.width + 1);
    expect(circle.y + circle.height).toBeLessThanOrEqual(bounds.y + bounds.height + 1);
  }

  const first = circles[0]!;
  const horizontalGap = circles[1]!.x - first.x - first.width;
  const verticalGap = circles[10]!.y - first.y - first.height;

  expect(Math.abs(horizontalGap - verticalGap)).toBeLessThan(1);
};

const verifyFits = async (card: Locator) => {
  await expect(card.locator(".countdown-widget, .focus-widget, .daily-cover")).toBeVisible();
  await verifyCountdownDots(card);
  const overflow = await card.evaluate((element) => ({
    width: element.scrollWidth - element.clientWidth,
    height: element.scrollHeight - element.clientHeight,
  }));
  expect(overflow, (await card.getAttribute("data-widget-id")) ?? "widget").toEqual({
    width: 0,
    height: 0,
  });

  expect(
    await card.evaluate((element) => {
      const controls = element.querySelector(".focus-controls");
      if (!controls) return true;
      const top = controls.getBoundingClientRect().top;

      return [...element.querySelectorAll(".focus-readout, .focus-dial")].every(
        (content) => content.getBoundingClientRect().bottom <= top + 1,
      );
    }),
  ).toBe(true);

  const bounds = (await card.boundingBox())!;
  const sections = card.locator(
    ".countdown-value, .countdown-label, .countdown-dots, .countdown-dial, .countdown-flip, .focus-main, .focus-controls, .focus-dial, .focus-readout, .focus-cycle, .daily-band",
  );

  for (const section of await sections.all()) {
    if (await section.isVisible()) {
      const sectionBounds = (await section.boundingBox())!;
      expect(sectionBounds.x).toBeGreaterThanOrEqual(bounds.x - 1);
      expect(sectionBounds.y).toBeGreaterThanOrEqual(bounds.y - 1);
      expect(sectionBounds.x + sectionBounds.width).toBeLessThanOrEqual(
        bounds.x + bounds.width + 1,
      );
      expect(sectionBounds.y + sectionBounds.height).toBeLessThanOrEqual(
        bounds.y + bounds.height + 1,
      );
    }
  }
};

const verifyLargeCountdownDots = async (page: Page, colorScheme: string) => {
  const dots = page.locator('[data-widget-id="general-13-3"]');

  expect(
    (await dots.locator(".countdown-dot-grid span").first().boundingBox())!.width,
  ).toBeGreaterThan(32);
  await dots.screenshot({ path: `/tmp/alexandria-countdown-dots-large-${colorScheme}.png` });
};

for (const colorScheme of ["light", "dark"] as const) {
  test(`eleven general variants fit compact, tall, wide and large cards in ${colorScheme} mode`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1800, height: 1100 });
    await page.emulateMedia({ colorScheme });
    await page.clock.setFixedTime(new Date("2026-10-09T10:00:00Z"));
    const state = await fixture(page, gallery(), { backgroundColor: null });

    for (const [width, height] of [
      [2, 2],
      [2, 3],
      [3, 4],
      [4, 6],
      [6, 2],
      [6, 4],
      [12, 6],
    ]) {
      state.saved = gallery(width, height);
      if (width === 12) state.saved.showWidgetTitles = false;
      await page.reload();
      await expect(page.locator("[data-widget-id]")).toHaveCount(11);

      for (const card of await page.locator("[data-widget-id]").all()) await verifyFits(card);

      if (width === 12 && height === 6) await verifyLargeCountdownDots(page, colorScheme);

      if (width === 2 || width === 3)
        await page.screenshot({
          path: `/tmp/alexandria-general-${colorScheme}-${width}x${height}.png`,
          fullPage: true,
        });
    }

    state.saved = gallery(2, 2);
    for (const widget of state.saved.desktop.widgets) {
      widget.options.countdownTarget = "9999-12-31";
      widget.options.focusMinutes = 180;
      widget.options.focusSessions = 8;
    }
    state.saved.useSeparateMobileLayout = true;
    state.saved.mobile = {
      columns: 4,
      widgets: state.saved.desktop.widgets.map((widget, index) => ({
        ...widget,
        x: (index % 2) * 2,
        y: Math.floor(index / 2) * 2,
      })),
    };
    await page.setViewportSize({ width: 414, height: 1000 });
    await page.reload();
    await expect(page.locator("[data-widget-id]")).toHaveCount(11);
    for (const card of await page.locator("[data-widget-id]").all()) await verifyFits(card);
    await page.screenshot({
      path: `/tmp/alexandria-general-mobile-${colorScheme}.png`,
      fullPage: true,
    });
  });
}

const chooseCountdownDate = async (page: Page) => {
  await page.getByRole("button", { name: "Choose target date", exact: true }).click();
  await page.getByRole("button", { name: "Wednesday, October 21, 2026", exact: true }).click();
  await page.keyboard.press("Escape");
};

const configureCountdown = async (page: Page) => {
  await page.getByRole("textbox", { name: "Event name", exact: true }).fill("Holiday");
  await chooseCountdownDate(page);

  const day = page.locator('[data-segment="day"]');

  await expect(day).toHaveText("21");
  await day.press("ArrowUp");
  await expect(day).toHaveText("22");
  await day.press("ArrowDown");
  await expect(day).toHaveText("21");

  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Day dots", exact: true }).click();
};

const discardCountdownEdit = async (page: Page, card: Locator) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");
  await chooseCountdownDate(page);
  await expect(card.getByText("Choose a date", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Discard and exit", exact: true })
    .click();
  await expect(card.locator(".countdown-value")).toHaveText("12");
};

test("Countdown treats free text as text and saves only settings through Done and Cancel", async ({
  page,
}) => {
  await page.clock.setFixedTime(new Date("2026-10-09T20:59:00Z"));
  const initial = gallery();
  initial.desktop.widgets = [initial.desktop.widgets[0]!];
  initial.desktop.widgets[0]!.options.countdownLabel = "<img src=x onerror=alert(1)> & party";
  initial.desktop.widgets[0]!.options.countdownTarget = "2026-10-09";
  const state = await fixture(page, initial);
  const card = page.locator("[data-widget-id]");

  await expect(card.locator(".countdown-value")).toHaveText("Today");
  await expect(card.locator(".countdown-label")).toHaveText("<img src=x onerror=alert(1)> & party");
  await expect(card.locator("img, script")).toHaveCount(0);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");
  await configureCountdown(page);
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.batches[0]!.updates[0]!.changes.options).toEqual({
    countdownLabel: "Holiday",
    countdownTarget: "2026-10-21",
    countdownStyle: 3,
  });
  await page.reload();
  await expect(card.locator(".countdown-label")).toHaveText("until Holiday");
  await expect(card.locator(".countdown-value")).toHaveText("12");

  await discardCountdownEdit(page, card);
  expect(state.writes).toHaveLength(1);
});

const verifyManualBreak = async (page: Page, first: Locator) => {
  await expect(first.getByRole("status")).toHaveText("Session complete");
  await page.clock.runFor(60_000);
  await expect(first.locator(".focus-widget")).toHaveAttribute("data-focus-phase", "focus");
  await first.getByRole("button", { name: "Start break", exact: true }).click();
  await expect(first.locator(".focus-widget")).toHaveAttribute("data-focus-phase", "shortBreak");
  await page.clock.runFor(1);
  await first.getByRole("button", { name: "Skip break", exact: true }).click();
  await expect(first.getByText("Session 2 of 2", { exact: true })).toBeVisible();
  await first.getByRole("button", { name: "Start", exact: true }).click();
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await expect(first.getByRole("button", { name: "Pause", exact: true })).toBeDisabled();
};

test("Focus stays local, recovers a deadline and waits for manual break starts", async ({
  page,
}) => {
  await page.clock.install({ time: new Date("2026-10-09T10:00:00Z") });
  await page.clock.pauseAt(new Date("2026-10-09T10:00:00Z"));
  const initial = gallery(6, 4);
  initial.desktop.widgets = initial.desktop.widgets
    .filter((widget) => widget.type === 14 && widget.options.focusStyle! < 2)
    .map((widget, index) => ({ ...widget, x: index * 6, y: 0 }));
  const state = await fixture(page, initial);
  const first = page.locator('[data-widget-id="general-14-0"]');
  const second = page.locator('[data-widget-id="general-14-1"]');

  await page.clock.runFor(1);

  await first.getByRole("button", { name: "Start", exact: true }).click();
  await expect(first.locator(".focus-widget")).toHaveAttribute("data-focus-status", "running");
  await expect(second.locator(".focus-widget")).toHaveAttribute("data-focus-status", "idle");
  const stored = await page.evaluate(() =>
    localStorage.getItem("alexandria:home-focus:home-fixture:general-14-0"),
  );
  await page.clock.runFor(10_000);
  expect(
    await page.evaluate(() =>
      localStorage.getItem("alexandria:home-focus:home-fixture:general-14-0"),
    ),
  ).toBe(stored);
  await page.reload();
  await expect(first.locator("svg text")).toHaveText("00:50");
  await page.clock.runFor(1);
  await first.getByRole("button", { name: "Pause", exact: true }).click();
  await page.clock.runFor(10_000);
  await expect(first.locator("svg text")).toHaveText("00:50");
  await first.getByRole("button", { name: "Resume", exact: true }).click();
  await page.clock.runFor(50_500);
  await verifyManualBreak(page, first);
  expect(state.writes).toHaveLength(0);
});

test("Focus and Daily cover styles and preferences save through the existing editor", async ({
  page,
}) => {
  const initial = gallery(6, 4);
  initial.desktop.widgets = initial.desktop.widgets
    .filter((widget) => ["general-14-0", "general-15-0"].includes(widget.instanceId))
    .map((widget, index) => ({ ...widget, x: index * 6, y: 0 }));
  const state = await fixture(page, initial);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, page.locator('[data-widget-id="general-14-0"]'));
  await openWidgetTab(page, "Options");
  await page.getByRole("spinbutton", { name: "Focus (minutes)", exact: true }).fill("42");
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Cycle", exact: true }).click();
  await selectWidget(page, page.locator('[data-widget-id="general-15-0"]'));
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Date poster", exact: true }).click();
  await page.screenshot({ path: "/tmp/alexandria-general-editor.png" });
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.batches[0]!.updates.map((update) => update.changes)).toEqual([
    { options: { focusStyle: 2, focusMinutes: 42 } },
    { options: { dailyCoverStyle: 3 } },
  ]);
  await page.reload();
  await expect(page.locator(".focus-time")).toHaveText("42:00");
  await expect(page.locator(".daily-cover")).toHaveAttribute("data-cover-style", "3");
});

test("Daily covers are stable on reload and roll over at local midnight without saved artwork", async ({
  page,
}) => {
  await page.clock.install({ time: new Date("2026-10-09T20:59:59Z") });
  await page.clock.pauseAt(new Date("2026-10-09T20:59:59Z"));
  const initial = gallery();
  initial.desktop.widgets = initial.desktop.widgets.filter((widget) => widget.type === 15);
  const state = await fixture(page, initial);
  const first = page.locator(".daily-cover").first();
  await expect(first).toHaveAttribute("data-cover-day", "2026-10-09");
  const artwork = await first.locator(".daily-inks").innerHTML();
  await page.reload();
  await expect(first.locator(".daily-inks")).toHaveJSProperty("innerHTML", artwork);
  const ids = await page
    .locator(".daily-art defs [id]")
    .evaluateAll((elements) => elements.map((element) => element.id));
  expect(new Set(ids).size).toBe(ids.length);
  await page.clock.runFor(31_000);
  await expect(first).toHaveAttribute("data-cover-day", "2026-10-10");
  expect(await first.locator(".daily-inks").innerHTML()).not.toBe(artwork);
  expect(state.writes).toHaveLength(0);
  expect(
    await page.evaluate(() => Object.keys(localStorage).filter((key) => /cover|seed/iu.test(key))),
  ).toEqual([]);
});
