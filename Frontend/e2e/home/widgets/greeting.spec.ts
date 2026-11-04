import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  closeWidgetSettings,
  dashboardSettings,
  openWidgetTab,
  selectWidget,
} from "./helpers/home-grid-editor.js";

test.use({ timezoneId: "UTC" });

const document = () => {
  const value = seed();
  value.desktop.widgets = [0, 1].map((style) => ({
    ...value.desktop.widgets[0]!,
    instanceId: `greeting-${style}`,
    type: 16,
    x: 0,
    y: style * 4,
    options: {
      ...value.desktop.widgets[0]!.options,
      greetingStyle: style,
      greetingDecoration: 1,
      greetingAlignment: 0,
      greetingShowName: true,
      greetingShowDate: true,
      greetingHeading: null,
      greetingSubtitle: "Everything in your library, arranged your way.",
    },
  }));

  return value;
};

const widgets = (page: Page) => page.locator("[data-widget-id]");

const fits = async (widget: Locator) => {
  await expect
    .poll(() =>
      widget.evaluate((element) => {
        const bounds = element.getBoundingClientRect();
        const body = element.querySelector(".greeting-body")!.getBoundingClientRect();

        return (
          body.width > 0 &&
          body.height > 0 &&
          body.left >= bounds.left &&
          body.right <= bounds.right + 1 &&
          body.top >= bounds.top &&
          body.bottom <= bounds.bottom + 1 &&
          element.scrollWidth <= element.clientWidth + 1 &&
          element.scrollHeight <= element.clientHeight + 1
        );
      }),
    )
    .toBe(true);
};

const verifyGreetingLayout = async (
  page: Page,
  layout: { viewportWidth: number; width: number; height: number; showTitle: boolean },
) => {
  const { viewportWidth, width, height, showTitle } = layout;

  for (let index = 0; index < 2; index++) {
    const widget = widgets(page).nth(index);
    await fits(widget);
    const dimensions = await widget.locator(".greeting-scroll").evaluate((element) => ({
      width: element.clientWidth,
      height: element.clientHeight,
      contentHeight: element.scrollHeight,
      overflow: element.scrollHeight > element.clientHeight + 1,
    }));

    if (width === 2 && height === 2) {
      await widget.screenshot({
        path: `/tmp/alexandria-greeting-finish/compact-${viewportWidth}-${index}-${showTitle}.png`,
      });
    }

    expect(
      dimensions,
      `viewport ${viewportWidth}, style ${index}, ${width}x${height}, title ${showTitle}: ${JSON.stringify(dimensions)}`,
    ).toMatchObject({ overflow: false });
  }
};

const selectChoice = async (page: Page, name: string, label: string) => {
  await page.getByRole("combobox", { name, exact: true }).click();
  await page.getByRole("option", { name: label, exact: true }).click();
  await expect(page.getByRole("listbox")).toBeHidden();
};

const openGreetingOptions = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Options");
};

const customizeGreeting = async (page: Page) => {
  await page.getByRole("textbox", { name: "Greeting heading", exact: true }).fill("Welcome home");
  await page.getByRole("textbox", { name: "Greeting subtitle", exact: true }).fill("Stay curious");
  await selectChoice(page, "Greeting decoration", "☕ Coffee");
  await selectChoice(page, "Greeting alignment", "Center");
  await page.getByRole("switch", { name: "Show name in greeting", exact: true }).click();
  await page.getByRole("switch", { name: "Show date in greeting", exact: true }).click();
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Welcome", exact: true }).click();
  await closeWidgetSettings(page);
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Show header greeting", exact: true }).click();
};

