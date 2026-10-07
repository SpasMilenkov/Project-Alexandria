import { expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";

test("searches the server catalogue and saves a quarter-hour zone through reload", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.clock.setFixedTime(new Date("2026-01-15T12:00:00Z"));

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Widget options", exact: true }).click();

  const control = page.getByRole("button", { name: "Time zone", exact: true });

  await control.click();
  await page.getByPlaceholder("Search time zones...").fill("Kathmandu");

  await expect(page.getByRole("option")).toHaveCount(1);

  await page.getByRole("option", { name: "Asia / Kathmandu", exact: true }).click();
  await page.getByRole("button", { name: "Close options", exact: true }).click();

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
  await page.getByRole("button", { name: "Widget options", exact: true }).click();

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
  await page.getByRole("button", { name: "Close options", exact: true }).click();

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
  await page.getByRole("button", { name: "Widget options", exact: true }).click();

  await expect(page.getByLabel("Loading time zones")).toBeVisible();
  expect(attempts).toBe(1);

  release();

  await expect(page.getByRole("button", { name: "Retry time zones", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Retry time zones", exact: true }).click();

  await expect(page.getByRole("button", { name: "Time zone", exact: true })).toContainText(
    "US / Eastern (saved)",
  );

  await page.getByRole("button", { name: "Close options", exact: true }).click();

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
  await page.getByRole("button", { name: "Widget options", exact: true }).click();

  await expect(page.getByRole("button", { name: "Time zone", exact: true })).toBeVisible();
  await expect(page.getByLabel("Loading time zones")).toHaveCount(0);

  await page.getByRole("button", { name: "Time zone", exact: true }).click();

  await expect(page.getByRole("option", { name: "Asia / Kathmandu", exact: true })).toBeVisible();
  expect(requests).toBe(1);
  expect(state.writes).toHaveLength(0);
});
