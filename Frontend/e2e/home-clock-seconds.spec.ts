import { expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { closeWidgetSettings, openWidgetTab } from "./helpers/home-grid-editor.js";

test.use({ timezoneId: "America/New_York" });

for (const fontFamily of ["system", "mono"]) {
  test(`digital seconds fit compact, tall and wide clocks with the ${fontFamily} font`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1440, height: 1100 });
    await page.clock.setFixedTime(new Date("2026-10-08T23:33:30Z"));

    const initial = seed();
    const original = initial.desktop.widgets[0]!;
    const shapes = [
      { name: "tiny", x: 0, y: 0, width: 2, height: 2 },
      { name: "narrow", x: 2, y: 0, width: 2, height: 3 },
      { name: "banner", x: 4, y: 0, width: 4, height: 2 },
      { name: "tall", x: 8, y: 0, width: 2, height: 6 },
      { name: "large", x: 0, y: 6, width: 6, height: 6 },
    ];
    initial.desktop.widgets = [0, 2, 3, 4, 5].flatMap((style, index) =>
      shapes.map((shape) => ({
        ...original,
        ...shape,
        instanceId: `${shape.name}-${style}`,
        y: index * 12 + shape.y,
        options: {
          ...original.options,
          clockStyle: style,
          showSeconds: true,
          hourFormat: 1,
          timeZone: "UTC",
        },
      })),
    );
    initial.useSeparateMobileLayout = true;
    initial.mobile = {
      columns: 4,
      widgets: initial.desktop.widgets.map((widget, index) => ({
        ...widget,
        instanceId: `mobile-${widget.instanceId}`,
        width: 2,
        height: 2,
        x: (index % 2) * 2,
        y: Math.floor(index / 2) * 2,
      })),
    };

    const state = await fixture(page, initial, { fontFamily, backgroundColor: null });

    for (const width of [1440, 414]) {
      await page.setViewportSize({ width, height: 1100 });

      for (const colorScheme of ["light", "dark"] as const) {
        await page.emulateMedia({ colorScheme });

        for (const clock of await page.locator("[data-widget-id]").all()) {
          await expect(clock.locator(".clock-time")).toHaveText(/11:33:30\s*PM/u);
          expect(
            await clock.evaluate(
              (card) =>
                card.scrollHeight <= card.clientHeight + 1 &&
                card.scrollWidth <= card.clientWidth + 1,
            ),
            (await clock.getAttribute("data-widget-id")) ?? "Clock",
          ).toBe(true);
        }

        if (fontFamily === "mono") {
          await page
            .locator('[data-widget-id$="tiny-3"]')
            .screenshot({ path: `/tmp/alexandria-seconds-flip-${width}-${colorScheme}.png` });
        }
      }
    }

    expect(state.writes).toHaveLength(0);
  });
}

test("seconds preview immediately, save only through Done and leave mobile independent with a named local timezone", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.clock.setFixedTime(new Date("2026-10-08T12:08:09Z"));

  const initial = seed();
  initial.desktop.widgets = [initial.desktop.widgets[0]!];
  initial.desktop.widgets[0]!.options = {
    ...initial.desktop.widgets[0]!.options,
    clockStyle: 3,
    hourFormat: 1,
  };
  initial.useSeparateMobileLayout = true;
  initial.mobile = {
    columns: 4,
    widgets: [
      {
        ...initial.desktop.widgets[0]!,
        instanceId: "mobile-clock",
        width: 4,
        options: { ...initial.desktop.widgets[0]!.options, clockStyle: 0, showSeconds: false },
      },
    ],
  };
  const state = await fixture(page, initial);

  await expect(page.locator(".clock-zone")).toHaveText("America / New York");
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await openWidgetTab(page, "Options");
  await page.getByRole("switch", { name: "Show seconds", exact: true }).click();

  await expect(page.locator('[data-clock-part="second"]')).toHaveText("09");
  await expect(page.getByRole("button", { name: "Time zone", exact: true })).toContainText(
    "Local (America / New York)",
  );
  expect(state.writes).toHaveLength(0);

  await closeWidgetSettings(page);
  await page.getByRole("button", { name: "Done", exact: true }).click();

  expect(state.saved.desktop.widgets[0]!.options.showSeconds).toBe(true);
  expect(state.saved.desktop.widgets[0]!.options.timeZone).toBe("local");
  expect(state.saved.mobile!.widgets[0]!.options.showSeconds).toBe(false);

  await page.reload();
  await expect(page.locator(".clock-time")).toHaveText(/08:08:09\s*AM/u);
  await page.setViewportSize({ width: 414, height: 896 });
  await expect(page.locator(".clock-time")).toHaveText(/08:08\s*AM/u);
});
