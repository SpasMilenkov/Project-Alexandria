import { type Locator, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";

test.use({ timezoneId: "UTC" });

const faces = [
  ".clock-quiet-time",
  ".clock-dial",
  ".clock-material-time",
  ".clock-flip-card",
  ".clock-rings-dial",
  ".clock-words-phrase",
];

const faceSize = (element: Locator) =>
  element.evaluate((face) => {
    if (face.tagName.toLowerCase() === "svg") return face.getBoundingClientRect().height;

    return parseFloat(getComputedStyle(face).fontSize);
  });

test("every clock grows with a larger widget while keeping content inside its card", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.clock.setFixedTime(new Date("2026-10-07T23:33:30Z"));

  const initial = seed();
  const original = initial.desktop.widgets[0]!;

  initial.desktop.widgets = faces.map((_, style) => ({
    ...original,
    instanceId: `scaling-${style}`,
    width: 4,
    height: 4,
    y: style * 4,
    options: { ...original.options, clockStyle: style, timeZone: "UTC", hourFormat: 1 },
  }));

  const state = await fixture(page, initial, { fontFamily: "mono", backgroundColor: null });
  await expect(page.locator(".clock-time")).toHaveCount(faces.length);

  const smallSizes: number[] = [];

  for (const [style, face] of faces.entries()) {
    const element = page.locator(`[data-widget-id="scaling-${style}"]`).locator(face).first();
    smallSizes.push(await faceSize(element));
  }

  state.saved.desktop.widgets.forEach((widget, index) => {
    widget.width = 12;
    widget.height = 8;
    widget.y = index * 8;
  });

  await page.reload();
  await expect(page.locator(".clock-time")).toHaveCount(faces.length);

  for (const [style, face] of faces.entries()) {
    const clock = page.locator(`[data-widget-id="scaling-${style}"]`);
    const element = clock.locator(face).first();

    expect(await faceSize(element), face).toBeGreaterThan(smallSizes[style]! * 1.3);
    expect(
      await clock.evaluate(
        (card) =>
          card.scrollHeight <= card.clientHeight + 1 && card.scrollWidth <= card.clientWidth + 1,
      ),
    ).toBe(true);
  }

  expect(state.writes).toHaveLength(0);
});

const ringTypography = (clock: Locator) =>
  clock.evaluate((card) => {
    const legend = card.querySelector(".clock-rings-legend")!;
    const date = card.querySelector(".clock-date")!;
    const zone = card.querySelector(".clock-zone")!;

    return {
      legend: parseFloat(getComputedStyle(legend).fontSize),
      date: parseFloat(getComputedStyle(date).fontSize),
      zone: parseFloat(getComputedStyle(zone).fontSize),
    };
  });

const ringContentFits = (clock: Locator) =>
  clock.evaluate((card) => {
    const bounds = card.getBoundingClientRect();
    const contents = card.querySelectorAll(
      ".clock-rings-dial, .clock-rings-legend, .clock-details",
    );

    return [...contents].every((element) => {
      if (getComputedStyle(element).display === "none") return true;

      const rect = element.getBoundingClientRect();

      return (
        rect.left >= bounds.left &&
        rect.right <= bounds.right + 1 &&
        rect.top >= bounds.top &&
        rect.bottom <= bounds.bottom + 1 &&
        element.scrollWidth <= element.clientWidth + 1 &&
        element.scrollHeight <= element.clientHeight + 1
      );
    });
  });

for (const colorScheme of ["light", "dark"] as const) {
  test(`ring labels scale together in compact and large cards in ${colorScheme}`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme, reducedMotion: "reduce" });
    await page.setViewportSize({ width: 1440, height: 1100 });
    await page.clock.setFixedTime(new Date("2026-10-10T17:23:25Z"));
    const initial = seed();
    initial.desktop.widgets = [initial.desktop.widgets[0]!];
    Object.assign(initial.desktop.widgets[0]!.options, {
      clockStyle: 4,
      timeZone: "Europe/Bucharest",
      hourFormat: 1,
      showSeconds: true,
    });
    const state = await fixture(page, initial, { fontFamily: "mono", backgroundColor: null });
    const clock = page.locator("[data-widget-id]").first();
    const sizes = [];

    for (const [width, height] of [
      [6, 4],
      [12, 8],
      [3, 3],
    ] as const) {
      Object.assign(state.saved.desktop.widgets[0]!, { width, height });
      await page.reload();
      await expect(clock.locator(".clock-time")).toHaveText(/08:23:25\s*PM/u);
      expect(await ringContentFits(clock)).toBe(true);
      sizes.push(await ringTypography(clock));
      await clock.screenshot({
        path: `/tmp/alexandria-clock-resize/rings-${width}-${height}-${colorScheme}.png`,
      });
    }

    expect(sizes[0]!.legend).toBeGreaterThan(14);
    expect(sizes[1]!.legend).toBeGreaterThan(sizes[0]!.legend);
    expect(sizes[1]).toEqual({ legend: 20, date: 20, zone: 20 });
    expect(sizes[2]!.legend).toBe(12);
    await page.setViewportSize({ width: 390, height: 844 });
    expect(await ringContentFits(clock)).toBe(true);
    await clock.screenshot({ path: `/tmp/alexandria-clock-resize/rings-phone-${colorScheme}.png` });
    expect(state.writes).toHaveLength(0);
  });
}
