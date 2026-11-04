import { type Locator, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { closeWidgetSettings, openWidgetTab } from "./helpers/home-grid-editor.js";

test.use({ timezoneId: "UTC" });

const styles = [3, 4, 5];

const fitsCard = async (clock: Locator) => {
  const fits = await clock.evaluate(
    (element) =>
      element.scrollHeight <= element.clientHeight + 1 &&
      element.scrollWidth <= element.clientWidth + 1,
  );

  expect(fits, (await clock.getAttribute("data-widget-id")) ?? "Clock").toBe(true);
};

for (const hourFormat of [1, 2]) {
  test(`new clock styles show saved-zone midnight in hour format ${hourFormat}`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1440, height: 1100 });
    await page.clock.setFixedTime(new Date("2026-01-15T18:20:00Z"));

    const initial = seed();
    const original = initial.desktop.widgets[0]!;

    initial.desktop.widgets = styles.map((style, index) => ({
      ...original,
      instanceId: `midnight-${style}`,
      x: index * 3,
      width: 3,
      height: 6,
      options: { ...original.options, clockStyle: style, timeZone: "Asia/Kathmandu", hourFormat },
    }));

    await fixture(page, initial);

    for (const clock of await page.locator("[data-widget-id]").all()) {
      if (hourFormat === 1) await expect(clock.locator(".clock-time")).toHaveText(/12:05\s*AM/u);
      else await expect(clock.locator(".clock-time")).toHaveText("00:05");

      await expect(clock.locator(".clock-date")).toHaveText("Friday, January 16");
      await fitsCard(clock);
    }

    await expect(page.locator(".clock-words-phrase")).toHaveText("five past twelve");
  });
}

for (const fontFamily of ["system", "mono"]) {
  test(`new clock faces fit all five grid shapes with the ${fontFamily} font`, async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1100 });
    await page.clock.setFixedTime(new Date("2026-10-07T23:33:30Z"));

    const initial = seed();
    const original = initial.desktop.widgets[0]!;
    const shapes = [
      { name: "compact", x: 0, y: 0, width: 2, height: 3 },
      { name: "banner", x: 2, y: 0, width: 4, height: 2 },
      { name: "tall", x: 6, y: 0, width: 2, height: 6 },
      { name: "medium", x: 8, y: 0, width: 4, height: 4 },
      { name: "large", x: 0, y: 6, width: 6, height: 6 },
    ];

    initial.desktop.widgets = styles.flatMap((style, index) =>
      shapes.map((shape) => ({
        ...original,
        ...shape,
        instanceId: `${shape.name}-${style}`,
        y: index * 12 + shape.y,
        options: { ...original.options, clockStyle: style, timeZone: "local", hourFormat: 1 },
      })),
    );

    await fixture(page, initial, { fontFamily, backgroundColor: null });

    for (const colorScheme of ["light", "dark"] as const) {
      await page.emulateMedia({ colorScheme });

      for (const clock of await page.locator("[data-widget-id]").all()) await fitsCard(clock);
    }
  });
}

test("new compact faces keep an unavailable-zone notice visible on desktop and phone", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.clock.setFixedTime(new Date("2026-10-07T23:33:30Z"));

  const initial = seed();
  const original = initial.desktop.widgets[0]!;

  initial.desktop.widgets = styles.flatMap((style, index) =>
    [
      { name: "narrow", x: 0, width: 2, height: 3 },
      { name: "short", x: 2, width: 4, height: 2 },
      { name: "tiny", x: 6, width: 2, height: 2 },
    ].map((shape) => ({
      ...original,
      ...shape,
      instanceId: `${shape.name}-${style}`,
      y: index * 4,
      options: {
        ...original.options,
        clockStyle: style,
        timeZone: "Europe/RemovedZone",
        hourFormat: 1,
      },
    })),
  );

  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: initial.desktop.widgets.map((widget, index) => ({
      ...widget,
      instanceId: `mobile-${widget.instanceId}`,
      x: (index % 2) * 2,
      y: Math.floor(index / 2) * 2,
      width: 2,
      height: 2,
    })),
  };

  const state = await fixture(page, initial, { fontFamily: "mono", backgroundColor: null });

  for (const viewportWidth of [1440, 414]) {
    await page.setViewportSize({ width: viewportWidth, height: 1100 });

    for (const clock of await page.locator("[data-widget-id]").all()) {
      const body = clock.locator(".clock-alignment");

      if ((await body.getAttribute("data-face-only")) === "true") {
        await expect(body).toHaveAttribute("title", /Showing device time/u);
        await expect(clock.getByRole("status")).toHaveClass("sr-only");
      } else {
        await expect(clock.getByRole("status")).toBeVisible();
      }

      await expect(clock.getByRole("status")).toContainText("Showing device time.");
      await fitsCard(clock);
    }
  }

  expect(state.writes).toHaveLength(0);
  expect(
    state.saved.desktop.widgets.every((widget) => widget.options.timeZone === "Europe/RemovedZone"),
  ).toBe(true);
});

