import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  closeWidgetSettings,
  dashboardSettings,
  openWidgetTab,
  selectWidget,
} from "./helpers/home-grid-editor.js";

const artwork = [
  [4, "Pixel sun"],
  [5, "Pixel moon"],
  [6, "Pixel coffee"],
  [7, "Pixel plant"],
  [8, "Pixel cat"],
  [9, "Pixel dog"],
  [10, "Pixel duck"],
  [11, "Pixel vinyl"],
] as const;

const emojis = [
  [1, "👋"],
  [2, "☕"],
  [3, "📚"],
  [12, "☀️"],
  [13, "🌙"],
  [14, "🪴"],
  [15, "🐈"],
  [16, "🐕"],
  [17, "🦆"],
  [18, "💿"],
] as const;

const greetingBoard = () => {
  const value = seed();
  value.desktop.widgets = artwork.flatMap(([decoration], index) =>
    [0, 1].map((style) => ({
      ...value.desktop.widgets[0]!,
      instanceId: `art-${decoration}-${style}`,
      type: 16,
      x: style * 6,
      y: index * 4,
      width: 6,
      height: 4,
      options: {
        ...value.desktop.widgets[0]!.options,
        greetingStyle: style,
        greetingDecoration: decoration,
        greetingHeading: "Welcome home",
        greetingSubtitle: "Make yourself at home.",
      },
    })),
  );

  return value;
};

const widgets = (page: Page) => page.locator("[data-widget-id]");

const artworkFits = async (widget: Locator, colored = false) => {
  await widget.scrollIntoViewIfNeeded();
  const svg = widget.locator(".greeting-pixel-art");
  await expect(svg).toBeVisible();
  await expect(svg).toHaveAttribute("viewBox", "0 0 16 16");
  await expect(svg).toHaveAttribute("aria-hidden", "true");
  await expect(svg).toHaveAttribute("focusable", "false");

  expect(
    await svg.evaluate((element, colorMode) => {
      const bounds = element.getBoundingClientRect();
      const artElement = element.closest(".greeting-art")!;
      const art = artElement.getBoundingClientRect();
      const copy = element.closest(".greeting-content")!.querySelector(".greeting-copy")!;
      const text = copy.getBoundingClientRect();

      return (
        bounds.width >= 32 &&
        Math.abs(bounds.width - bounds.height) < 1 &&
        Math.abs(bounds.width - art.width) < 1 &&
        Math.abs(bounds.height - art.height) < 1 &&
        (bounds.bottom <= text.top + 1 || bounds.right <= text.left + 1) &&
        (getComputedStyle(element).fill !== getComputedStyle(artElement).color) === colorMode &&
        getComputedStyle(element).shapeRendering.toLowerCase() === "crispedges" &&
        element.querySelectorAll("path").length > 0 &&
        element.querySelectorAll("metadata, script, foreignObject, image").length === 0
      );
    }, colored),
  ).toBe(true);
};

const verifyAnimationFrozen = async (page: Page, svg: Locator) => {
  const animationTime = () =>
    svg.evaluate((element) => Number(element.getAnimations({ subtree: true })[0]!.currentTime));
  const paused = await animationTime();

  await page.evaluate(async () => {
    for (let frame = 0; frame < 10; frame++) {
      await new Promise<number>((resolve) => {
        requestAnimationFrame(resolve);
      });
    }
  });

  expect(await animationTime()).toBe(paused);
};

const verifyDocumentVisibility = async (page: Page, decoration: Locator, svg: Locator) => {
  for (const visibility of ["hidden", "visible"] as const) {
    await page.evaluate((value) => {
      Object.defineProperty(document, "visibilityState", { configurable: true, value });
      document.dispatchEvent(new Event("visibilitychange"));
    }, visibility);

    await expect(decoration).toHaveAttribute(
      "data-animation-running",
      String(visibility === "visible"),
    );
    await expect
      .poll(() =>
        svg.evaluate((element) =>
          element.getAnimations({ subtree: true }).map((animation) => animation.playState),
        ),
      )
      .toContain(visibility === "visible" ? "running" : "paused");
  }
};

const selectPixelArtwork = async (page: Page) => {
  const glyphs = new Set<string | null>();

  for (const [, label] of artwork) {
    await page.getByRole("combobox", { name: "Greeting decoration", exact: true }).click();
    await page.getByRole("option", { name: label, exact: true }).click();
    await expect(widgets(page).first().locator(".greeting-pixel-art")).toBeVisible();
    glyphs.add(
      await widgets(page).first().locator(".greeting-pixel-art path").first().getAttribute("d"),
    );
  }

  expect(glyphs.size).toBe(8);
};

