import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { openWidgetTab } from "./helpers/home-grid-editor.js";

const sampleLeaf = (leaf: Locator, progress: number) =>
  leaf.evaluate((element, fraction) => {
    const animation = element.getAnimations()[0]!;
    animation.pause();
    animation.currentTime = Number(animation.effect!.getComputedTiming().duration) * fraction;

    const style = getComputedStyle(element);
    const transform = new DOMMatrixReadOnly(style.transform);

    return {
      rotation: transform.m23,
      opacity: Number(style.opacity),
      originY: Number.parseFloat(style.transformOrigin.split(" ")[1]!),
      height: Number.parseFloat(style.height),
      digit: getComputedStyle(element, "::before").content,
    };
  }, progress);

const verifyTopFold = async (page: Page, colorScheme: string) => {
  const outgoing = page.locator(".clock-flip-leaf--old-top");
  const incoming = page.locator(".clock-flip-leaf--new-bottom");
  const oldBottom = page.locator(".clock-flip-leaf--old-bottom");
  const topStart = await sampleLeaf(outgoing, 0.25);
  const bottomStart = await sampleLeaf(incoming, 0.25);

  expect(topStart.rotation).toBeLessThan(0);
  expect(topStart.opacity).toBe(1);
  expect(topStart.originY).toBeCloseTo(topStart.height, 0);
  expect(topStart.digit).toBe('"8"');
  expect(bottomStart.rotation).toBeCloseTo(1, 4);
  expect(bottomStart.originY).toBe(0);
  expect(bottomStart.digit).toBe('"9"');
  expect((await sampleLeaf(oldBottom, 0.25)).digit).toBe('"8"');
  await page
    .locator(".clock-flip-face")
    .screenshot({ animations: "allow", path: `/tmp/alexandria-flip-fold-${colorScheme}.png` });

  return { outgoing, incoming, oldBottom };
};

for (const colorScheme of ["light", "dark"] as const) {
  test(`Flip folds downward from old to new digits and handles minute rollover in ${colorScheme} mode`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme });
    await page.clock.install({ time: new Date("2026-10-08T10:08:57Z") });
    await page.clock.pauseAt(new Date("2026-10-08T10:08:58Z"));

    const initial = seed();
    initial.desktop.widgets = [initial.desktop.widgets[0]!];
    initial.desktop.widgets[0]!.options = {
      ...initial.desktop.widgets[0]!.options,
      clockStyle: 3,
      showSeconds: true,
      timeZone: "UTC",
      hourFormat: 2,
    };
    await fixture(page, initial);

    await expect(page.locator(".clock-flip-leaf")).toHaveCount(0);
    await page.clock.runFor(1000);
    await expect(page.locator(".clock-flip-digit--changing")).toHaveCount(1);

    const { outgoing, incoming, oldBottom } = await verifyTopFold(page, colorScheme);

    await verifyBottomDrop(outgoing, incoming, oldBottom);
    await page.clock.runFor(1000);
    await expect(page.locator(".clock-time")).toHaveText("10:09:00");
    await expect(page.locator(".clock-flip-digit--changing")).toHaveCount(3);
    await page.emulateMedia({ reducedMotion: "reduce" });
    await expect(page.locator(".clock-flip-leaf")).toHaveCount(0);
    await page.clock.runFor(1000);
    await expect(page.locator(".clock-time")).toHaveText("10:09:01");
    await expect(page.locator(".clock-flip-digit--changing")).toHaveCount(0);
  });
}

const verifyBottomDrop = async (outgoing: Locator, incoming: Locator, oldBottom: Locator) => {
  expect((await sampleLeaf(outgoing, 0.75)).opacity).toBe(0);
  expect((await sampleLeaf(oldBottom, 0.75)).opacity).toBe(1);

  const dropping = await sampleLeaf(incoming, 0.75);

  expect(dropping.rotation).toBeGreaterThan(0);
  expect(dropping.rotation).toBeLessThan(1);
  expect((await sampleLeaf(incoming, 1)).rotation).toBeCloseTo(0, 4);
  expect((await sampleLeaf(oldBottom, 1)).opacity).toBe(0);
};

const noonFlipSeed = () => {
  const initial = seed();
  initial.desktop.widgets = [initial.desktop.widgets[0]!];
  initial.desktop.widgets[0]!.options = {
    ...initial.desktop.widgets[0]!.options,
    clockStyle: 3,
    showSeconds: true,
    timeZone: "UTC",
    hourFormat: 1,
  };

  return initial;
};

test("AM/PM flips at noon and the hour-format buttons persist the 24-hour preference", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.emulateMedia({ colorScheme: "dark" });
  await page.clock.install({ time: new Date("2026-10-09T11:59:59Z") });
  await page.clock.pauseAt(new Date("2026-10-09T11:59:59Z"));
  const state = await fixture(page, noonFlipSeed());
  const period = page.locator('[data-clock-part="period"]');

  await expect(period.locator(".clock-flip-digit")).toHaveText(["A", "M"]);
  await page.clock.runFor(1000);
  await expect(period.locator(".clock-flip-digit--changing")).toHaveText("P");
  expect((await sampleLeaf(period.locator(".clock-flip-leaf--old-top"), 0.25)).digit).toBe('"A"');
  expect((await sampleLeaf(period.locator(".clock-flip-leaf--new-bottom"), 0.75)).digit).toBe(
    '"P"',
  );
  await page.clock.runFor(1000);
  await page
    .locator("[data-widget-id]")
    .screenshot({ path: "/tmp/alexandria-flip-period.png", animations: "disabled" });
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");
  await page.getByRole("button", { name: "24-hour", exact: true }).click();
  await expect(period).toHaveCount(0);
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.hourFormat).toBe(2);
  await page.reload();
  await expect(page.locator('[data-clock-part="period"]')).toHaveCount(0);
});
