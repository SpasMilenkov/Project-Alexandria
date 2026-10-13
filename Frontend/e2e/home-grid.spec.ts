import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { removeWidget } from "./helpers/home-grid-editor.js";

const verifyRightPlacement = async (page: Page) => {
  const grid = page.locator('[aria-label="Home widgets"]');
  const clock = page.locator('[data-widget-id="default-clock"]');
  const library = page.locator('[data-widget-id="default-library"]');

  await expect(grid).toHaveAttribute("data-grid-columns", "12");

  const gridBounds = (await grid.boundingBox())!;
  const clockBounds = (await clock.boundingBox())!;
  const libraryBounds = (await library.boundingBox())!;

  expect(clockBounds.x).toBeGreaterThan(gridBounds.x + gridBounds.width / 2);
  expect(clockBounds.x + clockBounds.width).toBeCloseTo(gridBounds.x + gridBounds.width, 0);
  expect(libraryBounds.y - clockBounds.y).toBe(8 * 80);

  await page.screenshot({ path: "/tmp/alexandria-grid-right-aligned.png", fullPage: true });

  return { clock, library, clockBounds };
};

const verifySharedProjection = async (page: Page, desktopX: number) => {
  const grid = page.locator('[aria-label="Home widgets"]');
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.setViewportSize({ width: 390, height: 844 });

  await expect(grid).toHaveAttribute("data-grid-columns", "4");
  expect((await clock.boundingBox())!.width).toBeLessThanOrEqual(390);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );

  await page.screenshot({ path: "/tmp/alexandria-grid-shared-mobile.png", fullPage: true });

  await page.setViewportSize({ width: 1440, height: 1100 });

  await expect(grid).toHaveAttribute("data-grid-columns", "12");
  expect((await clock.boundingBox())!.x).toBe(desktopX);
};

test("saved cell geometry keeps a medium clock on the right and preserves vacant rows", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = seed();
  initial.desktop.widgets[0]!.x = 6;
  initial.desktop.widgets[1]!.y = 8;
  const state = await fixture(page, initial);
  const { clock, library, clockBounds } = await verifyRightPlacement(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  expect((await clock.boundingBox())!.height).toBe(clockBounds.height);

  await removeWidget(page, library);

  const editGrid = (await page.locator('[aria-label="Home widgets"]').boundingBox())!;
  const editedClock = (await clock.boundingBox())!;

  expect(editedClock.x + editedClock.width).toBeCloseTo(editGrid.x + editGrid.width, 0);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.schemaVersion).toBe(1);
  expect(state.saved.desktop.widgets[0]).toMatchObject({ x: 6, y: 0, width: 6, height: 4 });

  await page.reload();

  await expect(clock).toBeVisible();
  expect((await clock.boundingBox())!.x).toBe(clockBounds.x);

  await verifySharedProjection(page, clockBounds.x);

  expect(state.saved.desktop.widgets[0]!.x).toBe(6);
});

test("separate mobile geometry remains independent through edits and reload", async ({ page }) => {
  const initial = seed();
  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: [{ ...initial.desktop.widgets[0]!, size: 0, x: 2, y: 2, width: 2, height: 3 }],
  };

  await page.setViewportSize({ width: 390, height: 844 });

  const state = await fixture(page, initial);
  const clock = page.locator('[data-widget-id="default-clock"]');
  const grid = page.locator('[aria-label="Home widgets"]');
  const gridBounds = (await grid.boundingBox())!;

  expect((await clock.boundingBox())!.x).toBeGreaterThan(gridBounds.x + gridBounds.width / 2);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  await page.reload();

  expect(state.saved.mobile!.widgets[0]).toMatchObject({ x: 2, y: 2, width: 2, height: 3 });
  expect(state.saved.desktop).toEqual(seed().desktop);

  await page.screenshot({ path: "/tmp/alexandria-grid-independent-mobile.png", fullPage: true });
});
