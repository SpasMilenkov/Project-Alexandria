import { type Locator, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { resizeWidget } from "./helpers/home-grid-editor.js";

test.use({ timezoneId: "UTC" });

const faces = [
  ".clock-quiet-time",
  ".clock-dial",
  ".clock-material-time",
  ".clock-flip-face",
  ".clock-rings-dial",
  ".clock-words-phrase",
];

const verifyFace = async (clock: Locator, selector: string) => {
  const body = clock.locator(".clock-alignment");
  const content = clock.locator(".clock-content");
  const face = clock.locator(selector);

  await expect(body).toHaveAttribute("data-face-only", "true");
  await expect(
    clock.locator("header, .clock-details, .clock-greeting, .clock-date-window"),
  ).toHaveCount(0);
  await expect(
    clock.locator(
      ".clock-rings-legend, .clock-words-intro, .clock-words-period, .clock-words-exact",
    ),
  ).toHaveCount(0);
  await expect(face).toBeVisible();

  await expect
    .poll(() =>
      clock
        .locator("..")
        .evaluate((element) =>
          element
            .getAnimations({ subtree: true })
            .some((animation) => animation.playState === "running"),
        ),
    )
    .toBe(false);

  const bodyBounds = (await body.boundingBox())!;
  const contentBounds = (await content.boundingBox())!;
  const faceBounds = (await face.boundingBox())!;

  expect(contentBounds.width, selector).toBeCloseTo(bodyBounds.width, 0);
  expect(contentBounds.height, selector).toBeCloseTo(bodyBounds.height, 0);
  expect(faceBounds.x).toBeGreaterThanOrEqual(bodyBounds.x - 1);
  expect(faceBounds.y).toBeGreaterThanOrEqual(bodyBounds.y - 1);
  expect(faceBounds.x + faceBounds.width).toBeLessThanOrEqual(bodyBounds.x + bodyBounds.width + 1);
  expect(faceBounds.y + faceBounds.height).toBeLessThanOrEqual(
    bodyBounds.y + bodyBounds.height + 1,
  );

  if (selector.endsWith("dial")) {
    expect(faceBounds.width / faceBounds.height).toBeCloseTo(1, 1);
    expect(faceBounds.height).toBeGreaterThanOrEqual(
      Math.min(bodyBounds.width, bodyBounds.height) - 1,
    );
  }

  if (selector === ".clock-flip-face") {
    const groupsBounds = (await clock.locator(".clock-flip-groups").boundingBox())!;

    expect(groupsBounds.width).toBeCloseTo(bodyBounds.width, 0);
  }

  expect(
    await clock.evaluate(
      (card) =>
        card.scrollWidth <= card.clientWidth + 1 && card.scrollHeight <= card.clientHeight + 1,
    ),
  ).toBe(true);
};

for (const fontFamily of ["system", "mono"]) {
  for (const hourFormat of [1, 2]) {
    test(`2×2 faces fill their body with ${fontFamily} font and hour format ${hourFormat}`, async ({
      page,
    }) => {
      await page.clock.setFixedTime(new Date("2026-10-08T23:33:30Z"));

      const initial = seed();
      const original = initial.desktop.widgets[0]!;
      initial.desktop.widgets = faces.map((_, style) => ({
        ...original,
        instanceId: `minimal-${style}`,
        x: style * 2,
        y: 0,
        width: 2,
        height: 2,
        options: { ...original.options, clockStyle: style, hourFormat, timeZone: "UTC" },
      }));
      initial.useSeparateMobileLayout = true;
      initial.mobile = {
        columns: 4,
        widgets: initial.desktop.widgets.map((widget, index) => ({
          ...widget,
          x: (index % 2) * 2,
          y: Math.floor(index / 2) * 2,
        })),
      };

      const state = await fixture(page, initial, { fontFamily, backgroundColor: null });

      for (const width of [1800, 1440, 414]) {
        await page.setViewportSize({ width, height: 1100 });

        for (const colorScheme of ["light", "dark"] as const) {
          await page.emulateMedia({ colorScheme });

          for (const [style, selector] of faces.entries()) {
            await verifyFace(page.locator(`[data-widget-id="minimal-${style}"]`), selector);
          }
        }

        if (fontFamily === "mono" && hourFormat === 1) {
          await page.screenshot({ path: `/tmp/alexandria-minimal-clocks-${width}.png` });
        }
      }

      expect(state.writes).toHaveLength(0);
    });
  }
}

const resizeClockByKeys = async (handle: Locator, horizontal: string, vertical: string) => {
  await handle.focus();
  await handle.press(horizontal);
  await handle.press(horizontal);
  await handle.press(vertical);
  await handle.press(vertical);
};

test("pointer and keyboard resizing reach 2×2, restore larger content and save compact geometry", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });

  const initial = seed();
  initial.desktop.widgets = [
    {
      ...initial.desktop.widgets[0]!,
      options: {
        ...initial.desktop.widgets[0]!.options,
        clockStyle: 1,
        horizontalAlignment: 2,
        verticalAlignment: 2,
        textAlignment: 1,
      },
    },
  ];
  const state = await fixture(page, initial);
  const clock = page.locator('[data-widget-id="default-clock"]');

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await resizeWidget(page, clock, "bottom-right", -4, -2);
  await verifyFace(clock, ".clock-dial");

  const bodyBounds = (await clock.locator(".clock-alignment").boundingBox())!;
  const dialBounds = (await clock.locator(".clock-dial").boundingBox())!;

  expect(Math.abs(bodyBounds.x + bodyBounds.width - dialBounds.x - dialBounds.width)).toBeLessThan(
    1,
  );

  const handle = page.getByRole("button", { name: "Resize Clock from bottom right", exact: true });
  await resizeClockByKeys(handle, "ArrowRight", "ArrowDown");

  await expect(clock.locator("header")).toHaveCount(0);
  await expect(clock.locator(".clock-details")).toBeVisible();
  await expect(clock.locator(".clock-alignment")).toHaveAttribute("data-face-only", "false");

  await resizeClockByKeys(handle, "ArrowLeft", "ArrowUp");
  await verifyFace(clock, ".clock-dial");
  await page.getByRole("button", { name: "Done", exact: true }).click();

  expect(state.saved.desktop.widgets[0]).toMatchObject({
    width: 2,
    height: 2,
    options: { clockStyle: 1, horizontalAlignment: 2, verticalAlignment: 2, textAlignment: 1 },
  });

  await page.reload();
  await verifyFace(clock, ".clock-dial");
});
