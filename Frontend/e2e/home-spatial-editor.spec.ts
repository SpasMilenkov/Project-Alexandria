import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  dragCatalogue,
  dragTouchWidget,
  dragWidget,
  gridPoint,
} from "./helpers/home-grid-editor.js";

test("medium clock snaps to the right and persists after dragging from content", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const state = await fixture(page);
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await dragWidget(page, clock.locator("p").first(), 6, 0);

  await expect(page.getByText("Column 7, row 1", { exact: true })).toBeVisible();
  expect(state.writes).toHaveLength(0);

  await page.screenshot({ path: "/tmp/alexandria-spatial-right-clock.png", fullPage: true });
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]).toMatchObject({ x: 6, y: 0, width: 6, height: 4 });

  await page.reload();

  await expect(clock).toBeVisible();
  expect(await clock.locator("..").evaluate((element) => element.style.gridColumn)).toBe(
    "7 / span 6",
  );
});

test("collision ghost previews displacement, and Escape cancels without exiting editing", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const clock = page.locator('[data-widget-id="default-clock"]');
  const library = page.locator('[data-widget-id="default-library"]');

  await clock.scrollIntoViewIfNeeded();

  const start = (await clock.boundingBox())!;
  const target = await gridPoint(page, 0, 4);

  await page.mouse.move(start.x + 16, start.y + 16);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 10 });

  const ghost = page.locator("[data-placement-ghost]");

  await expect(ghost).toHaveAttribute("data-valid", "true");
  await expect(ghost).toHaveAttribute("data-row", "4");
  expect(await library.locator("..").evaluate((element) => element.style.gridRow)).toBe(
    "9 / span 6",
  );
  expect(state.saved.desktop.widgets[1]!.y).toBe(4);

  await page.screenshot({ path: "/tmp/alexandria-spatial-collision-ghost.png", fullPage: true });
  await page.keyboard.press("Escape");
  await page.mouse.up();

  await expect(ghost).toHaveCount(0);
  await expect(page.getByRole("dialog", { name: "Leave customization?" })).not.toBeVisible();
  expect(await library.locator("..").evaluate((element) => element.style.gridRow)).toBe(
    "5 / span 6",
  );
  expect(state.writes).toHaveLength(0);
});

test("desktop catalogue places directly into right-side cells and controls never start dragging", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Add widget", exact: true }).click();
  await dragCatalogue(page, 0, 6, 0);

  await expect(page.locator("[data-widget-id]")).toHaveCount(3);
  await expect(page.getByText("Column 7, row 1", { exact: true })).toBeVisible();

  const inspector = page.getByRole("region", { name: "Widget editing controls" });

  await inspector.getByRole("button", { name: "Move widget left" }).click();

  await expect(page.getByText("Column 6, row 1", { exact: true })).toBeVisible();
  await expect(page.locator("[data-placement-ghost]")).toHaveCount(0);
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets.some((widget) => widget.x === 5)).toBe(true);
});

for (const viewport of [
  { label: "desktop", size: { width: 1440, height: 1200 } },
  { label: "phone", size: { width: 390, height: 844 } },
]) {
  test(`dark default background scopes grid guidance on ${viewport.label}`, async ({ page }) => {
    await page.setViewportSize(viewport.size);
    await page.addInitScript(() => localStorage.setItem("vueuse-color-scheme", "dark"));
    await fixture(page, seed(), {
      backgroundColor: "system",
      fontFamily: "mono",
      accentColor: "blue",
    });
    await page.getByRole("button", { name: "Customize", exact: true }).click();

    await expect(page.locator("html")).toHaveClass(/dark/u);

    const styles = await page.locator("body").evaluate((body) => ({
      background: getComputedStyle(body).backgroundColor,
      canvas: getComputedStyle(document.querySelector('[aria-label="Home widgets"]')!).position,
      rootImage: getComputedStyle(document.documentElement).backgroundImage,
      bodyImage: getComputedStyle(body).backgroundImage,
    }));

    await page.screenshot({
      path: `/tmp/alexandria-spatial-dark-${viewport.label}.png`,
      fullPage: true,
    });

    expect(styles.canvas).toBe("relative");
    expect(styles.rootImage).toBe("none");
    expect(styles.bodyImage).toBe("none");
    expect(styles.background).not.toBe("rgba(0, 0, 0, 0)");
    expect(styles.background).not.toBe("rgb(255, 255, 255)");
  });
}

