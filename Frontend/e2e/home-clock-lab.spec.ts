import { expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";

test.use({ timezoneId: "UTC" });

for (const fontFamily of ["system", "mono"]) {
  test(`compact Tile captions remain readable with the ${fontFamily} font`, async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 1100 });
    await page.clock.setFixedTime(new Date("2026-10-07T20:08:00Z"));

    const initial = seed();
    const original = initial.desktop.widgets[0]!;

    initial.desktop.widgets = [0, 1, 2].flatMap((verticalAlignment) =>
      [0, 1, 2].map((horizontalAlignment) => ({
        ...original,
        instanceId: `tile-${verticalAlignment}-${horizontalAlignment}`,
        x: horizontalAlignment * 4,
        y: verticalAlignment * 4,
        width: 4,
        height: 4,
        options: {
          ...original.options,
          clockStyle: 2,
          hourFormat: 1,
          horizontalAlignment,
          verticalAlignment,
          textAlignment: horizontalAlignment,
        },
      })),
    );

    initial.useSeparateMobileLayout = true;
    initial.mobile = {
      columns: 4,
      widgets: initial.desktop.widgets.map((widget, index) => ({
        ...widget,
        x: 0,
        y: index * 4,
      })),
    };

    await fixture(page, initial, { fontFamily, backgroundColor: null, cornerRadius: 0 });

    for (const viewportWidth of [1280, 414]) {
      await page.setViewportSize({ width: viewportWidth, height: 1100 });

      for (const colorScheme of ["light", "dark"] as const) {
        await page.emulateMedia({ colorScheme });

        for (const clock of await page.locator("[data-widget-id]").all()) {
          await expect(clock.locator(".clock-date")).toHaveText("Wednesday, October 7");
          await expect(clock.locator(".clock-zone")).toBeVisible();

          const geometry = await clock.evaluate((element) => {
            const date = element.querySelector<HTMLElement>(".clock-date")!;
            const zone = element.querySelector<HTMLElement>(".clock-zone")!;
            const card = element.getBoundingClientRect();

            return {
              dateLines: date.clientHeight / parseFloat(getComputedStyle(date).lineHeight),
              zoneLines: zone.clientHeight / parseFloat(getComputedStyle(zone).lineHeight),
              captionWidth: date.clientWidth,
              captionBottom: zone.getBoundingClientRect().bottom,
              cardBottom: card.bottom,
              fits:
                element.scrollHeight <= element.clientHeight + 1 &&
                element.scrollWidth <= element.clientWidth + 1,
            };
          });

          expect(geometry.dateLines).toBeLessThanOrEqual(2);
          expect(geometry.zoneLines).toBeLessThanOrEqual(2);
          expect(geometry.captionWidth).toBeGreaterThanOrEqual(140);
          expect(geometry.captionBottom).toBeLessThanOrEqual(geometry.cardBottom);
          expect(geometry.fits).toBe(true);
        }
      }
    }
  });
}

for (const format of [1, 2]) {
  test(`all clock designs display midnight in ${format === 1 ? 12 : 24}-hour format`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1440, height: 1100 });
    await page.clock.setFixedTime(new Date("2026-01-15T18:20:00Z"));

    const initial = seed();
    const original = initial.desktop.widgets[0]!;

    initial.desktop.widgets = Array.from({ length: 3 }, (_, style) => ({
      ...original,
      instanceId: `midnight-${style}`,
      x: style * 4,
      y: 0,
      width: 4,
      height: 6,
      options: {
        ...original.options,
        timeZone: "Asia/Kathmandu",
        clockStyle: style,
        hourFormat: format,
      },
    }));

    await fixture(page, initial);

    for (const clock of await page.locator("[data-widget-id]").all()) {
      const time = clock.locator(".clock-time");

      if (format === 1) await expect(time).toHaveText(/12:05\s*AM/u);
      else await expect(time).toHaveText("00:05");

      await expect(clock.locator(".clock-date")).toContainText("Friday, January 16");
    }

    await expect(page.locator('[data-widget-id="midnight-0"] .clock-greeting')).toHaveText(
      "Good night",
    );
    await expect(page.locator('[data-widget-id="midnight-1"] .clock-date-window')).toContainText(
      "16",
    );
    await expect(page.locator('[data-widget-id="midnight-1"] .clock-second-hand')).toHaveCount(1);
  });
}