for (const colorScheme of ["light", "dark"] as const) {
  test(`both greeting styles fit small, tall, wide and large cards in ${colorScheme} mode`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme, reducedMotion: "reduce" });
    await page.clock.install({ time: new Date("2026-10-10T10:00:00Z") });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const state = await fixture(page, document());

    for (const [width, height] of [
      [2, 2],
      [3, 6],
      [12, 2],
      [12, 6],
    ]) {
      state.saved.desktop.widgets.forEach((widget, index) => {
        Object.assign(widget, { width, height, y: index * height! });
      });
      await page.reload();

      for (let index = 0; index < 2; index++) {
        const widget = widgets(page).nth(index);
        await fits(widget);
        await expect(widget.getByRole("heading")).toHaveText("Good morning, Alex");
        await expect(widget.locator(".greeting-date")).not.toBeEmpty();
        await expect(widget.locator(".greeting-decoration")).toHaveAttribute("aria-hidden", "true");
      }
    }

    for (let index = 0; index < 2; index++) {
      await widgets(page)
        .nth(index)
        .screenshot({ path: `/tmp/alexandria-greeting/style-${index}-${colorScheme}.png` });
    }
  });
}

test("emoji and text-only choices scale with the container on desktop and phone", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[1]!];
  const state = await fixture(page, value);
  const widget = widgets(page).first();
  const sizes: number[] = [];

  for (const [width, height] of [
    [3, 2],
    [12, 6],
  ]) {
    Object.assign(state.saved.desktop.widgets[0]!, { width, height, y: 0 });
    await page.reload();
    await fits(widget);
    sizes.push(
      await widget
        .locator(".greeting-art")
        .evaluate((element) => element.getBoundingClientRect().width),
    );
  }

  expect(sizes[1]).toBeGreaterThan(sizes[0]!);
  await page.setViewportSize({ width: 390, height: 844 });

  for (const [decoration, emoji] of [
    [0, ""],
    [1, "👋"],
    [2, "☕"],
    [3, "📚"],
  ] as const) {
    state.saved.desktop.widgets[0]!.options.greetingDecoration = decoration;
    await page.reload();
    await fits(widget);
    await expect(widget.locator("svg")).toHaveCount(0);

    if (emoji) await expect(widget.locator(".greeting-emoji")).toHaveText(emoji);
    else await expect(widget.locator(".greeting-art")).toHaveCount(0);
  }

  await page.screenshot({ path: "/tmp/alexandria-greeting/phone.png", fullPage: true });
});

test("ordinary greetings have no inner vertical overflow at compact sizes with and without titles", async ({
  page,
}) => {
  test.setTimeout(60_000);
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 1440, height: 1000 });
  const state = await fixture(page, document());

  for (const viewportWidth of [1440, 390]) {
    await page.setViewportSize({ width: viewportWidth, height: 1000 });

    for (const showTitle of [false, true]) {
      for (const [width, height] of [
        [2, 2],
        [3, 2],
        [6, 2],
        [12, 2],
        [3, 6],
        [6, 4],
        [12, 6],
      ]) {
        state.saved.desktop.widgets.forEach((widget, index) => {
          Object.assign(widget, { width, height, y: index * height!, showTitle });
        });
        await page.reload();

        await verifyGreetingLayout(page, {
          viewportWidth,
          width: width!,
          height: height!,
          showTitle,
        });
      }
    }
  }
});

test("greeting options and header toggle save through Done and remain independent on mobile", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[0]!];
  const state = await fixture(page, value);
  await openGreetingOptions(page);
  await customizeGreeting(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout", exact: true }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Options");
  await page.getByRole("textbox", { name: "Greeting heading", exact: true }).fill("Hello phone");
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.desktop.widgets[0]!.options).toMatchObject({
    greetingStyle: 1,
    greetingHeading: "Welcome home",
    greetingSubtitle: "Stay curious",
    greetingDecoration: 2,
    greetingAlignment: 1,
    greetingShowName: false,
    greetingShowDate: false,
  });
  expect(state.saved.mobile!.widgets[0]!.options.greetingHeading).toBe("Hello phone");
  expect(state.saved.showHeaderGreeting).toBe(false);
  await page.reload();
  await expect(widgets(page).first().getByRole("heading")).toHaveText("Welcome home");
  await expect(page.locator(".home-heading")).toHaveClass(/sr-only/u);
  await expect(page.getByRole("heading", { name: "Home", exact: true })).toHaveCount(1);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(widgets(page).first().getByRole("heading")).toHaveText("Hello phone");
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
});

