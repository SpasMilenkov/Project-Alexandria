import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { resizePoints, resizeTouchWidget, resizeWidget } from "./helpers/home-grid-editor.js";

const rectangle = (widget: Locator) =>
  widget.locator("..").evaluate((element) => ({
    column: element.style.gridColumn,
    row: element.style.gridRow,
  }));

const verifyWideClock = async (clock: Locator) => {
  expect(
    await clock
      .locator(".clock-content")
      .evaluate((element) => getComputedStyle(element).flexDirection),
  ).toBe("row");
  expect(await clock.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(true);
};

const restoreSmallPreset = async (page: Page, clock: Locator) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("combobox", { name: "Widget size" }).click();
  await page.getByRole("option", { name: "Small", exact: true }).click();

  expect(await rectangle(clock)).toEqual({ column: "1 / span 3", row: "1 / span 3" });
};

test("corner resizing previews adaptive content and saves custom geometry through reload and presets", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const state = await fixture(page);
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await resizeWidget(page, clock, "bottom-right", 3, -2);

  expect(await rectangle(clock)).toEqual({ column: "1 / span 9", row: "1 / span 2" });
  await expect(page.getByRole("combobox", { name: "Widget size" })).toContainText("Custom");

  await verifyWideClock(clock);

  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Preview", exact: true }).click();

  await expect(page.locator("[data-resize-edge]")).toHaveCount(0);

  await verifyWideClock(clock);
  await page.screenshot({ path: "/tmp/alexandria-resize-clock-wide.png", fullPage: true });
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]).toMatchObject({ width: 9, height: 2, size: 0 });

  await page.reload();

  await expect(clock).toBeVisible();
  expect(await rectangle(clock)).toEqual({ column: "1 / span 9", row: "1 / span 2" });

  await restoreSmallPreset(page, clock);

  expect(state.saved.desktop.widgets[0]!.height).toBe(2);
});

test("top and left handles anchor opposite edges and keyboard handles resize without moving", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = seed();
  initial.desktop.widgets = [{ ...initial.desktop.widgets[0]!, x: 6, y: 4 }];

  await fixture(page, initial);

  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await resizeWidget(page, clock, "top-left", -2, -2);

  expect(await rectangle(clock)).toEqual({ column: "5 / span 8", row: "3 / span 6" });

  await resizeWidget(page, clock, "bottom-right", -5, 2);

  expect(await rectangle(clock)).toEqual({ column: "5 / span 3", row: "3 / span 8" });
  expect(
    await clock
      .locator(".clock-content")
      .evaluate((element) => getComputedStyle(element).flexDirection),
  ).toBe("column");

  const corner = clock.locator("..").locator('[data-resize-edge="bottom-right"]');

  await corner.focus();
  await corner.press("ArrowRight");
  await corner.press("ArrowDown");

  expect(await rectangle(clock)).toEqual({ column: "5 / span 4", row: "3 / span 9" });

  await page.screenshot({ path: "/tmp/alexandria-resize-clock-tall.png", fullPage: true });
});

test("resize collisions preview before release and Escape restores the unmodified draft", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const state = await fixture(page);
  const clock = page.locator('[data-widget-id="default-clock"]');
  const library = page.locator('[data-widget-id="default-library"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const { start, end } = await resizePoints(page, clock, "bottom", 0, 2);

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(end.x, end.y, { steps: 10 });
  await page.keyboard.press("ArrowDown");

  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-height", "6");
  expect((await rectangle(library)).row).toBe("7 / span 6");
  expect(state.saved.desktop.widgets[1]!.y).toBe(4);

  await page.keyboard.press("Escape");
  await page.mouse.up();

  expect((await rectangle(clock)).row).toBe("1 / span 4");
  expect((await rectangle(library)).row).toBe("5 / span 6");
  await expect(page.getByRole("dialog", { name: "Leave customization?" })).not.toBeVisible();
  expect(state.writes).toHaveLength(0);
});

test("an overflowing resize collision is rejected without partially editing the draft", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = seed();
  initial.desktop.widgets[0]!.y = 250;
  initial.desktop.widgets[1]!.y = 254;
  initial.desktop.widgets[1]!.height = 2;
  const state = await fixture(page, initial);
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const { start, end } = await resizePoints(page, clock, "bottom", 0, 1);

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(end.x, end.y, { steps: 10 });

  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-valid", "false");

  await page.mouse.up();

  expect((await rectangle(clock)).row).toBe("251 / span 4");
  expect(state.writes).toHaveLength(0);
});

test("shortcut content adapts between a wide strip and a tall narrow arrangement", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = seed();
  initial.desktop.widgets = [{ ...initial.desktop.widgets[1]!, x: 0, y: 0, height: 2 }];

  await fixture(page, initial);

  const library = page.locator('[data-widget-id="default-library"]');
  const links = library.getByRole("link");

  await expect(links).toHaveCount(4);

  const wide = await links.evaluateAll((elements) =>
    elements.map((element) => element.getBoundingClientRect().y),
  );

  expect(new Set(wide).size).toBe(1);

  await page.screenshot({ path: "/tmp/alexandria-resize-shortcuts-wide.png", fullPage: true });
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await resizeWidget(page, library, "bottom-right", -9, 6);

  const tall = await links.evaluateAll((elements) =>
    elements.map((element) => element.getBoundingClientRect().y),
  );

  expect(new Set(tall).size).toBe(4);
  expect(await library.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(
    true,
  );

  await page.screenshot({ path: "/tmp/alexandria-resize-shortcuts-tall.png", fullPage: true });
});

test("touch corner resizing saves independent mobile dimensions and displaced widgets", async ({
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
      widgets: [
        { ...initial.desktop.widgets[0]!, x: 0, y: 0, width: 2, height: 3, size: 0 },
        { ...initial.desktop.widgets[1]!, x: 2, y: 0, width: 2, height: 3, size: 0 },
      ],
    };
    const state = await fixture(page, initial);

    await page.getByRole("button", { name: "Customize", exact: true }).tap();

    const clock = page.locator('[data-widget-id="default-clock"]');

    await resizeTouchWidget(page, clock, "bottom-right", 2, 1);

    expect(await rectangle(clock)).toEqual({ column: "1 / span 4", row: "1 / span 4" });
    expect(state.writes).toHaveLength(0);

    await page.screenshot({ path: "/tmp/alexandria-resize-phone.png", fullPage: true });
    await page.getByRole("button", { name: "Done", exact: true }).tap();

    await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
    expect(state.saved.mobile!.widgets[0]).toMatchObject({ width: 4, height: 4 });
    expect(state.saved.mobile!.widgets[1]).toMatchObject({ x: 2, y: 4 });
    expect(state.saved.desktop).toEqual(seed().desktop);
  } finally {
    await context.close();
  }
});
