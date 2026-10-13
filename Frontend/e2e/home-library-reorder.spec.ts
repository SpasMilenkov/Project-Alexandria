import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { closeWidgetSettings, openWidgetTab, selectWidget } from "./helpers/home-grid-editor.js";

const document = () => {
  const value = seed();
  value.desktop.widgets = [
    {
      ...value.desktop.widgets[1]!,
      width: 6,
      height: 4,
      options: { ...value.desktop.widgets[1]!.options, libraryStyle: 3 },
    },
  ];

  return value;
};

const widgets = (page: Page) => page.locator("[data-widget-id]");

const openLibraryOptions = async (page: Page, widget: Locator) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, widget);
  await openWidgetTab(page, "Options");
};

const dragShortcut = async (page: Page, source: Locator, target: Locator) => {
  await source.scrollIntoViewIfNeeded();
  const from = (await source.boundingBox())!;
  const to = (await target.boundingBox())!;

  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(to.x + 24, to.y + 8, { steps: 12 });
  await expect(target).toHaveAttribute("data-drop-edge", "false");
};

const shortcutOrder = (widget: Locator) =>
  widget
    .getByRole("link")
    .evaluateAll((links) => links.map((link) => link.getAttribute("aria-label")));

const checkKeyboardBounds = async (page: Page, handle: Locator, widget: Locator) => {
  await handle.focus();
  await page.keyboard.press("Home");
  await page.keyboard.press("ArrowUp");
  await expect
    .poll(() => shortcutOrder(widget))
    .toEqual(["Access History", "File Explorer", "Tags and Categories", "Your Storage"]);
  await page.keyboard.press("End");
  await page.keyboard.press("ArrowDown");
  await expect
    .poll(() => shortcutOrder(widget))
    .toEqual(["File Explorer", "Tags and Categories", "Your Storage", "Access History"]);
  await expect(handle).toBeFocused();
};

test("dragging destination handles saves order with Done and preserves labels and feature", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets[0]!.options = {
    ...value.desktop.widgets[0]!.options,
    libraryStyle: 3,
    shortcutGroup: 3,
    shortcuts: [
      { destination: 0, label: "Files" },
      { destination: 1, label: null },
      { destination: 2, label: null },
      { destination: 3, label: null },
    ],
    featuredDestination: 3,
  };
  const state = await fixture(page, value);
  const widget = widgets(page).first();
  await openLibraryOptions(page, widget);
  const target = page.locator('[data-library-destination="1"]');
  await dragShortcut(
    page,
    page.getByRole("button", { name: "Reorder Access History", exact: true }),
    target,
  );
  await page.mouse.up();

  await expect
    .poll(() => shortcutOrder(widget))
    .toEqual(["Files", "Access History", "Tags and Categories", "Your Storage"]);
  await expect(
    page.getByRole("button", { name: "Reorder Access History", exact: true }),
  ).toBeFocused();
  expect(state.writes).toHaveLength(0);
  await page.screenshot({
    path: "/tmp/alexandria-library-final/reorder-inspector.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(
    state.saved.desktop.widgets[0]!.options.shortcuts?.map((shortcut) => shortcut.destination),
  ).toEqual([0, 2, 1, 3]);
  expect(state.saved.desktop.widgets[0]!.options.featuredDestination).toBe(3);
  expect(state.saved.desktop.widgets[0]!.options.shortcuts?.[0]?.label).toBe("Files");
  await page.reload();
  await expect
    .poll(() => shortcutOrder(widget))
    .toEqual(["Files", "Access History", "Tags and Categories", "Your Storage"]);
});

test("Escape cancels shortcut dragging and keyboard handles keep ordering bounded", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  const state = await fixture(page, value);
  const widget = widgets(page).first();
  await openLibraryOptions(page, widget);
  const handle = page.getByRole("button", { name: "Reorder Access History", exact: true });
  await dragShortcut(page, handle, page.locator('[data-library-destination="1"]'));
  await page.keyboard.press("Escape");
  await page.mouse.up();
  await expect(page.getByRole("list", { name: "Selected destinations" })).toBeVisible();
  await expect
    .poll(() => shortcutOrder(widget))
    .toEqual(["File Explorer", "Tags and Categories", "Access History", "Your Storage"]);
  await checkKeyboardBounds(page, handle, widget);
  expect(state.writes).toHaveLength(0);
  await closeWidgetSettings(page);
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.writes).toHaveLength(0);
  expect(state.saved.desktop.widgets[0]!.options.shortcutGroup).toBe(0);
});

const touchReorderWithScroll = async (page: Page) => {
  const source = page.getByRole("button", { name: "Reorder File Explorer", exact: true });
  await source.scrollIntoViewIfNeeded();
  const from = (await source.boundingBox())!;
  const scroll = page
    .getByRole("region", { name: "Widget editing controls", exact: true })
    .locator(".overflow-y-auto");
  const bounds = (await scroll.boundingBox())!;
  const session = await page.context().newCDPSession(page);
  const x = from.x + from.width / 2;

  await session.send("Input.dispatchTouchEvent", {
    type: "touchStart",
    touchPoints: [{ x, y: from.y + from.height / 2 }],
  });
  await session.send("Input.dispatchTouchEvent", {
    type: "touchMove",
    touchPoints: [{ x, y: bounds.y + bounds.height - 12 }],
  });
  const target = page.locator('[data-library-destination="7"]');
  await expect
    .poll(() =>
      target.evaluate((row) => {
        const rectangle = row.getBoundingClientRect();
        const container = row.closest(".overflow-y-auto")!.getBoundingClientRect();

        return rectangle.top >= container.top && rectangle.bottom <= container.bottom;
      }),
    )
    .toBe(true);
  const to = (await target.boundingBox())!;
  await session.send("Input.dispatchTouchEvent", {
    type: "touchMove",
    touchPoints: [{ x: to.x + 24, y: to.y + to.height - 8 }],
  });
  await expect(target).toHaveAttribute("data-drop-edge", "true");
  await page.screenshot({
    path: "/tmp/alexandria-library-final/reorder-touch.png",
    fullPage: true,
  });
  await session.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  await session.detach();
};

test.describe("Touch shortcut ordering", () => {
  test.use({ hasTouch: true });

  test("drag handles scroll the inspector and place a shortcut at the end on a phone", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const value = document();
    value.desktop.widgets[0]!.options.shortcutGroup = 3;
    value.desktop.widgets[0]!.options.shortcuts = Array.from({ length: 8 }, (_, destination) => ({
      destination,
      label: null,
    }));
    const state = await fixture(page, value);
    await openLibraryOptions(page, widgets(page).first());
    await page.getByRole("button", { name: "Expand inspector", exact: true }).click();
    await touchReorderWithScroll(page);
    const list = page.getByRole("list", { name: "Selected destinations" });

    await expect
      .poll(() =>
        list
          .getByRole("listitem")
          .evaluateAll((rows) =>
            rows.map((row) => Number((row as HTMLElement).dataset.libraryDestination)),
          ),
      )
      .toEqual([1, 2, 3, 4, 5, 6, 7, 0]);
    expect(state.writes).toHaveLength(0);
    await closeWidgetSettings(page);
    await page.getByRole("button", { name: "Done", exact: true }).click();
    await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
    expect(
      state.saved.desktop.widgets[0]!.options.shortcuts?.map((shortcut) => shortcut.destination),
    ).toEqual([1, 2, 3, 4, 5, 6, 7, 0]);
  });
});
