import { expect, test } from "@playwright/test";

import { fixture } from "./helpers/home-dashboard.js";
import { openWidgetTab, selectWidget } from "./helpers/home-grid-editor.js";

test("Done preserves another device's additions, geometry and unedited preferences", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const state = await fixture(page);
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, clock);
  await openWidgetTab(page, "Options");
  await page.getByRole("switch", { name: "Show seconds", exact: true }).click();

  const remoteClock = state.saved.desktop.widgets[0]!;
  remoteClock.x = 6;
  remoteClock.options.hourFormat = 2;
  state.saved.showWidgetTitles = true;
  state.saved.desktop.widgets.push({
    ...structuredClone(remoteClock),
    instanceId: "pc-added",
    y: 12,
  });

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.batches).toEqual([
    {
      creates: [],
      updates: [{ id: "default-clock", changes: { options: { showSeconds: true } } }],
      deletes: [],
      initializeMobileLayout: false,
    },
  ]);
  expect(state.saved.desktop.widgets[0]).toMatchObject({
    x: 6,
    options: { showSeconds: true, hourFormat: 2 },
  });
  expect(state.saved.showWidgetTitles).toBe(true);
  await expect(page.locator('[data-widget-id="pc-added"]')).toBeVisible();
  await expect(clock.locator("header")).toContainText("Clock");

  await page.reload();
  await expect(page.locator("[data-widget-id]")).toHaveCount(3);
});
