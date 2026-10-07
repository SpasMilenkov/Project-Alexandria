import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  closeControls,
  dragCatalogue,
  dragWidget,
  openControls,
  removeWidget,
  selectWidget,
} from "./helpers/home-grid-editor.js";

const selectOption = async (page: Page, control: ReturnType<Page["getByRole"]>, option: string) => {
  await control.click();
  await page.getByRole("option", { name: option, exact: true }).click();

  await expect(page.getByRole("option", { name: option, exact: true })).not.toBeVisible();
};

const verifyFullWidth = async (page: Page) => {
  const bounds = await page
    .locator("main")
    .last()
    .evaluate((element) => ({
      width: element.getBoundingClientRect().width,
      available: element.parentElement!.clientWidth,
    }));

  expect(Math.abs(bounds.width - bounds.available)).toBeLessThanOrEqual(1);
};

const verifyCatalogueDirection = async (page: Page, direction: "right" | "bottom") => {
  await page.getByRole("button", { name: "Add widget", exact: true }).click();

  if (direction === "right") {
    const panel = page.getByRole("complementary", { name: "Widget catalogue" });

    await expect(panel).toBeVisible();

    const bounds = (await panel.boundingBox())!;

    expect(bounds.x).toBeGreaterThan(page.viewportSize()!.width / 2);
  } else {
    const drawer = page.getByRole("dialog", { name: "Add a widget" });

    await expect(drawer).toHaveAttribute("data-vaul-drawer-direction", "bottom");

    await drawer.getByRole("button", { name: "Add Clock", exact: true }).hover();

    expect((await drawer.boundingBox())!.width).toBe(page.viewportSize()!.width);
  }
};

const discardCustomization = async (page: Page) => {
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();
};

test.use({ serviceWorkers: "block" });

const verifyReordering = async (page: Page) => {
  const clock = page.locator('[data-widget-id="default-clock"]');
  const library = page.locator('[data-widget-id="default-library"]');

  await dragWidget(page, clock.locator("p").first(), 6, 0);
  await dragWidget(page, clock.getByRole("heading", { name: "Clock", exact: true }), 0, 0);
  await dragWidget(page, library.getByRole("link", { name: /File Explorer/u }), 0, 6);

  await expect(page).toHaveURL(/\/home$/u);

  await clock.focus();
  await clock.press("ArrowRight");

  await expect(page.getByText("Column 2, row 1", { exact: true })).toBeVisible();

  await clock.press("ArrowLeft");
};

const customizeDesktop = async (page: Page, state: Awaited<ReturnType<typeof fixture>>) => {
  const widgets = page.locator("[data-widget-id]");
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await verifyReordering(page);

  await selectWidget(page, clock);
  await selectOption(page, page.getByRole("combobox", { name: "Widget size" }), "Large");

  await page.getByRole("button", { name: "Widget options", exact: true }).click();
  await selectOption(page, page.getByRole("button", { name: "Time zone" }), "UTC");
  await page.getByRole("button", { name: "Close options", exact: true }).click();

  await page.getByRole("button", { name: "Add widget", exact: true }).click();
  await page.getByRole("button", { name: "Add Clock", exact: true }).click();

  await expect(widgets).toHaveCount(3);
  expect(state.writes).toHaveLength(0);
  await expect(page.getByRole("dialog", { name: "Add a widget" })).not.toBeVisible();

  await page.screenshot({ path: "/tmp/alexandria-home-desktop-editor.png", fullPage: true });
};

const saveWithRecovery = async (page: Page, state: Awaited<ReturnType<typeof fixture>>) => {
  const widgets = page.locator("[data-widget-id]");
  state.failNextSave = true;

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByText("Changes are not saved", { exact: true })).toBeVisible();
  await expect(widgets).toHaveCount(3);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  const clock = state.saved.desktop.widgets.find(
    (widget) => widget.instanceId === "default-clock",
  )!;

  expect(clock.options.timeZone).toBe("UTC");
  expect(clock.size).toBe(2);
};