test("Cancel discards greeting and header drafts, with undo and redo for the header toggle", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[0]!];
  const state = await fixture(page, value);
  await openGreetingOptions(page);
  await customizeGreeting(page);
  const toggle = page.getByRole("switch", { name: "Show header greeting", exact: true });

  await expect(toggle).toHaveAttribute("aria-checked", "false");
  await page.getByRole("button", { name: "Undo", exact: true }).click();
  await expect(toggle).toHaveAttribute("aria-checked", "true");
  await page.getByRole("button", { name: "Redo", exact: true }).click();
  await expect(toggle).toHaveAttribute("aria-checked", "false");
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Discard and exit", exact: true }).click();

  expect(state.writes).toHaveLength(0);
  await expect(page.locator(".home-heading")).not.toHaveClass(/sr-only/u);
  await expect(widgets(page).first().locator(".greeting-content")).toHaveAttribute(
    "data-greeting-style",
    "0",
  );
  await expect(widgets(page).first().locator(".greeting-emoji")).toHaveText("👋");
});

test("catalogue adds the greeting and renders custom text literally with input limits", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = seed();
  value.desktop.widgets = [];
  const state = await fixture(page, value);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await page.getByRole("button", { name: "Add widget", exact: true }).click();
  await page.getByRole("button", { name: "General", exact: true }).click();
  await page.getByRole("button", { name: "Add Greeting", exact: true }).press("Enter");
  await expect(
    page.getByRole("button", { name: "Close widget catalogue", exact: true }),
  ).toBeHidden();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Options");
  const heading = page.getByRole("textbox", { name: "Greeting heading", exact: true });
  const subtitle = page.getByRole("textbox", { name: "Greeting subtitle", exact: true });

  await expect(heading).toHaveAttribute("maxlength", "80");
  await expect(subtitle).toHaveAttribute("maxlength", "160");
  await heading.fill("<b>Hello</b>");
  await subtitle.fill("<img src=x onerror=alert(1)>");
  await expect(widgets(page).first().getByRole("heading")).toHaveText("<b>Hello</b>, Alex");
  await expect(widgets(page).first().locator("b, img")).toHaveCount(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.batches[0]!.creates[0]!.type).toBe(16);
  expect(Object.keys(state.batches[0]!.creates[0]!.options)).toHaveLength(8);
});

test("maximum-length text remains reachable in the smallest card with a title", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.showWidgetTitles = true;
  value.desktop.widgets.forEach((widget, index) => {
    Object.assign(widget, { width: 2, height: 2, x: index * 2, y: 0 });
    widget.options.greetingHeading = "A".repeat(80);
    widget.options.greetingSubtitle = "B".repeat(160);
  });
  await fixture(page, value);

  for (let index = 0; index < 2; index++) {
    const widget = widgets(page).nth(index);
    await fits(widget);
    const subtitle = widget.locator(".greeting-subtitle");
    await expect(subtitle).toHaveText("B".repeat(160));
    await subtitle.evaluate((element) => element.scrollIntoView({ block: "end" }));
    await expect
      .poll(() =>
        subtitle.evaluate((element) => {
          const bounds = element.getBoundingClientRect();
          const scroller = element.closest(".greeting-scroll")!.getBoundingClientRect();

          return bounds.bottom <= scroller.bottom + 1 && bounds.right <= scroller.right + 1;
        }),
      )
      .toBe(true);
  }

  await page.screenshot({ path: "/tmp/alexandria-greeting/long-text.png", fullPage: true });
});
