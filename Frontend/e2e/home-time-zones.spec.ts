import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { closeWidgetSettings, openWidgetTab } from "./helpers/home-grid-editor.js";

test("searches the server catalogue and saves a quarter-hour zone through reload", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.clock.setFixedTime(new Date("2026-01-15T12:00:00Z"));

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");

  // Vue rejects pointer events stamped at the same frozen time as its listeners.
  await page.clock.setFixedTime(new Date("2026-01-15T12:00:00.001Z"));

  const control = page.getByRole("button", { name: "Time zone", exact: true });

  await control.click();
  await page.getByPlaceholder("Search time zones...").fill("Kathmandu");

  await expect(page.getByRole("option")).toHaveCount(1);

  await page.getByRole("option", { name: "Asia / Kathmandu", exact: true }).click();
  await closeWidgetSettings(page);

  const clock = page.locator('[data-widget-id="default-clock"]');

  await expect(clock.locator(".clock-time")).toHaveText(/05:45|17:45/u);
  await expect(clock.locator(".clock-zone")).toHaveText("Asia / Kathmandu");
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.timeZone).toBe("Asia/Kathmandu");

  await page.reload();

  await expect(clock.locator(".clock-zone")).toHaveText("Asia / Kathmandu");

  await page.screenshot({ path: "/tmp/alexandria-zone-kathmandu.png", fullPage: true });
});

test("browser-unsupported zones preserve the saved preference and offer an explicit replacement", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.addInitScript(() => {
    Intl.DateTimeFormat = new Proxy(Intl.DateTimeFormat, {
      construct: (target, args, newTarget) => {
        if (args[1]?.timeZone === "Pacific/Chatham") throw new RangeError("Unsupported zone");

        return Reflect.construct(target, args, newTarget);
      },
    });
  });

  const initial = seed();
  initial.desktop.widgets[0]!.options.timeZone = "Pacific/Chatham";
  const state = await fixture(page, initial);
  const clock = page.locator('[data-widget-id="default-clock"]');

  await expect(clock.getByRole("status")).toContainText("Showing device time.");
  await expect(page.locator('[data-widget-id="default-library"]')).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.timeZone).toBe("Pacific/Chatham");

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");

  await expect(
    page.getByText("This device cannot display your saved time zone.", { exact: false }),
  ).toBeVisible();

  await page.getByRole("button", { name: "Time zone", exact: true }).click();

  const unavailableChoice = page.getByRole("option", {
    name: "Pacific / Chatham (saved)",
    exact: true,
  });

  await expect(unavailableChoice).toHaveAttribute("data-disabled", "");

  await unavailableChoice.click({ force: true });

  await expect(unavailableChoice).toBeVisible();
  await expect(page.getByRole("option", { name: "Pacific / Chatham", exact: true })).toHaveCount(0);

  await page.getByRole("option", { name: "UTC", exact: true }).click();
  await closeWidgetSettings(page);

  await expect(clock.getByRole("status")).toHaveCount(0);
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.timeZone).toBe("UTC");
});

test("catalogue loading and retry never overwrite the saved zone", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = seed();
  initial.desktop.widgets[0]!.options.timeZone = "US/Eastern";

  let release = () => {};
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  let attempts = 0;

  const state = await fixture(page, initial, {}, async (route) => {
    attempts++;

    if (attempts === 1) {
      await pending;
      await route.fulfill({ status: 503, json: { message: "Temporarily unavailable" } });

      return;
    }

    await route.fulfill({ json: ["local", "UTC", "America/New_York", "Asia/Kathmandu"] });
  });

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");

  await expect(page.getByLabel("Loading time zones")).toBeVisible();
  expect(attempts).toBe(1);

  release();

  await expect(page.getByRole("button", { name: "Retry time zones", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Retry time zones", exact: true }).click();

  await expect(page.getByRole("button", { name: "Time zone", exact: true })).toContainText(
    "US / Eastern (saved)",
  );

  await closeWidgetSettings(page);

  await expect(page.locator('[data-widget-id="default-clock"] .clock-zone')).toHaveText(
    "US / Eastern",
  );
  expect(state.saved.desktop.widgets[0]!.options.timeZone).toBe("US/Eastern");
  expect(state.writes).toHaveLength(0);
  expect(attempts).toBe(2);
});

test("prefetches without blocking Home and reuses the catalogue when options open", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  let release = () => {};
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  let requests = 0;

  const state = await fixture(page, seed(), {}, async (route) => {
    requests++;
    await pending;
    await route.fulfill({ json: ["local", "UTC", "Asia/Kathmandu"] });
  });

  await expect.poll(() => requests).toBe(1);
  await expect(page.locator('[data-widget-id="default-clock"]')).toBeVisible();

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const response = page.waitForResponse("**/api/settings/home/time-zones");

  release();
  await response;
  await openWidgetTab(page, "Options");

  await expect(page.getByRole("button", { name: "Time zone", exact: true })).toBeVisible();
  await expect(page.getByLabel("Loading time zones")).toHaveCount(0);

  await page.getByRole("button", { name: "Time zone", exact: true }).click();

  await expect(page.getByRole("option", { name: "Asia / Kathmandu", exact: true })).toBeVisible();
  expect(requests).toBe(1);
  expect(state.writes).toHaveLength(0);
});

const changeClockStyles = async (page: Page) => {
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Watch", exact: true }).click();
  await page.evaluate(() => Reflect.set(window, "timezoneFormatterCalls", 0));
  await page.getByRole("button", { name: "Tile", exact: true }).click();
  await page.getByRole("button", { name: "Watch", exact: true }).click();

  const validationCalls = await page.evaluate(() =>
    Number(Reflect.get(window, "timezoneFormatterCalls")),
  );

  expect(validationCalls).toBeLessThan(20);

  await openWidgetTab(page, "Options");
};

test("keeps the full catalogue searchable without rendering or revalidating every zone on edits", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.addInitScript(() => {
    Reflect.set(window, "timezoneFormatterCalls", 0);
    Intl.DateTimeFormat = new Proxy(Intl.DateTimeFormat, {
      construct: (target, args, newTarget) => {
        const calls = Number(Reflect.get(window, "timezoneFormatterCalls"));

        Reflect.set(window, "timezoneFormatterCalls", calls + 1);

        return Reflect.construct(target, args, newTarget);
      },
    });
  });

  const catalogue = await page.evaluate(() => [
    "local",
    "UTC",
    "Asia/Kathmandu",
    ...Intl.supportedValuesOf("timeZone"),
  ]);

  expect(catalogue.length).toBeGreaterThan(350);

  const state = await fixture(page, seed(), {}, async (route) => {
    await route.fulfill({ json: catalogue });
  });

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await changeClockStyles(page);

  await page.getByRole("button", { name: "Time zone", exact: true }).click();
  await expect(page.getByRole("option").first()).toBeVisible();
  expect(await page.getByRole("option").count()).toBeLessThan(40);

  await page.getByPlaceholder("Search time zones...").fill("Kathmandu");
  await expect(page.getByRole("option")).toHaveCount(1);
  await page.getByRole("option", { name: "Asia / Kathmandu", exact: true }).click();
  await closeWidgetSettings(page);
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.timeZone).toBe("Asia/Kathmandu");
});