const customizeMobile = async (page: Page) => {
  const widgets = page.locator("[data-widget-id]");
  const clock = page.locator('[data-widget-id="default-clock"]');
  const library = page.locator('[data-widget-id="default-library"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();

  await removeWidget(page, library);
  await removeWidget(
    page,
    page.locator('[data-widget-id]:not([data-widget-id="default-clock"])').last(),
  );

  await expect(widgets).toHaveCount(1);

  await selectWidget(page, clock);
  await page.getByRole("button", { name: "Widget options", exact: true }).click();
  await selectOption(page, page.getByRole("button", { name: "Time zone" }), "Asia / Tokyo");
  await page.getByRole("button", { name: "Close options", exact: true }).click();
  await page.screenshot({ path: "/tmp/alexandria-home-mobile-preview.png", fullPage: true });

  const preview = await page.locator('[data-preview="mobile"]').boundingBox();

  expect(preview!.width).toBeLessThanOrEqual(390);
};

const saveAndRestoreMobile = async (page: Page, state: Awaited<ReturnType<typeof fixture>>) => {
  const widgets = page.locator("[data-widget-id]");

  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();

  await expect(widgets).toHaveCount(3);
  await expect(page.getByRole("button", { name: "Reset this layout" })).toBeDisabled();

  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();

  await expect(widgets).toHaveCount(1);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets).toHaveLength(3);
  expect(state.saved.mobile!.widgets).toHaveLength(1);
  expect(state.saved.mobile!.widgets[0]!.options.timeZone).toBe("Asia/Tokyo");
};

const verifyResponsiveReload = async (page: Page, state: Awaited<ReturnType<typeof fixture>>) => {
  const widgets = page.locator("[data-widget-id]");
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.reload();

  await expect(widgets).toHaveCount(3);

  await page.setViewportSize({ width: 390, height: 844 });

  await expect(widgets).toHaveCount(1);
  await expect(clock.getByText("Asia / Tokyo", { exact: true })).toBeVisible();

  await page.screenshot({ path: "/tmp/alexandria-home-mobile.png", fullPage: true });

  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openControls(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await closeControls(page);
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  await expect(widgets).toHaveCount(3);
  expect(state.saved.mobile!.widgets).toHaveLength(1);
};

test("Home saves on Done, recovers failed saves and retains independent mobile composition", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const state = await fixture(page);

  await customizeDesktop(page, state);
  await saveWithRecovery(page, state);
  await customizeMobile(page);
  await saveAndRestoreMobile(page, state);
  await verifyResponsiveReload(page, state);
});

test("Cancel and navigation protection preserve saved Home; empty layouts survive reload", async ({
  page,
}) => {
  const state = await fixture(page);
  const widgets = page.locator("[data-widget-id]");

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await removeWidget(page, widgets.first());
  await page.locator('a[href="/dashboard"]').first().click();
  await page.getByRole("button", { name: "Keep editing", exact: true }).click();

  await expect(page).toHaveURL(/\/home$/u);

  await discardCustomization(page);

  await expect(widgets).toHaveCount(2);
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await removeWidget(page, widgets.first());
  await removeWidget(page, widgets.first());

  await expect(
    page.getByText("Add a widget to start building your layout.", { exact: true }),
  ).toBeVisible();

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  await page.reload();

  await expect(widgets).toHaveCount(0);
  await expect(page.getByText("Your dashboard is empty", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Reset this layout" }).click();

  await expect(widgets).toHaveCount(2);
  expect(state.saved.desktop.widgets).toEqual([]);

  await discardCustomization(page);

  await expect(widgets).toHaveCount(0);
});

test("touch controls edit the shared layout and build a separate phone layout", async ({
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
    const state = await fixture(page);
    const widgets = page.locator("[data-widget-id]");

    await page.getByRole("button", { name: "Customize", exact: true }).tap();
    await widgets.first().tap();
    await openControls(page);
    await page.getByRole("button", { name: "Move widget down", exact: true }).tap();

    await expect(page.getByText("Column 1, row 2", { exact: true })).toBeVisible();

    await page.getByRole("switch", { name: "Use a separate mobile layout" }).tap();
    await page.getByRole("button", { name: "Mobile", exact: true }).tap();
    await closeControls(page);
    await removeWidget(page, widgets.first());

    await expect(widgets).toHaveCount(1);
    expect(state.writes).toHaveLength(0);

    await page.screenshot({ path: "/tmp/alexandria-home-touch-editor.png", fullPage: true });
    await page.getByRole("button", { name: "Done", exact: true }).tap();

    await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
    expect(state.saved.desktop.widgets).toHaveLength(2);
    expect(state.saved.mobile!.widgets).toHaveLength(1);
  } finally {
    await context.close();
  }
});

test("Home fills a wide screen and the catalogue adapts to the viewport", async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await fixture(page);
  await verifyFullWidth(page);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await verifyFullWidth(page);
  await verifyCatalogueDirection(page, "right");
  await page.screenshot({ path: "/tmp/alexandria-home-side-catalogue.png", fullPage: true });
  await page.keyboard.press("Escape");

  await expect(page.getByRole("dialog", { name: "Add a widget" })).not.toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await verifyFullWidth(page);
  await verifyCatalogueDirection(page, "bottom");
  await page.screenshot({ path: "/tmp/alexandria-home-bottom-catalogue.png", fullPage: true });
  await page.keyboard.press("Escape");

  await expect(page.getByRole("dialog", { name: "Add a widget" })).not.toBeVisible();

  await page.setViewportSize({ width: 1280, height: 900 });
  await verifyFullWidth(page);
  await verifyCatalogueDirection(page, "right");
});

test("Escape confirms unchanged drafts and Enter saves or keeps a failed draft open", async ({
  page,
}) => {
  const state = await fixture(page);
  const widgets = page.locator("[data-widget-id]");

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.keyboard.press("Escape");

  const modal = page.getByRole("dialog", { name: "Leave customization?" });

  await expect(modal).toBeVisible();
  await expect(modal).toContainText("Your layout has no changes.");

  await modal.getByRole("button", { name: "Keep editing" }).click();

  await expect(modal).not.toBeVisible();

  await removeWidget(page, widgets.first());
  await page.keyboard.press("Escape");

  await expect(modal).toBeVisible();

  state.failNextSave = true;

  await page.keyboard.press("Enter");

  await expect(modal.getByText("Changes are not saved", { exact: true })).toBeVisible();
  await expect(widgets).toHaveCount(1);

  await page.keyboard.press("Enter");

  await expect(modal).not.toBeVisible();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets).toHaveLength(1);
});

