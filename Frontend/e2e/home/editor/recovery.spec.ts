import { type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";

const prepareBrokenHome = async (page: Page) => {
  const control = {
    broken: false,
    failReset: false,
    holdReset: false,
    resetRequests: 0,
    finishReset: () => {},
  };
  const saved = seed();
  saved.useSeparateMobileLayout = true;
  saved.mobile = { columns: 4, widgets: [] };

  const state = await fixture(page, saved, {}, undefined, async (route) => {
    if (new URL(route.request().url()).pathname !== "/api/home/widgets") return false;

    if (route.request().method() === "GET" && control.broken) {
      await route.fulfill({ status: 422, json: { message: "Internal widget validation details" } });

      return true;
    }

    if (route.request().method() !== "DELETE") return false;

    control.resetRequests += 1;

    if (control.failReset) {
      control.failReset = false;
      await route.fulfill({ status: 503, json: { message: "Database unavailable" } });

      return true;
    }

    if (control.holdReset) {
      await new Promise<void>((resolve) => {
        control.finishReset = resolve;
      });
    }

    control.broken = false;
    state.saved = { ...seed(), desktop: { columns: 12, widgets: [] }, mobile: null };
    await route.fulfill({
      json: {
        initialized: true,
        hasMobileLayout: false,
        preferences: { useSeparateMobileLayout: false, showWidgetTitles: false },
        widgets: [],
      },
    });

    return true;
  });

  control.broken = true;
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Your dashboard couldn’t be loaded" }),
  ).toBeVisible();

  return control;
};

test("Retry recovers the saved board and canceling reset never deletes it", async ({ page }) => {
  const control = await prepareBrokenHome(page);
  await expect(page.getByText("Internal widget validation details")).toHaveCount(0);
  await page.getByRole("button", { name: "Reset dashboard", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Reset your dashboard?" });
  await expect(dialog.getByText(/both your desktop and mobile layouts/u)).toBeVisible();
  await expect(dialog.getByText(/This cannot be undone/u)).toBeVisible();
  expect(control.resetRequests).toBe(0);
  await dialog.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(dialog).toBeHidden();
  expect(control.resetRequests).toBe(0);

  control.broken = false;
  await page.getByRole("button", { name: "Try again", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Your dashboard couldn’t be loaded" }),
  ).toBeHidden();
  expect(control.resetRequests).toBe(0);
});

test("a failed reset can be retried and leaves an empty, editable board after reload", async ({
  page,
}) => {
  const control = await prepareBrokenHome(page);
  control.failReset = true;
  await page.getByRole("button", { name: "Reset dashboard", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Reset your dashboard?" });
  await dialog.getByRole("button", { name: "Reset dashboard", exact: true }).click();

  await expect(dialog.getByText("Dashboard could not be reset")).toBeVisible();
  expect(control.resetRequests).toBe(1);
  await expect(dialog.getByRole("button", { name: "Cancel", exact: true })).toBeEnabled();

  await dialog.getByRole("button", { name: "Reset dashboard", exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("heading", { name: "Your dashboard is empty" })).toBeVisible();
  expect(control.resetRequests).toBe(2);
  await page.reload();
  await expect(page.getByRole("heading", { name: "Your dashboard is empty" })).toBeVisible();
  await page.getByRole("button", { name: "Customize Home", exact: true }).click();
  await expect(page.getByRole("button", { name: "Done", exact: true })).toBeVisible();
});

test("reset stays open and blocks repeated submission while the request is pending", async ({
  page,
}) => {
  const control = await prepareBrokenHome(page);
  control.holdReset = true;
  await page.getByRole("button", { name: "Reset dashboard", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Reset your dashboard?" });
  const confirm = dialog.getByRole("button", { name: "Reset dashboard", exact: true });
  await confirm.click();
  await expect.poll(() => control.resetRequests).toBe(1);
  await expect(confirm).toBeDisabled();
  await expect(dialog.getByRole("button", { name: "Cancel", exact: true })).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  expect(control.resetRequests).toBe(1);

  control.finishReset();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("heading", { name: "Your dashboard is empty" })).toBeVisible();
});

for (const colorScheme of ["light", "dark"] as const) {
  test(`recovery panel and confirmation fit a ${colorScheme} phone`, async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.emulateMedia({ colorScheme });
    await prepareBrokenHome(page);
    await page.screenshot({ path: `/tmp/home-recovery-${colorScheme}-phone.png`, fullPage: true });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.getByRole("button", { name: "Reset dashboard", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: "Reset your dashboard?" });
    await expect(dialog).toBeVisible();
    await expect(dialog).toHaveCSS("opacity", "1");
    await expect(dialog.getByRole("button", { name: "Cancel", exact: true })).toBeVisible();
    await expect(
      dialog.getByRole("button", { name: "Reset dashboard", exact: true }),
    ).toBeVisible();
    await page.screenshot({
      path: `/tmp/home-recovery-${colorScheme}-confirm-phone.png`,
      fullPage: true,
    });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
  });
}
