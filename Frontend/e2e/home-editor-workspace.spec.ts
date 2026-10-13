import { type Page, expect, test } from "@playwright/test";

import { fixture } from "./helpers/home-dashboard.js";
import { dragCatalogue } from "./helpers/home-grid-editor.js";

const configureClock = async (page: Page) => {
  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });

  await panel.getByRole("button", { name: "Edit Clock", exact: true }).click();
  await expect(panel.getByRole("tab", { name: "Style", exact: true })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await page.screenshot({ path: "/tmp/alexandria-editor-workspace-styles.png" });

  await panel.getByRole("button", { name: "Watch", exact: true }).click();
  await panel.getByRole("tab", { name: "Options", exact: true }).click();
  await panel.getByRole("button", { name: "Position clock at middle center", exact: true }).click();
  await panel.getByRole("tab", { name: "Layout", exact: true }).click();
  await panel.getByRole("button", { name: "Decrease width", exact: true }).click();
  await panel.getByRole("button", { name: "Move widget right", exact: true }).click();
};

const addStorage = async (page: Page) => {
  await page.getByRole("button", { name: "Back to dashboard settings", exact: true }).click();
  await page.getByRole("button", { name: "Add widget", exact: true }).click();

  const catalogue = page.getByRole("region", { name: "Widget catalogue", exact: true });
  await catalogue.getByRole("button", { name: "Files", exact: true }).click();
  await catalogue.getByRole("textbox", { name: "Search widgets", exact: true }).fill("upload");
  await expect(catalogue.locator("[data-catalogue-widget]")).toHaveCount(1);
  await catalogue.getByRole("textbox", { name: "Search widgets", exact: true }).fill("storage");

  await page.screenshot({ path: "/tmp/alexandria-editor-workspace-catalogue.png" });
  await dragCatalogue(page, 2, 6, 0);
  await expect(page.locator('[data-widget-id][aria-label="Storage"]')).toHaveCount(1);
};

const previewDesktop = async (page: Page, previousWidth: number) => {
  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });

  await page.getByRole("button", { name: "Preview", exact: true }).click();
  await expect(panel).toHaveCount(0);
  expect((await page.locator(".home-canvas").boundingBox())!.width).toBeGreaterThan(
    previousWidth + 250,
  );
  await expect(page.locator("[data-resize-edge]")).toHaveCount(0);
  await page.getByRole("button", { name: "Back to editing", exact: true }).click();
};

test("workspace keeps the canvas beside the inspector and saves supported widget controls", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 950 });
  await page.addInitScript(() => localStorage.setItem("vueuse-color-scheme", "dark"));

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });
  const canvas = page.locator(".home-canvas");
  await expect(panel.getByRole("heading", { name: "Dashboard", exact: true })).toBeVisible();

  const panelBounds = (await panel.boundingBox())!;
  const canvasBounds = (await canvas.boundingBox())!;

  expect(Math.abs(panelBounds.y - canvasBounds.y)).toBeLessThan(2);
  expect(canvasBounds.x + canvasBounds.width).toBeLessThanOrEqual(panelBounds.x + 1);

  await configureClock(page);

  await page.screenshot({ path: "/tmp/alexandria-editor-workspace-selected.png" });
  await previewDesktop(page, canvasBounds.width);
  await addStorage(page);

  expect(state.writes).toHaveLength(0);
  await expect(
    page.getByRole("button", { name: "Position clock at middle center", exact: true }),
  ).toHaveCount(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.desktop.widgets[0]).toMatchObject({
    width: 5,
    x: 1,
    options: { clockStyle: 1, horizontalAlignment: 1 },
  });
  expect(state.saved.desktop.widgets).toHaveLength(3);

  await page.reload();
  await expect(page.locator('[data-widget-id][aria-label="Storage"]')).toHaveCount(1);
});

const preparePhone = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Layout controls", exact: true }).click();

  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });
  await panel.getByRole("switch", { name: "Use a separate mobile layout", exact: true }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await panel.getByRole("button", { name: "Expand inspector", exact: true }).click();
  await expect(panel).toHaveClass(/is-expanded/u);
  await panel.getByRole("button", { name: "Reset this layout", exact: true }).click();
  await expect(
    panel.getByRole("group", { name: "Confirm layout reset", exact: true }),
  ).toBeVisible();
  await panel.getByRole("button", { name: "Keep layout", exact: true }).click();
  await panel.getByRole("button", { name: "Back to layout", exact: true }).click();
  await expect(panel).toHaveCount(0);
};

const discardPhoneReset = async (page: Page) => {
  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });

  await page.getByRole("button", { name: "Back to editing", exact: true }).click();
  await page.getByRole("button", { name: "Layout controls", exact: true }).click();
  await panel.getByRole("button", { name: "Back to dashboard settings", exact: true }).click();
  await panel.getByRole("button", { name: "Reset this layout", exact: true }).click();
  await panel.getByRole("button", { name: "Reset layout", exact: true }).click();
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
};

test("phone inspector expands, dismisses, previews and confirms current-layout reset", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const state = await fixture(page);
  await preparePhone(page);

  const panel = page.getByRole("complementary", { name: "Dashboard editing panel" });

  await page.getByRole("button", { name: "Add widget", exact: true }).click();

  const catalogue = page.getByRole("region", { name: "Widget catalogue", exact: true });
  await catalogue.getByRole("textbox", { name: "Search widgets", exact: true }).fill("Clock");
  await catalogue.getByRole("button", { name: "Add Clock", exact: true }).click();
  await expect(panel).toBeVisible();
  await panel.getByRole("tab", { name: "Options", exact: true }).click();

  await page.screenshot({ path: "/tmp/alexandria-editor-workspace-phone.png" });
  await page.keyboard.press("Escape");
  await expect(panel).toHaveCount(0);
  await page.getByRole("button", { name: "Preview", exact: true }).click();
  await expect(page.locator("[data-resize-edge]")).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  expect(state.writes).toHaveLength(0);

  await discardPhoneReset(page);

  expect(state.saved.mobile).toBeNull();
  expect(state.writes).toHaveLength(0);
});