test("Words keeps exact minutes under rounded phrases", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.clock.setFixedTime(new Date("2026-10-07T23:58:00Z"));

  const initial = seed();
  const original = initial.desktop.widgets[0]!;

  initial.desktop.widgets = [
    {
      ...original,
      width: 4,
      height: 5,
      options: { ...original.options, clockStyle: 5, timeZone: "UTC", hourFormat: 2 },
    },
  ];

  await fixture(page, initial);

  await expect(page.locator(".clock-words-phrase")).toHaveText("twelve o’clock");
  await expect(page.locator(".clock-words-exact")).toHaveText("23:58");
});

test("new styles remain independent on desktop and mobile and save only through Done", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = seed();
  initial.desktop.widgets = [initial.desktop.widgets[0]!];
  initial.desktop.widgets[0]!.options.clockStyle = 3;
  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: [
      {
        ...initial.desktop.widgets[0]!,
        instanceId: "mobile-clock",
        width: 4,
        options: { ...initial.desktop.widgets[0]!.options, clockStyle: 0 },
      },
    ],
  };

  const state = await fixture(page, initial);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Words", exact: true }).click();
  await closeWidgetSettings(page);
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Rings", exact: true }).click();
  await closeWidgetSettings(page);

  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.desktop.widgets[0]!.options.clockStyle).toBe(5);
  expect(state.saved.mobile!.widgets[0]!.options.clockStyle).toBe(4);

  await page.reload();
  await expect(page.locator(".clock-words-face")).toBeVisible();
  await page.setViewportSize({ width: 414, height: 896 });
  await page.reload();
  await expect(page.locator(".clock-rings-face")).toBeVisible();
});

test("Rings uses fractional seconds only while visible and normal motion is enabled", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.clock.setFixedTime(new Date("2026-10-07T12:00:00.500Z"));

  const initial = seed();
  const original = initial.desktop.widgets[0]!;

  initial.desktop.widgets = [
    { ...original, options: { ...original.options, clockStyle: 4, timeZone: "UTC" } },
    { ...original, instanceId: "far-clock", y: 24 },
  ];

  await fixture(page, initial);

  const clock = page.locator('[data-widget-id][aria-label="Clock"]').first();
  const motion = clock.locator(".clock-alignment");
  const seconds = clock.locator('[data-ring="second"]');

  await expect(motion).toHaveAttribute("data-motion-active", "true");
  await expect
    .poll(async () => parseFloat((await seconds.getAttribute("stroke-dasharray"))!))
    .toBeCloseTo((0.5 / 60) * 100);

  await page.locator('[data-widget-id="far-clock"]').scrollIntoViewIfNeeded();
  await expect(motion).toHaveAttribute("data-motion-active", "false");
  await clock.scrollIntoViewIfNeeded();
  await expect(motion).toHaveAttribute("data-motion-active", "true");

  await page.emulateMedia({ reducedMotion: "reduce" });
  await expect(motion).toHaveAttribute("data-motion-active", "false");
  await expect(seconds).toHaveAttribute("stroke-dasharray", "0 100");
  await page.clock.setFixedTime(new Date("2026-10-07T12:00:01.500Z"));
  await expect
    .poll(async () => parseFloat((await seconds.getAttribute("stroke-dasharray"))!))
    .toBeCloseTo((1 / 60) * 100);
});