const selectArtworkColors = async (page: Page, label: string) => {
  await page.getByRole("combobox", { name: "Greeting artwork colors", exact: true }).click();
  await page.getByRole("option", { name: label, exact: true }).click();
};

const configureMobileArtwork = async (page: Page) => {
  await closeWidgetSettings(page);
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout", exact: true }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Options");
  await selectArtworkColors(page, "Monochrome");
  await page.getByRole("combobox", { name: "Greeting decoration", exact: true }).click();
  await page.getByRole("option", { name: "🦆 Duck", exact: true }).click();
  await expect(
    page.getByRole("combobox", { name: "Greeting artwork colors", exact: true }),
  ).toHaveCount(0);
};

const verifyPickerArtwork = async (page: Page, mode: number) => {
  for (const [, label] of artwork) {
    const option = page.getByRole("option", { name: label, exact: true });
    await option.scrollIntoViewIfNeeded();
    const svg = option.locator(".greeting-pixel-art");
    await expect(svg).toBeVisible();
    await expect(svg).toHaveAttribute("data-color-mode", String(mode));
    await expect(svg).toHaveAttribute("aria-hidden", "true");
    expect(
      await svg.evaluate((element) => ({
        width: element.getBoundingClientRect().width,
        height: element.getBoundingClientRect().height,
        animations: element.getAnimations({ subtree: true }).length,
      })),
    ).toEqual({ width: 24, height: 24, animations: 0 });
  }

  await expect(
    page.getByRole("option", { name: "None", exact: true }).locator(".greeting-pixel-art"),
  ).toHaveCount(0);
  await expect(
    page
      .getByRole("option", { name: "👋 Waving hand", exact: true })
      .locator(".greeting-pixel-art"),
  ).toHaveCount(0);
};

test("decoration picker shows static pixel thumbnails beside choices and the selected value", async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = greetingBoard();
  value.desktop.widgets = [value.desktop.widgets[2]!];
  await fixture(page, value);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Options");
  const picker = page.getByRole("combobox", { name: "Greeting decoration", exact: true });

  await expect(picker.locator(".greeting-pixel-art")).toHaveAttribute("data-color-mode", "0");
  await picker.click();
  await verifyPickerArtwork(page, 0);
  await page.getByRole("option", { name: "Pixel moon", exact: true }).click();
  await selectArtworkColors(page, "Colored");
  await expect(picker.locator(".greeting-pixel-art")).toHaveAttribute("data-color-mode", "1");
  await picker.click();
  await verifyPickerArtwork(page, 1);
  await page.getByRole("option", { name: "Pixel moon", exact: true }).scrollIntoViewIfNeeded();
  await page
    .getByRole("listbox")
    .screenshot({ path: "/tmp/alexandria-greeting-finish/picker.png" });
  await page.getByRole("option", { name: "None", exact: true }).click();
  await expect(picker.locator(".greeting-pixel-art")).toHaveCount(0);
});

for (const colorScheme of ["light", "dark"] as const) {
  test(`monochrome and colored versions of all eight designs render in ${colorScheme}`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme, reducedMotion: "reduce" });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const value = greetingBoard();
    value.desktop.widgets.forEach((widget, index) => {
      widget.options.greetingColorMode = index % 2;
      widget.options.greetingStyle = Math.floor(index / 2) % 2;
    });
    await fixture(page, value);

    for (const [index, [decoration]] of artwork.entries()) {
      const colors = [];

      for (const mode of [0, 1]) {
        const widget = widgets(page).nth(index * 2 + mode);
        await artworkFits(widget, mode === 1);
        const svg = widget.locator("svg");
        await expect(svg).toHaveAttribute("data-color-mode", String(mode));
        colors.push(
          await svg
            .locator("path")
            .evaluateAll((paths) => paths.map((path) => getComputedStyle(path).fill)),
        );

        if (mode === 1) {
          await svg.screenshot({
            path: `/tmp/alexandria-greeting-finish/art-${decoration}-${colorScheme}.png`,
          });
        }
      }

      expect(new Set(colors[0]).size).toBe(1);
      expect(new Set(colors[1]).size).toBeGreaterThan(1);
    }

    await widgets(page)
      .nth(9)
      .screenshot({ path: `/tmp/alexandria-greeting-finish/colored-cat-${colorScheme}.png` });
  });

  test(`all supplied pixel designs fit both greeting styles and respect reduced motion in ${colorScheme}`, async ({
    page,
  }) => {
    test.setTimeout(60_000);
    await page.emulateMedia({ colorScheme, reducedMotion: "reduce" });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const state = await fixture(page, greetingBoard());
    const first = widgets(page).first().locator("svg");
    const sizes: number[] = [];

    for (const [width, height] of [
      [3, 2],
      [3, 6],
      [12, 2],
      [6, 6],
    ]) {
      const columns = 12 / width!;
      state.saved.desktop.widgets.forEach((widget, index) => {
        Object.assign(widget, {
          width,
          height,
          x: (index % columns) * width!,
          y: Math.floor(index / columns) * height!,
        });
      });
      await page.reload();

      for (let index = 0; index < 16; index++) {
        const widget = widgets(page).nth(index);
        await artworkFits(widget);
        expect(
          await widget
            .locator("svg")
            .evaluate((svg) => svg.getAnimations({ subtree: true }).length),
        ).toBe(0);
      }

      sizes.push(await first.evaluate((svg) => svg.getBoundingClientRect().width));
    }

    expect(sizes.at(-1)).toBeGreaterThan(sizes[0]!);
    for (const index of [8, 9, 6, 7]) {
      await widgets(page)
        .nth(index)
        .screenshot({ path: `/tmp/alexandria-greeting-art/card-${index}-${colorScheme}.png` });
    }

    await page.setViewportSize({ width: 390, height: 844 });
    for (let index = 0; index < 16; index++) {
      await artworkFits(widgets(page).nth(index));
    }
  });
}

