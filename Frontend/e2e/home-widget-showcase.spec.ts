import { type Page, expect, test } from "@playwright/test";

import { fixture } from "./helpers/home-dashboard.js";
import { expectRecapContentFits } from "./helpers/home-recap.js";

const openShowcase = async (page: Page) => {
  await page.getByRole("button", { name: "Add widget", exact: true }).click();
  await page.getByRole("button", { name: "Explore all sizes", exact: true }).click();
  await expect(page.locator("[data-home-showcase]")).toBeVisible();
};

const verifyShowcase = async (page: Page) => {
  const showcase = page.locator("[data-home-showcase]");
  const groups = showcase.locator("[data-showcase-type]");
  await expect(groups).toHaveCount(17);
  await expect(showcase.locator("[data-widget-id]")).toHaveCount(51);
  await expect(showcase.getByText("This widget could not be loaded.")).toHaveCount(0);
  await expect(showcase.locator('[aria-label="Loading widget"]')).toHaveCount(0);
  await expect(showcase.locator('[aria-label="Loading widget data"]')).toHaveCount(0);

  for (const group of await groups.all()) {
    const ids = await group
      .locator("[data-widget-id]")
      .evaluateAll((elements) => elements.map((element) => element.getAttribute("data-widget-id")));
    expect(ids.map((id) => id!.split("-").at(-1))).toEqual(["2", "1", "0"]);
  }

  await showcase.screenshot({ path: "/tmp/alexandria-widget-showcase-desktop.png" });
  for (const selector of await showcase
    .locator("[data-showcase-type] > header")
    .getByRole("combobox")
    .all()) {
    await selector.click();
    await page.getByRole("option").last().click();
    await expect(showcase.getByText("This widget could not be loaded.")).toHaveCount(0);
  }
};

const editClock = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  const clock = page.locator('[data-widget-id="default-clock"]');
  await clock.focus();
  await clock.press("ArrowRight");
  const draftStyle = await clock.getAttribute("style");

  return { clock, draftStyle };
};

test.use({ serviceWorkers: "block" });

test("showcase renders every preset and keeps the draft, history and APIs isolated", async ({
  page,
}) => {
  test.setTimeout(90_000);
  await page.setViewportSize({ width: 1440, height: 1000 });
  const state = await fixture(page);
  const { clock, draftStyle } = await editClock(page);
  const stored = await page.evaluate(() => ({ ...localStorage }));
  const requests: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.startsWith("/api/")) requests.push(request.url());
  });

  await openShowcase(page);
  await verifyShowcase(page);

  await page.getByRole("button", { name: "Back to editing", exact: true }).focus();
  await page.keyboard.press("Control+z");
  await page.getByRole("button", { name: "Back to editing", exact: true }).click();
  await expect(clock).toHaveAttribute("style", draftStyle!);
  await expect(page.getByRole("button", { name: "Undo", exact: true })).toBeEnabled();
  expect(await page.evaluate(() => ({ ...localStorage }))).toEqual(stored);
  expect(requests).toEqual([]);
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets).toHaveLength(2);
  expect(state.saved.desktop.widgets[0]!.x).toBe(1);
  expect(state.batches[0]!.creates).toHaveLength(0);
});

test("catalogue fits a phone and Escape returns to the editor", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const state = await fixture(page);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openShowcase(page);
  const showcase = page.locator("[data-home-showcase]");
  await showcase.getByRole("textbox", { name: "Find a showcase widget" }).fill("storage");
  await expect(showcase.locator("[data-showcase-type]")).toHaveCount(1);
  const bounds = await showcase.evaluate((element) => ({
    width: element.clientWidth,
    content: element.scrollWidth,
  }));
  expect(bounds.content).toBeLessThanOrEqual(bounds.width);
  await expect(showcase.locator('[aria-label="Loading widget data"]')).toHaveCount(0);
  await showcase.screenshot({ path: "/tmp/alexandria-widget-showcase-mobile.png" });
  await showcase.getByRole("textbox").press("Escape");
  await expect(showcase).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Add widget", exact: true })).toBeFocused();
  expect(state.writes).toHaveLength(0);
});

test("recap catalogue content fits every preset at desktop and phone widths", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-09-08T14:00:00Z"));
  await page.setViewportSize({ width: 1800, height: 1000 });
  await fixture(page);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openShowcase(page);
  const showcase = page.locator("[data-home-showcase]");
  await showcase.getByRole("textbox", { name: "Find a showcase widget" }).fill("Latest recap");
  const group = showcase.locator("[data-showcase-type]");
  await expect(group).toHaveCount(1);

  for (const style of ["Poster", "Numbers"]) {
    await group.getByRole("combobox").click();
    await page.getByRole("option", { name: style, exact: true }).click();

    for (const width of [2100, 1800, 1440, 1024, 768, 390, 320]) {
      await page.setViewportSize({ width, height: 1000 });
      const cards = group.locator(".recap-poster, .recap-face");
      await expect(cards).toHaveCount(3);
      await expectRecapContentFits(cards);

      if (width === 2100 || width === 1800 || width === 390) {
        await cards
          .first()
          .screenshot({ path: `/tmp/alexandria-recap-${style}-${width}-large.png` });
        await cards
          .last()
          .screenshot({ path: `/tmp/alexandria-recap-${style}-${width}-small.png` });
      }
    }
  }
});