test("every compact clock preserves an unavailable-zone notice in both themes", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });

  const initial = seed();
  const original = initial.desktop.widgets[0]!;

  initial.desktop.widgets = Array.from({ length: 3 }, (_, style) => ({
    ...original,
    instanceId: `unavailable-${style}`,
    x: style * 4,
    y: 0,
    width: 4,
    height: 2,
    options: { ...original.options, timeZone: "Europe/RemovedZone", clockStyle: style },
  }));

  const state = await fixture(page, initial, { backgroundColor: null });

  for (const mode of ["light", "dark"] as const) {
    await page.emulateMedia({ colorScheme: mode });

    for (const clock of await page.locator("[data-widget-id]").all()) {
      await expect(clock.getByRole("status")).toBeVisible();
      await expect(clock.getByRole("status")).toContainText("Showing device time.");

      const fits = await clock.evaluate(
        (element) =>
          element.scrollHeight <= element.clientHeight + 1 &&
          element.scrollWidth <= element.clientWidth + 1,
      );

      expect(fits).toBe(true);
    }
  }

  expect(state.writes).toHaveLength(0);
  expect(
    state.saved.desktop.widgets.every((widget) => widget.options.timeZone === "Europe/RemovedZone"),
  ).toBe(true);
});

test("Watch sweeps only while visible and steps under reduced motion", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.clock.setFixedTime(new Date("2026-01-15T12:00:00.500Z"));

  const initial = seed();
  const original = initial.desktop.widgets[0]!;

  initial.desktop.widgets = [
    { ...original, options: { ...original.options, timeZone: "UTC", clockStyle: 1 } },
    { ...original, instanceId: "far-clock", y: 24 },
  ];

  await fixture(page, initial);

  const clock = page.locator('[data-widget-id="default-clock"]');
  const motion = clock.locator(".clock-alignment");
  const secondHand = clock.locator(".clock-second-hand");

  await expect(motion).toHaveAttribute("data-motion-active", "true");
  await expect(secondHand).toHaveAttribute("transform", "rotate(3 100 100)");
  await page.locator('[data-widget-id="far-clock"]').scrollIntoViewIfNeeded();
  await expect(motion).toHaveAttribute("data-motion-active", "false");
  await clock.scrollIntoViewIfNeeded();
  await expect(motion).toHaveAttribute("data-motion-active", "true");

  await page.emulateMedia({ reducedMotion: "reduce" });
  await expect(motion).toHaveAttribute("data-motion-active", "false");
  await expect(secondHand).toHaveAttribute("transform", "rotate(0 100 100)");
  await page.clock.setFixedTime(new Date("2026-01-15T12:00:01.500Z"));
  await expect(secondHand).toHaveAttribute("transform", "rotate(6 100 100)");
});

test("the five lab shapes fit the dashboard grid for each clock design", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.clock.setFixedTime(new Date("2026-01-15T10:08:00Z"));

  const initial = seed();
  const original = initial.desktop.widgets[0]!;
  const shapes = [
    { name: "compact", x: 0, y: 0, width: 2, height: 3 },
    { name: "banner", x: 2, y: 0, width: 4, height: 2 },
    { name: "tall", x: 6, y: 0, width: 2, height: 6 },
    { name: "medium", x: 8, y: 0, width: 4, height: 4 },
    { name: "large", x: 0, y: 6, width: 6, height: 6 },
  ];

  initial.desktop.widgets = [0, 1, 2].flatMap((style) =>
    shapes.map((shape) => ({
      ...original,
      ...shape,
      instanceId: `${shape.name}-${style}`,
      y: style * 12 + shape.y,
      options: { ...original.options, clockStyle: style, timeZone: "UTC", hourFormat: 1 },
    })),
  );

  await fixture(page, initial);

  for (const clock of await page.locator("[data-widget-id]").all()) {
    const fits = await clock.evaluate(
      (element) =>
        element.scrollHeight <= element.clientHeight + 1 &&
        element.scrollWidth <= element.clientWidth + 1,
    );

    expect(fits, (await clock.getAttribute("data-widget-id")) ?? "Clock").toBe(true);
  }

  await expect(page.locator(".clock-colon").first()).toHaveCSS("animation-name", "none");
  await expect(page.locator(".clock-colon").first()).toHaveCSS("visibility", "visible");
});
