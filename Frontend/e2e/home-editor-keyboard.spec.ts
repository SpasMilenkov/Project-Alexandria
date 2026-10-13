import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  dashboardSettings,
  gridPoint,
  openWidgetTab,
  resizePoints,
} from "./helpers/home-grid-editor.js";

test.use({ viewport: { width: 1440, height: 1100 }, serviceWorkers: "block" });

const customize = (page: Page) =>
  page.getByRole("button", { name: "Customize", exact: true }).click();

const gesturePoints = async (page: Page, gesture: "move" | "resize") => {
  const clock = page.locator('[data-widget-id="default-clock"]');

  if (gesture === "resize") return resizePoints(page, clock, "bottom", 0, 2);

  const bounds = (await clock.boundingBox())!;

  return {
    start: { x: bounds.x + 24, y: bounds.y + 24 },
    end: await gridPoint(page, 0, 3, { x: 24, y: 24 }),
  };
};

test("Delete keeps neighboring keyboard focus and saves an empty board only on Done", async ({
  page,
}) => {
  const initial = seed();
  const clock = initial.desktop.widgets[0]!;
  initial.desktop.widgets = [0, 1, 2].map((index) => ({
    ...clock,
    instanceId: `clock-${index}`,
    x: index * 4,
    width: 4,
    height: 3,
  }));
  const state = await fixture(page, initial);
  const cards = page.locator("[data-widget-id]");

  await customize(page);
  await page.locator('[data-widget-id="clock-1"]').focus();
  await page.keyboard.press("Delete");

  await expect(cards).toHaveCount(2);
  await expect(page.locator('[data-widget-id="clock-2"]')).toBeFocused();

  await page.keyboard.press("Delete");

  await expect(page.locator('[data-widget-id="clock-0"]')).toBeFocused();

  await page.keyboard.press("Delete");

  await expect(cards).toHaveCount(0);
  await expect(page.locator('[aria-label="Home widgets"]')).toBeFocused();
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets).toEqual([]);

  await page.reload();

  await expect(page.getByText("Your dashboard is empty", { exact: true })).toBeVisible();
});

for (const gesture of ["move", "resize"] as const) {
  test(`Delete cancels an active ${gesture} and pointer release cannot restore the card`, async ({
    page,
  }) => {
    const state = await fixture(page);
    const clock = page.locator('[data-widget-id="default-clock"]');
    const library = page.locator('[data-widget-id="default-library"]');

    await customize(page);

    const points = await gesturePoints(page, gesture);

    await page.mouse.move(points.start.x, points.start.y);
    await page.mouse.down();
    await page.mouse.move(points.end.x, points.end.y, { steps: 8 });

    await expect(page.locator("[data-placement-ghost]")).toBeVisible();

    await page.keyboard.press("Delete");

    await expect(clock).toHaveCount(0);
    await expect(page.locator("[data-placement-ghost]")).toHaveCount(0);

    await page.mouse.up();

    await expect(library).toBeFocused();
    expect(await library.locator("..").evaluate((element) => element.style.gridRow)).toBe(
      "5 / span 6",
    );
    expect(state.writes).toHaveLength(0);

    await page.getByRole("button", { name: "Done", exact: true }).click();

    await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
    expect(state.saved.desktop.widgets).toEqual([seed().desktop.widgets[1]]);
  });
}

test("Delete during catalogue placement cancels the new card and keeps existing widgets", async ({
  page,
}) => {
  await fixture(page);
  await customize(page);
  await page.getByRole("button", { name: "Add widget", exact: true }).click();

  const source = (await page.locator('[data-catalogue-widget="0"]').boundingBox())!;
  const end = await gridPoint(page, 0, 1);

  await page.mouse.move(source.x + 24, source.y + 24);
  await page.mouse.down();
  await page.mouse.move(end.x, end.y, { steps: 8 });

  await expect(page.locator("[data-placement-ghost]")).toBeVisible();

  await page.keyboard.press("Delete");
  await page.mouse.move(source.x + 24, source.y + 24);
  await page.mouse.up();

  await expect(page.locator("[data-placement-ghost]")).toHaveCount(0);
  await expect(page.locator("[data-widget-id]")).toHaveCount(2);
  await expect(page.locator('[aria-label="Home widgets"]')).toHaveAttribute(
    "data-dragging",
    "false",
  );
});

test("Delete ignores selection controls, modifiers and held-key repeats", async ({ page }) => {
  await fixture(page);
  const cards = page.locator("[data-widget-id]");
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.keyboard.press("Delete");
  await customize(page);
  await clock.focus();
  await page.keyboard.press("Control+Delete");
  await page.keyboard.press("Shift+Delete");
  await clock.evaluate((element) =>
    element.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Delete", repeat: true, bubbles: true }),
    ),
  );

  await openWidgetTab(page, "Options");
  const title = page.getByRole("combobox", { name: "Widget title", exact: true });

  await title.focus();
  await page.keyboard.press("Delete");
  await title.click();
  await page.keyboard.press("Delete");

  await expect(cards).toHaveCount(2);
});

test("Delete preserves text editing, overlays and dashboard preview", async ({ page }) => {
  await fixture(page);
  const cards = page.locator("[data-widget-id]");

  await customize(page);
  await openWidgetTab(page, "Options");
  await page.getByRole("button", { name: "Time zone", exact: true }).click();

  const search = page.getByPlaceholder("Search time zones...");

  await search.fill("UTC");
  await search.evaluate((element: HTMLInputElement) => element.setSelectionRange(0, 0));
  await search.press("Delete");

  await expect(search).toHaveValue("TC");
  await expect(cards).toHaveCount(2);

  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Preview", exact: true }).click();
  await page.keyboard.press("Delete");

  await expect(cards).toHaveCount(2);

  await page.getByRole("button", { name: "Back to editing", exact: true }).click();
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.keyboard.press("Delete");

  await expect(cards).toHaveCount(2);
  await expect(page.getByRole("dialog")).toBeVisible();
});

test("Delete cannot change a layout while Done is saving", async ({ page }) => {
  await fixture(page);
  let finishSave = () => {};
  const pending = new Promise<void>((resolve) => {
    finishSave = resolve;
  });

  await page.route("**/api/home/widgets", async (route) => {
    if (route.request().method() === "PATCH") await pending;

    await route.fallback();
  });

  await customize(page);
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.locator('[aria-label="Home widgets"]')).toHaveAttribute("data-editing", "true");
  await expect(page.getByRole("button", { name: "Cancel", exact: true })).toBeDisabled();

  await page.keyboard.press("Delete");

  await expect(page.locator("[data-widget-id]")).toHaveCount(2);

  finishSave();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
});

test("shared mobile preview is protected and separate mobile deletion leaves desktop intact", async ({
  page,
}) => {
  const initial = seed();
  initial.mobile = {
    columns: 4,
    widgets: [{ ...initial.desktop.widgets[0]!, instanceId: "mobile-clock", width: 4 }],
  };
  const state = await fixture(page, initial);

  await customize(page);
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await page.keyboard.press("Delete");

  await expect(page.locator("[data-widget-id]")).toHaveCount(2);

  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.locator('[data-widget-id="mobile-clock"]').focus();
  await page.keyboard.press("Delete");

  await expect(page.locator("[data-widget-id]")).toHaveCount(0);

  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();

  expect(state.writes).toHaveLength(0);

  await customize(page);
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.locator('[data-widget-id="mobile-clock"]').focus();
  await page.keyboard.press("Delete");
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.mobile!.widgets).toEqual([]);
  expect(state.saved.desktop).toEqual(seed().desktop);
});