test("Cancel and route exits confirm unchanged drafts and route discard preserves saved settings", async ({
  page,
}) => {
  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Cancel", exact: true }).click();

  const modal = page.getByRole("dialog", { name: "Leave customization?" });

  await expect(modal).toBeVisible();

  await modal.getByRole("button", { name: "Keep editing" }).click();

  await expect(modal).not.toBeVisible();

  await page.locator('a[href="/dashboard"]').first().click();

  await expect(modal).toBeVisible();

  await modal.getByRole("button", { name: "Keep editing" }).click();

  await expect(page).toHaveURL(/\/home$/u);

  await removeWidget(page, page.locator("[data-widget-id]").first());
  await page.locator('a[href="/dashboard"]').first().click();
  await modal.getByRole("button", { name: "Discard and exit" }).click();

  await expect(page).toHaveURL(/\/dashboard$/u);
  expect(state.writes).toHaveLength(0);
  expect(state.saved.desktop.widgets).toHaveLength(2);
});

const openMobileWorkspace = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();

  const preview = page.getByRole("region", { name: "Mobile layout preview", exact: true });
  const catalogue = page.getByRole("complementary", { name: "Widget catalogue", exact: true });

  await expect(preview).toBeVisible();
  await expect(catalogue).toBeVisible();
  await expect(preview.getByText("Your phone dashboard", { exact: true })).toBeVisible();

  const phoneBounds = await preview.boundingBox();
  const catalogueBounds = await catalogue.boundingBox();

  expect(catalogueBounds!.x).toBeGreaterThanOrEqual(phoneBounds!.x + phoneBounds!.width);

  return { preview, catalogue };
};

const composeMobileByDragging = async (page: Page) => {
  const { preview, catalogue } = await openMobileWorkspace(page);
  const widgets = preview.locator("[data-widget-id]");

  await removeWidget(page, widgets.first());
  await removeWidget(page, widgets.first());
  await dragCatalogue(page, 0, 0, 0);

  await expect(widgets).toHaveCount(1);

  await dragCatalogue(page, 1, 0, 0);

  await expect(widgets).toHaveCount(2);
  await expect(
    widgets.first().getByRole("heading", { name: "Library shortcuts", exact: true }),
  ).toBeVisible();

  await catalogue.getByRole("button", { name: "Add Clock", exact: true }).click();

  await expect(widgets).toHaveCount(3);
  await expect(catalogue).toBeVisible();

  return { preview, catalogue };
};