test("actual touch dragging moves a small mobile clock to the right without changing desktop", async ({
  browser,
}) => {
  const context = await browser.newContext({
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
    serviceWorkers: "block",
    baseURL: test.info().project.use.baseURL,
  });

  try {
    const page = await context.newPage();
    const initial = seed();
    initial.useSeparateMobileLayout = true;
    initial.mobile = {
      columns: 4,
      widgets: [{ ...initial.desktop.widgets[0]!, x: 0, y: 0, width: 2, height: 3, size: 0 }],
    };
    const state = await fixture(page, initial);

    await page.getByRole("button", { name: "Customize", exact: true }).tap();

    const clock = page.locator('[data-widget-id="default-clock"]');

    await dragTouchWidget(page, clock, 2, 1);

    expect(await clock.locator("..").evaluate((element) => element.style.gridColumn)).toBe(
      "3 / span 2",
    );
    expect(state.writes).toHaveLength(0);

    await page.getByRole("button", { name: "Done", exact: true }).tap();

    await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
    expect(state.saved.mobile!.widgets[0]).toMatchObject({ x: 2, y: 1, width: 2, height: 3 });
    expect(state.saved.desktop).toEqual(seed().desktop);

    await page.screenshot({ path: "/tmp/alexandria-spatial-touch-phone.png", fullPage: true });
  } finally {
    await context.close();
  }
});

test("dragging near the phone viewport edge scrolls its canvas and cancellation restores positions", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const initial = seed();
  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: initial.desktop.widgets.map((widget) => ({ ...widget, width: 4 })),
  };
  initial.mobile.widgets[1]!.y = 20;
  const state = await fixture(page, initial);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();

  const clock = page.locator('[data-widget-id="default-clock"]');

  await clock.scrollIntoViewIfNeeded();

  const viewport = page.locator('[data-preview="mobile"]');
  const start = (await clock.boundingBox())!;
  const visible = (await viewport.boundingBox())!;

  await page.mouse.move(start.x + 16, start.y + 16);
  await page.mouse.down();
  await page.mouse.move(start.x + 32, Math.min(visible.y + visible.height, 1400) - 12, {
    steps: 8,
  });

  await expect.poll(() => viewport.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);

  await page.keyboard.press("Escape");
  await page.mouse.up();

  expect(state.writes).toHaveLength(0);
  expect(state.saved.mobile!.widgets[0]!.y).toBe(0);
  await expect(page.locator("[data-placement-ghost]")).toHaveCount(0);
});

test("an overflowing collision shows an invalid ghost and leaves the draft unchanged", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const initial = seed();
  initial.desktop.widgets[0]!.y = 250;
  initial.desktop.widgets[1]!.y = 254;
  initial.desktop.widgets[1]!.height = 2;
  const state = await fixture(page, initial);

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const clock = page.locator('[data-widget-id="default-clock"]');

  await clock.scrollIntoViewIfNeeded();

  const start = (await clock.boundingBox())!;
  const target = await gridPoint(page, 0, 251);

  await page.mouse.move(start.x + 16, start.y + 16);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 8 });

  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-valid", "false");

  await page.mouse.up();

  expect(await clock.locator("..").evaluate((element) => element.style.gridRow)).toBe(
    "251 / span 4",
  );
  expect(state.writes).toHaveLength(0);
});

const verifyControlsStayVisible = async (page: Page) => {
  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });

  await page.mouse.move(700, 800);
  await page.mouse.wheel(0, 640);
  await expect
    .poll(async () => (await page.locator('[aria-label="Home widgets"]').boundingBox())!.y)
    .toBeLessThan(0);

  expect((await panel.boundingBox())!.y).toBeGreaterThanOrEqual(128);
  await expect(panel.getByRole("combobox", { name: "Widget size" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Done", exact: true })).toBeVisible();
};

test("desktop preview fills the board width and restores the unsaved editor", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const grid = page.locator('[aria-label="Home widgets"]');
  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });
  const clock = page.locator('[data-widget-id="default-clock"]');

  await dragWidget(page, clock, 6, 0);

  const edited = (await grid.boundingBox())!;

  expect(Math.abs(edited.y - (await panel.boundingBox())!.y)).toBeLessThanOrEqual(1);

  await page.getByRole("button", { name: "Preview", exact: true }).click();

  await expect(panel).not.toBeVisible();
  await expect(grid).toHaveAttribute("data-editing", "false");
  await expect(grid.locator(".grid-guidance")).toHaveCount(0);

  const preview = (await grid.boundingBox())!;

  expect(preview.width).toBeGreaterThan(edited.width + 300);
  expect(await clock.locator("..").evaluate((element) => element.style.gridColumn)).toBe(
    "7 / span 6",
  );
  expect(state.writes).toHaveLength(0);

  await page.screenshot({ path: "/tmp/alexandria-spatial-full-width-preview.png", fullPage: true });

  await page.getByRole("button", { name: "Back to editing", exact: true }).click();

  await expect(panel).toBeVisible();
  await expect(grid).toHaveAttribute("data-editing", "true");
  expect((await grid.boundingBox())!.width).toBe(edited.width);
  await expect(page.getByText("Column 7, row 1", { exact: true })).toBeVisible();
  expect(state.writes).toHaveLength(0);

  await verifyControlsStayVisible(page);
});

test("Done saves from Preview, retains a failed draft, and returns to the live dashboard", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Move widget right", exact: true }).click();
  await page.getByRole("button", { name: "Preview", exact: true }).click();

  state.failNextSave = true;

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByText("Changes are not saved", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Back to editing", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: /Alex/u })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.x).toBe(1);
});
