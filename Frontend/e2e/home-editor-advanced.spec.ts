import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  dashboardSettings,
  gridPoint,
  openWidgetTab,
  selectWidget,
} from "./helpers/home-grid-editor.js";

test.use({ viewport: { width: 1440, height: 950 }, serviceWorkers: "block" });

const customize = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await expect(page.getByRole("button", { name: "Undo", exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Redo", exact: true })).toBeDisabled();
};

const geometry = (card: Locator) =>
  card.locator("..").evaluate((element) => ({
    column: (element as HTMLElement).style.gridColumn,
    row: (element as HTMLElement).style.gridRow,
  }));

const heldArrow = async (page: Page, key: string) => {
  await page.keyboard.down(key);
  await page.keyboard.down(key);
  await page.keyboard.down(key);
  await page.keyboard.up(key);
};

const cancelDrag = async (page: Page, clock: Locator) => {
  const bounds = (await clock.boundingBox())!;

  await page.mouse.move(bounds.x + 24, bounds.y + 80);
  await page.mouse.down();
  await page.mouse.move(bounds.x + 180, bounds.y + 160, { steps: 6 });
  await expect(page.locator("[data-placement-ghost]")).toBeVisible();
  await page.keyboard.press("Escape");
  await page.mouse.up();
};

test("history groups held movement and resizing, restores displaced cards and ignores cancelled drags", async ({
  page,
}) => {
  const initial = seed();
  initial.desktop.widgets = [0, 1].map((index) => ({
    ...initial.desktop.widgets[0]!,
    instanceId: `clock-${index}`,
    x: index * 4,
    width: 4,
    height: 3,
  }));
  const state = await fixture(page, initial);
  const [clock, other] = [
    page.locator('[data-widget-id="clock-0"]'),
    page.locator('[data-widget-id="clock-1"]'),
  ];

  await customize(page);
  await clock.focus();
  await heldArrow(page, "ArrowRight");
  expect(await geometry(clock)).toEqual({ column: "4 / span 4", row: "1 / span 3" });
  expect((await geometry(other)).row).toBe("4 / span 3");
  await page.keyboard.press("Control+z");
  expect((await geometry(clock)).column).toBe("1 / span 4");
  expect((await geometry(other)).row).toBe("1 / span 3");
  await expect(page.getByRole("button", { name: "Undo", exact: true })).toBeDisabled();
  await page.keyboard.press("Control+y");

  await page.keyboard.down("Shift");
  await heldArrow(page, "ArrowRight");
  await page.keyboard.up("Shift");
  expect((await geometry(clock)).column).toBe("4 / span 7");
  await page.keyboard.press("Control+z");
  expect((await geometry(clock)).column).toBe("4 / span 4");

  await cancelDrag(page, clock);
  await page.getByRole("button", { name: "Undo", exact: true }).click();
  expect((await geometry(clock)).column).toBe("1 / span 4");
  await expect(page.getByRole("button", { name: "Undo", exact: true })).toBeDisabled();
  expect(state.writes).toHaveLength(0);
});

const configureAndDuplicate = async (page: Page) => {
  const clock = page.locator('[data-widget-id="default-clock"]');
  await selectWidget(page, clock);
  await page.getByRole("button", { name: "Quick widget style", exact: true }).click();
  const picker = page.locator('[aria-label="Quick style picker"]');
  await picker.getByRole("button", { name: "Flip", exact: true }).click();
  await expect(picker.getByRole("button", { name: "Flip", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await page.keyboard.press("Escape");
  await expect(picker).toHaveCount(0);
  await openWidgetTab(page, "Options");
  await page.getByRole("switch", { name: "Show seconds", exact: true }).click();
  await page.getByRole("button", { name: "Duplicate widget", exact: true }).click();
  await expect(page.locator("[data-widget-id]")).toHaveCount(3);
  await expect(page.locator('[data-selected="true"]')).toBeFocused();
};

test("quick styles and duplication preserve options, isolate copies and save only the final draft", async ({
  page,
}) => {
  const state = await fixture(page);
  await customize(page);
  await configureAndDuplicate(page);
  const copy = page.locator('[data-widget-id][data-selected="true"]');
  const copyId = await copy.getAttribute("data-widget-id");
  expect(copyId).not.toBe("default-clock");

  await page
    .getByRole("toolbar", { name: "Selected widget actions" })
    .getByRole("button", { name: "Remove selected widget", exact: true })
    .click();
  await expect(page.locator("[data-widget-id]")).toHaveCount(2);
  await page.getByRole("button", { name: "Undo", exact: true }).click();
  await expect(page.locator(`[data-widget-id="${copyId}"]`)).toBeVisible();
  await page.getByRole("button", { name: "Undo", exact: true }).click();
  await expect(page.locator("[data-widget-id]")).toHaveCount(2);
  await page.keyboard.press("Control+Shift+z");
  await expect(page.locator(`[data-widget-id="${copyId}"]`)).toBeVisible();
  await openWidgetTab(page, "Options");
  await page.getByRole("switch", { name: "Show seconds", exact: true }).click();
  await expect(page.getByRole("button", { name: "Redo", exact: true })).toBeDisabled();
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(
    state.saved.desktop.widgets.find((widget) => widget.instanceId === "default-clock")!.options,
  ).toMatchObject({ clockStyle: 3, showSeconds: true });
  expect(
    state.saved.desktop.widgets.find((widget) => widget.instanceId === copyId)!.options,
  ).toMatchObject({ clockStyle: 3, showSeconds: false });
  await customize(page);
  await page.screenshot({ path: "/tmp/alexandria-editor-advanced-desktop.png" });
});

const discardAfterFailedSave = async (page: Page) => {
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByText("Changes are not saved", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Undo", exact: true }).click();
  await expect(page.locator("[data-widget-id]")).toHaveCount(2);
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
};

test("history crosses layouts and reset, retains failed saves and Cancel discards the session", async ({
  page,
}) => {
  const state = await fixture(page);
  await customize(page);
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout", exact: true }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  const clock = page.locator('[data-widget-id][aria-label="Clock"]');
  await selectWidget(page, clock);
  await clock.focus();
  await page.keyboard.press("Control+d");
  await expect(page.locator("[data-widget-id]")).toHaveCount(3);
  await dashboardSettings(page);
  await page.getByRole("button", { name: "Reset this layout", exact: true }).click();
  await page.getByRole("button", { name: "Reset layout", exact: true }).click();
  await page.getByRole("button", { name: "Desktop", exact: true }).click();
  await page.getByRole("button", { name: "Undo", exact: true }).click();
  await expect(page.getByRole("button", { name: "Mobile", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await expect(page.locator("[data-widget-id]")).toHaveCount(3);

  state.failNextSave = true;
  await discardAfterFailedSave(page);
  expect(state.saved.mobile).toBeNull();
  await customize(page);
});

const moveWithToolbar = async (page: Page, clock: Locator) => {
  await selectWidget(page, clock);
  const card = (await clock.boundingBox())!;
  const grip = (await page
    .getByRole("button", { name: "Move selected widget", exact: true })
    .boundingBox())!;
  const start = { x: grip.x + grip.width / 2, y: grip.y + grip.height / 2 };
  const target = await gridPoint(page, 6, 0, { x: start.x - card.x, y: start.y - card.y });

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 12 });
  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-column", "6");
  await page.mouse.up();
};

test("completed pointer placement is one step and shortcuts leave inputs, popovers and Preview alone", async ({
  page,
}) => {
  await fixture(page);
  await customize(page);
  const clock = page.locator('[data-widget-id="default-clock"]');
  await moveWithToolbar(page, clock);
  await page.getByRole("button", { name: "Undo", exact: true }).click();
  expect((await geometry(clock)).column).toBe("1 / span 6");
  await expect(page.getByRole("button", { name: "Undo", exact: true })).toBeDisabled();
  await selectWidget(page, clock);
  await openWidgetTab(page, "Options");
  await page.getByRole("button", { name: "Time zone", exact: true }).click();
  const search = page.getByPlaceholder("Search time zones...");
  await search.fill("UTC");
  await search.press("Control+d");
  await search.press("Control+z");
  await expect(page.locator("[data-widget-id]")).toHaveCount(2);
  await expect(page.getByRole("button", { name: "Undo", exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Redo", exact: true })).toBeEnabled();
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Preview", exact: true }).click();
  await page.keyboard.press("Control+z");
  await page.keyboard.press("Control+d");
  await expect(page.getByRole("toolbar", { name: "Selected widget actions" })).toHaveCount(0);
  await expect(page.locator("[data-widget-id]")).toHaveCount(2);
});

test.describe("phone", () => {
  test.use({ hasTouch: true });

  test("quick actions fit the canvas and history stays available with the inspector dismissed", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const initial = seed();
    initial.useSeparateMobileLayout = true;
    initial.mobile = {
      columns: 4,
      widgets: [
        { ...initial.desktop.widgets[0]!, instanceId: "phone-clock", width: 2, height: 2, x: 2 },
      ],
    };
    await fixture(page, initial);
    await customize(page);
    await page.locator('[data-widget-id="phone-clock"]').tap();
    await page.getByRole("button", { name: "Back to layout", exact: true }).click();
    const toolbar = page.getByRole("toolbar", { name: "Selected widget actions" });
    const bounds = (await toolbar.boundingBox())!;
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(390);
    await toolbar.getByRole("button", { name: "Duplicate selected widget", exact: true }).click();
    await expect(page.locator("[data-widget-id]")).toHaveCount(2);
    await page.getByRole("button", { name: "Undo", exact: true }).click();
    await expect(page.locator("[data-widget-id]")).toHaveCount(1);
    await page.screenshot({ path: "/tmp/alexandria-editor-advanced-phone.png" });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
      true,
    );
  });
});
