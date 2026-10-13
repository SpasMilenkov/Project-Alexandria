import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { dashboardSettings, openWidgetTab, selectWidget } from "./helpers/home-grid-editor.js";

const titleChoice = async (page: Page, choice: "Show" | "Hide" | "Default") => {
  await openWidgetTab(page, "Options");
  await page.getByRole("combobox", { name: "Widget title", exact: true }).click();
  await page.getByRole("option", { name: choice, exact: true }).click();

  await expect(page.getByRole("listbox")).not.toBeVisible();
  await expect(page.getByRole("combobox", { name: "Widget title", exact: true })).toHaveText(
    choice,
  );
};

const setDesktopOverrides = async (page: Page) => {
  const clock = page.locator('[data-widget-id="titles-0"]');
  const player = page.locator('[data-widget-id="titles-6"]');

  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Show widget titles", exact: true }).click();
  await expect(page.locator("[data-widget-id] header")).toHaveCount(3);
  await selectWidget(page, clock);
  await titleChoice(page, "Hide");
  await expect(clock.locator("header")).toHaveCount(0);
  await selectWidget(page, player);
  await titleChoice(page, "Show");
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Show widget titles", exact: true }).click();
  await expect(player.locator("header")).toHaveCount(1);
  await expect(page.locator('[data-widget-id="titles-1"] header')).toHaveCount(0);
};

test("global titles and per-widget overrides preview, save and stay independent on mobile", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const initial = seed();
  const source = initial.desktop.widgets[0]!;
  initial.desktop.widgets = [0, 1, 6].map((type, index) => ({
    ...source,
    type,
    instanceId: `titles-${type}`,
    x: index * 4,
    width: 4,
  }));
  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: initial.desktop.widgets.map((widget, index) => ({
      ...widget,
      instanceId: `mobile-${widget.instanceId}`,
      x: 0,
      y: index * 4,
    })),
  };
  const state = await fixture(page, initial);
  const player = page.locator('[data-widget-id][aria-label="Player"]');

  await expect(page.locator("[data-widget-id] header")).toHaveCount(0);
  await expect(player).toHaveAttribute("aria-label", "Player");
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await setDesktopOverrides(page);
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await expect(player.locator("header")).toHaveCount(0);
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.showWidgetTitles).toBe(false);
  expect(state.saved.desktop.widgets.map((widget) => widget.showTitle)).toEqual([
    false,
    null,
    true,
  ]);
  expect(state.saved.mobile!.widgets.every((widget) => widget.showTitle === null)).toBe(true);
  await page.reload();
  await expect(player.locator("header")).toHaveCount(1);
});

test("inherit restores the global default and discarding restores saved title choices", async ({
  page,
}) => {
  const initial = seed();
  initial.showWidgetTitles = true;
  initial.desktop.widgets[0]!.showTitle = false;
  await fixture(page, initial);
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, clock);
  await titleChoice(page, "Default");
  await expect(clock.locator("header")).toHaveCount(1);
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  await expect(clock.locator("header")).toHaveCount(0);
});