test("desktop mobile workspace has phone guidance and supports catalogue drop with independent persistence", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const state = await fixture(page);
  const { preview } = await composeMobileByDragging(page);

  expect(state.writes).toHaveLength(0);

  await page.screenshot({ path: "/tmp/alexandria-mobile-editor-workspace.png", fullPage: true });

  expect(
    (await preview.locator('[data-preview="mobile"]').boundingBox())!.width,
  ).toBeLessThanOrEqual(390);

  await page.getByRole("button", { name: "Desktop", exact: true }).click();

  await expect(page.locator("[data-widget-id]")).toHaveCount(2);
  await expect(page.locator("[data-widget-id]").first()).toHaveAttribute(
    "data-widget-id",
    "default-clock",
  );

  await page.getByRole("button", { name: "Mobile", exact: true }).click();

  await expect(preview.locator("[data-widget-id]")).toHaveCount(3);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets.map((widget) => widget.type)).toEqual([0, 1]);
  expect(state.saved.mobile!.widgets.map((widget) => widget.type)).toEqual([1, 0, 0]);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();

  await expect(page.locator("[data-widget-id]")).toHaveCount(3);
});

test("shared preview blocks additions and Escape closes the dock before exit confirmation", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();

  const catalogue = page.getByRole("complementary", { name: "Widget catalogue", exact: true });

  await expect(page.getByText("Shared layout preview", { exact: true })).toBeVisible();
  await expect(catalogue.getByRole("button", { name: "Add Clock" })).toBeDisabled();
  await expect(
    catalogue.getByText("Enable a separate mobile layout to add widgets here."),
  ).toBeVisible();
  await expect(catalogue.locator('[data-catalogue-widget="0"]')).toHaveAttribute(
    "data-draggable",
    "false",
  );

  await page.keyboard.press("Escape");

  await expect(catalogue).not.toBeVisible();
  await expect(page.getByRole("dialog", { name: "Leave customization?" })).not.toBeVisible();

  await page.keyboard.press("Escape");

  await expect(page.getByRole("dialog", { name: "Leave customization?" })).toBeVisible();

  await page.getByRole("button", { name: "Keep editing" }).click();
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Add widget", exact: true }).click();

  await expect(catalogue).toBeVisible();
  expect(state.writes).toHaveLength(0);
});

test("native tab close warns about unsaved Home changes and allows closing after Done", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Move widget right", exact: true }).click();

  const dialogEvent = page.waitForEvent("dialog");

  await page.close({ runBeforeUnload: true });

  const dialog = await dialogEvent;

  expect(dialog.type()).toBe("beforeunload");

  await dialog.dismiss();

  await expect(page.getByText("Column 2, row 1", { exact: true })).toBeVisible();
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.writes).toHaveLength(1);

  const closed = page.waitForEvent("close");

  await page.close({ runBeforeUnload: true });
  await closed;
});

test("mobile catalogue enforces the widget limit for drops and Add buttons", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1400 });

  const initial = seed();
  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: Array.from({ length: 32 }, (_, index) => ({
      ...initial.desktop.widgets[0]!,
      instanceId: `mobile-${index}`,
      width: 4,
      y: index * 4,
    })),
  };
  const state = await fixture(page, initial);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();

  const preview = page.getByRole("region", { name: "Mobile layout preview", exact: true });
  const catalogue = page.getByRole("complementary", { name: "Widget catalogue", exact: true });

  await expect(catalogue.getByRole("button", { name: "Add Clock", exact: true })).toBeDisabled();

  await preview.evaluate((element) => {
    const data = new DataTransfer();

    data.setData("application/x-alexandria-home-widget", "0");
    element.dispatchEvent(new DragEvent("drop", { bubbles: true, dataTransfer: data }));
  });

  await expect(preview.locator("[data-widget-id]")).toHaveCount(32);

  await removeWidget(page, preview.locator("[data-widget-id]").first());
  await catalogue.getByRole("button", { name: "Add Clock", exact: true }).click();

  await expect(preview.locator("[data-widget-id]")).toHaveCount(32);
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.mobile!.widgets).toHaveLength(32);
  expect(state.saved.desktop.widgets).toHaveLength(2);
});