test("animations run, pause outside the viewport and on visibility changes, and stop for reduced motion", async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await page.setViewportSize({ width: 1440, height: 900 });
  await fixture(page, greetingBoard());
  const first = widgets(page).first();
  const decoration = first.locator(".greeting-decoration");
  const svg = first.locator("svg");
  const states = () =>
    svg.evaluate((element) =>
      element.getAnimations({ subtree: true }).map((animation) => animation.playState),
    );
  const time = () =>
    svg.evaluate((element) => Number(element.getAnimations({ subtree: true })[0]!.currentTime));

  await expect(decoration).toHaveAttribute("data-animation-running", "true");
  await expect.poll(states).toContain("running");
  const animationCount = (await states()).length;
  const initial = await time();
  await expect.poll(time).toBeGreaterThan(initial);

  await widgets(page).last().scrollIntoViewIfNeeded();
  await expect(decoration).toHaveAttribute("data-animation-running", "false");
  await expect.poll(states).toEqual(Array(animationCount).fill("paused"));
  await verifyAnimationFrozen(page, svg);

  await first.scrollIntoViewIfNeeded();
  await expect.poll(states).toContain("running");
  await verifyDocumentVisibility(page, decoration, svg);

  await page.emulateMedia({ reducedMotion: "reduce" });
  await expect
    .poll(() => svg.evaluate((element) => element.getAnimations({ subtree: true }).length))
    .toBe(0);
  const plant = widgets(page).nth(6);
  await plant.scrollIntoViewIfNeeded();
  expect(
    await plant
      .locator("svg g")
      .evaluateAll((groups) => groups.every((group) => getComputedStyle(group).opacity === "1")),
  ).toBe(true);
});

test("every new decoration is selectable and a pixel choice persists independently from a mobile emoji", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = greetingBoard();
  value.desktop.widgets = [value.desktop.widgets[0]!];
  const state = await fixture(page, value);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Options");

  await selectPixelArtwork(page);
  await selectArtworkColors(page, "Colored");
  expect(state.writes).toHaveLength(0);
  await configureMobileArtwork(page);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options).toMatchObject({
    greetingDecoration: 11,
    greetingColorMode: 1,
  });
  expect(state.saved.mobile!.widgets[0]!.options).toMatchObject({
    greetingDecoration: 17,
    greetingColorMode: 0,
  });
  await page.reload();
  await expect(widgets(page).first().locator(".greeting-pixel-art")).toBeVisible();
  await expect(widgets(page).first().locator(".greeting-pixel-art")).toHaveAttribute(
    "data-color-mode",
    "1",
  );
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(widgets(page).first().locator(".greeting-emoji")).toHaveText("🦆");
});

test("matching emojis and the original waving hand render on phone", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const value = greetingBoard();
  const template = value.desktop.widgets[1]!;
  value.desktop.widgets = emojis.map(([decoration], index) => ({
    ...template,
    instanceId: `emoji-${decoration}`,
    y: index * 4,
    options: { ...template.options, greetingDecoration: decoration },
  }));
  await fixture(page, value);

  for (const [index, [, emoji]] of emojis.entries()) {
    const widget = widgets(page).nth(index);
    await widget.scrollIntoViewIfNeeded();
    await expect(widget.locator(".greeting-emoji")).toHaveText(emoji);
    await expect(widget.locator("svg")).toHaveCount(0);
  }
});
