import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  closeControls,
  closeWidgetSettings,
  dashboardSettings,
  openControls,
  openWidgetTab,
  resizeWidget,
} from "./helpers/home-grid-editor.js";

const fitsCard = async (clock: Locator) => {
  const fits = await clock.evaluate((element) => ({
    width: element.scrollWidth <= element.clientWidth + 1,
    height: element.scrollHeight <= element.clientHeight + 1,
  }));

  expect(fits, (await clock.getAttribute("data-widget-id")) ?? "Clock").toEqual({
    width: true,
    height: true,
  });
};

const portraitClockSeed = () => {
  const initial = seed();
  initial.desktop.widgets[0]!.height = 6;
  initial.desktop.widgets[1]!.y = 6;

  return initial;
};

const chooseHourFormat = async (page: Page, label: string) => {
  await page
    .getByRole("group", { name: "Hour format", exact: true })
    .getByRole("button", { name: label, exact: true })
    .click();
};

const configureAnalogClock = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Style");

  await expect(page.getByRole("dialog", { name: "Widget options", exact: true })).toHaveCount(0);
  await expect(page.locator('[data-widget-id][aria-label="Clock"]').first()).toBeInViewport();

  await expect(page.getByRole("button", { name: "Quiet", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await page.getByRole("button", { name: "Watch", exact: true }).focus();
  await page.keyboard.press("Enter");

  await expect(page.getByRole("button", { name: "Watch", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await scrollWidgetOptions(page);

  await expect(page.getByRole("button", { name: "Device default", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await openWidgetTab(page, "Options");
  await chooseHourFormat(page, "12-hour");

  await page.getByRole("button", { name: "Position clock at bottom right", exact: true }).focus();
  await page.keyboard.press("Enter");
  await page.getByRole("button", { name: "Position text at bottom center", exact: true }).click();
  await page.getByRole("button", { name: "Time zone", exact: true }).click();
  await page.getByPlaceholder("Search time zones...").fill("Kathmandu");
  await page.getByRole("option", { name: "Asia / Kathmandu", exact: true }).click();

  await page.screenshot({ animations: "disabled", path: "/tmp/alexandria-clock-options.png" });
  await closeWidgetSettings(page);
};

test("edits clock appearance, aligns content and preserves its style while resizing and saving", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.clock.setFixedTime(new Date("2026-01-15T12:00:00Z"));

  const initial = seed();
  initial.desktop.widgets[0]!.options = { timeZone: "local", shortcutGroup: 0 };
  const state = await fixture(page, initial);
  const clock = page.locator('[data-widget-id][aria-label="Clock"]').first();

  await configureAnalogClock(page);

  await expect(clock.locator(".clock-hour-hand")).toHaveAttribute(
    "transform",
    "rotate(172.5 100 100)",
  );
  await expect(clock.locator(".clock-minute-hand")).toHaveAttribute(
    "transform",
    "rotate(270 100 100)",
  );

  const bounds = (await clock.locator(".clock-alignment").boundingBox())!;
  const contentBounds = (await clock.locator(".clock-content").boundingBox())!;

  expect(Math.abs(bounds.x + bounds.width - contentBounds.x - contentBounds.width)).toBeLessThan(2);
  expect(Math.abs(bounds.y + bounds.height - contentBounds.y - contentBounds.height)).toBeLessThan(
    2,
  );
  expect(state.writes).toHaveLength(0);

  // Vue rejects pointer events stamped at the same frozen time as its listeners.
  await page.clock.setFixedTime(new Date("2026-01-15T12:00:00.001Z"));
  await resizeWidget(page, clock, "bottom-right", 3, -2);

  await expect(clock.locator(".clock-dial")).toBeVisible();
  await fitsCard(clock);

  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.desktop.widgets[0]).toMatchObject({
    width: 9,
    height: 2,
    options: {
      timeZone: "Asia/Kathmandu",
      clockStyle: 1,
      hourFormat: 1,
      horizontalAlignment: 2,
      verticalAlignment: 2,
      textAlignment: 1,
    },
  });

  await page.reload();

  await expect(clock.locator(".clock-dial")).toBeVisible();
  await expect(clock.locator(".clock-minute-hand")).toHaveAttribute(
    "transform",
    "rotate(270 100 100)",
  );
});

const discardAppearanceChange = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Watch", exact: true }).click();
  await openWidgetTab(page, "Options");
  await chooseHourFormat(page, "24-hour");
  await closeWidgetSettings(page);
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();
};

const composeSeparateStyles = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Watch", exact: true }).click();
  await openWidgetTab(page, "Options");
  await chooseHourFormat(page, "12-hour");
  await closeWidgetSettings(page);
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await openWidgetTab(page, "Style");

  await expect(page.getByRole("button", { name: "Watch", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await page.getByRole("button", { name: "Tile", exact: true }).click();
  await openWidgetTab(page, "Options");
  await chooseHourFormat(page, "24-hour");
  await page.getByRole("button", { name: "Position clock at top center", exact: true }).click();
  await page.getByRole("button", { name: "Position text at bottom center", exact: true }).click();
  await closeWidgetSettings(page);
};

const verifyPhoneOptions = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openControls(page);
  await openWidgetTab(page, "Style");

  await expect(page.getByRole("button", { name: "Tile", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await openWidgetTab(page, "Options");

  await expect(
    page.getByRole("button", { name: "Position clock at top center", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByRole("button", { name: "24-hour", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await page.screenshot({
    animations: "disabled",
    path: "/tmp/alexandria-clock-phone-options.png",
  });

  await closeWidgetSettings(page);
  await closeControls(page);
};

test("discards appearance changes and keeps desktop and mobile styles independent through Done", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const state = await fixture(page);
  const clock = page.locator('[data-widget-id][aria-label="Clock"]').first();

  await discardAppearanceChange(page);

  await expect(clock.locator(".clock-dial")).toHaveCount(0);
  expect(state.writes).toHaveLength(0);

  await composeSeparateStyles(page);

  await expect(clock.locator(".clock-material-time")).toBeVisible();
  expect(state.writes).toHaveLength(0);

  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.desktop.widgets[0]!.options.clockStyle).toBe(1);
  expect(state.saved.desktop.widgets[0]!.options.hourFormat).toBe(1);
  expect(state.saved.mobile!.widgets[0]!.options).toMatchObject({
    clockStyle: 2,
    hourFormat: 2,
    horizontalAlignment: 1,
    verticalAlignment: 1,
    textAlignment: 1,
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();

  await expect(clock.locator(".clock-material-time")).toBeVisible();
  await fitsCard(clock);
  await verifyPhoneOptions(page);
});

for (const mode of ["light", "dark"] as const) {
  test(`renders all styles in tall, wide and narrow cards in ${mode} mode`, async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1100 });
    await page.emulateMedia({ colorScheme: mode });
    await page.clock.setFixedTime(new Date("2026-01-15T10:08:00Z"));

    const initial = seed();
    const original = initial.desktop.widgets[0]!;
    initial.desktop.widgets = Array.from({ length: 6 }, (_, index) => ({
      ...original,
      instanceId: `clock-${index}`,
      x: (index % 3) * 4,
      y: index < 3 ? 0 : 6,
      width: 4,
      height: index < 3 ? 6 : 2,
      options: {
        ...original.options,
        timeZone: "UTC",
        clockStyle: index % 3,
        horizontalAlignment: 1,
        verticalAlignment: 0,
        textAlignment: 1,
      },
    }));

    await fixture(page, initial, { backgroundColor: null });

    for (const clock of await page.locator("[data-widget-id]").all()) await fitsCard(clock);

    await expect(page.locator(".clock-dial")).toHaveCount(2);
    await expect(page.locator(".clock-material-time")).toHaveCount(2);
    await page.screenshot({
      animations: "disabled",
      path: `/tmp/alexandria-clock-gallery-${mode}.png`,
      fullPage: true,
    });

    await page.setViewportSize({ width: 390, height: 844 });

    for (const clock of await page.locator("[data-widget-id]").all()) await fitsCard(clock);

    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({
      path: `/tmp/alexandria-clock-gallery-phone-${mode}.png`,
      fullPage: true,
    });
  });
}

const scrollWidgetOptions = async (page: Page) => {
  await openWidgetTab(page, "Options");
};

const expectFaceAnchor = async (clock: Locator, row: number, column: number) => {
  const area = (await clock.locator(".clock-face-slot").boundingBox())!;
  const content = (await clock.locator(".clock-quiet-time").boundingBox())!;

  expect(Math.abs(content.x - area.x - (column * (area.width - content.width)) / 2)).toBeLessThan(
    2,
  );
  const alignment = await clock.locator(".clock-content").evaluate((element) => ({
    horizontal: getComputedStyle(element).alignItems,
    vertical: getComputedStyle(element).justifyContent,
  }));

  expect(alignment.horizontal).toBe(["flex-start", "center", "flex-end"][column]);
  expect(alignment.vertical).toBe(["start", "center", "end"][row]);
  expect(content.y).toBeGreaterThanOrEqual(area.y - 1);
  expect(content.y + content.height).toBeLessThanOrEqual(area.y + area.height + 1);
};

test("the nine anchor points move clock content while the board stays visible", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const state = await fixture(page);
  const clock = page.locator('[data-widget-id][aria-label="Clock"]').first();

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const original = (await clock.boundingBox())!;

  await openWidgetTab(page, "Style");

  await scrollWidgetOptions(page);

  const picker = page.getByRole("group", { name: "Clock position", exact: true });

  await expect(picker.getByRole("button")).toHaveCount(9);

  for (const [row, vertical] of ["top", "middle", "bottom"].entries()) {
    for (const [column, horizontal] of ["left", "center", "right"].entries()) {
      const anchor = picker.getByRole("button", {
        name: `Position clock at ${vertical} ${horizontal}`,
        exact: true,
      });

      await anchor.click();
      await expect(anchor).toHaveAttribute("aria-pressed", "true");

      await expectFaceAnchor(clock, row, column);
    }
  }

  await expect(picker.locator('[aria-pressed="true"]')).toHaveCount(1);
  await expect(clock).toBeInViewport({ ratio: 1 });
  expect(await clock.boundingBox()).toEqual(original);
  expect(state.writes).toHaveLength(0);
});

const captionPositionSeed = () => {
  const initial = portraitClockSeed();
  initial.desktop.widgets[0]!.options = {
    ...initial.desktop.widgets[0]!.options,
    clockStyle: 1,
    horizontalAlignment: 2,
    verticalAlignment: 0,
  };
  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: [
      {
        ...initial.desktop.widgets[0]!,
        instanceId: "mobile-clock",
        width: 4,
        options: { ...initial.desktop.widgets[0]!.options },
      },
    ],
  };

  return initial;
};

const expectCaptionAtTopLeft = async (clock: Locator) => {
  const area = (await clock.locator(".clock-alignment").boundingBox())!;
  const caption = (await clock.locator(".clock-details").boundingBox())!;
  const face = (await clock.locator(".clock-dial").boundingBox())!;

  expect(Math.abs(caption.x - area.x)).toBeLessThan(2);
  expect(Math.abs(caption.y - area.y)).toBeLessThan(2);
  expect(face.y).toBeGreaterThan(caption.y + caption.height);
  expect(Math.abs(face.x + face.width - area.x - area.width)).toBeLessThan(2);
};

const discardCaptionEdit = async (page: Page) => {
  const picker = page.getByRole("group", { name: "Text position", exact: true });

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");
  await picker.getByRole("button", { name: "Position text at bottom right", exact: true }).click();
  await closeWidgetSettings(page);
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();
};

test("caption position previews independently and survives Done while mobile and discarded edits stay separate", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = captionPositionSeed();
  const state = await fixture(page, initial);
  const clock = page.locator('[data-widget-id][aria-label="Clock"]').first();

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Style");
  await scrollWidgetOptions(page);

  const picker = page.getByRole("group", { name: "Text position", exact: true });

  await expect(picker.getByRole("button")).toHaveCount(9);
  await picker.getByRole("button", { name: "Position text at top left", exact: true }).click();
  await expect(
    picker.getByRole("button", { name: "Position text at top left", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");

  await expectCaptionAtTopLeft(clock);
  expect(state.writes).toHaveLength(0);
  await page.screenshot({
    animations: "disabled",
    path: "/tmp/alexandria-clock-text-top-left.png",
  });

  await closeWidgetSettings(page);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.desktop.widgets[0]!.options).toMatchObject({
    horizontalAlignment: 2,
    verticalAlignment: 0,
    textAlignment: 0,
    textVerticalAlignment: 1,
  });
  expect(state.saved.mobile!.widgets[0]!.options).toMatchObject({
    textAlignment: 0,
    textVerticalAlignment: 2,
  });

  await page.reload();
  await expect(clock.locator(".clock-dial")).toBeVisible();
  expect(
    Math.abs(
      (await clock.locator(".clock-details").boundingBox())!.y -
        (await clock.locator(".clock-alignment").boundingBox())!.y,
    ),
  ).toBeLessThan(2);

  await discardCaptionEdit(page);

  expect(state.writes).toHaveLength(1);
  expect(
    Math.abs(
      (await clock.locator(".clock-details").boundingBox())!.y -
        (await clock.locator(".clock-alignment").boundingBox())!.y,
    ),
  ).toBeLessThan(2);
});
