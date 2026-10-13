import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import {
  closeWidgetSettings,
  dashboardSettings,
  dragWidget,
  openWidgetTab,
  selectWidget,
} from "./helpers/home-grid-editor.js";

const document = (width = 6, height = 4) => {
  const value = seed();
  const perRow = Math.floor(12 / width);

  value.desktop.widgets = Array.from({ length: 3 }, (_, index) => ({
    ...value.desktop.widgets[1]!,
    instanceId: `library-${index}`,
    x: (index % perRow) * width,
    y: Math.floor(index / perRow) * height,
    width,
    height,
    options: { ...value.desktop.widgets[1]!.options, libraryStyle: index },
  }));

  return value;
};

const fits = async (widget: Locator) => {
  await expect
    .poll(() =>
      widget.evaluate((element) => {
        const bounds = element.getBoundingClientRect();
        const body = element.querySelector(".library-body")!;
        const rectangle = body.getBoundingClientRect();

        return (
          rectangle.left >= bounds.left &&
          rectangle.right <= bounds.right + 1 &&
          rectangle.top >= bounds.top &&
          rectangle.bottom <= bounds.bottom + 1 &&
          element.scrollWidth <= element.clientWidth + 1 &&
          element.scrollHeight <= element.clientHeight + 1
        );
      }),
    )
    .toBe(true);
};

const widgets = (page: Page) => page.locator("[data-widget-id]");

const expectTooltip = async (page: Page, link: Locator, label: string) => {
  const tooltip = page.getByRole("tooltip", { includeHidden: true });

  await expect(tooltip).toHaveText(label);
  await expect(tooltip.locator("..")).toBeVisible();
  await expect(link).toHaveAccessibleDescription(label);
};

const iconsFit = async (widget: Locator, count: number, expectedSize?: number) => {
  await fits(widget);
  await expect(widget.getByRole("link")).toHaveCount(count);
  await expect(widget.locator(".library-copy, .library-arrow")).toHaveCount(0);
  await expect(widget.getByRole("button", { name: /More shortcuts/u })).toHaveCount(0);
  await expect
    .poll(() =>
      widget.getByRole("link").evaluateAll((links) =>
        links.every((link) => {
          const bounds = link.getBoundingClientRect();

          const glyph = link.querySelector(".library-glyph")!.getBoundingClientRect();

          return (
            Math.abs(bounds.width - bounds.height) < 0.1 &&
            bounds.width >= 48 &&
            bounds.width <= 80 &&
            glyph.width >= 20 &&
            glyph.width <= 32 &&
            Math.abs(glyph.width - glyph.height) < 0.1
          );
        }),
      ),
    )
    .toBe(true);

  if (expectedSize !== undefined)
    await expect
      .poll(() =>
        widget
          .getByRole("link")
          .first()
          .evaluate((link) => link.getBoundingClientRect().width),
      )
      .toBe(expectedSize);
};

const checkPhoneIcons = async (page: Page, value: ReturnType<typeof seed>) => {
  value.showWidgetTitles = true;
  Object.assign(value.desktop.widgets[0]!, { width: 3, height: 2 });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  const widget = widgets(page).first();
  await iconsFit(widget, 8, 48);
  const last = widget.getByRole("link").last();

  await last.focus();
  await expect(last).toBeFocused();
  await expect(last).toBeInViewport();
  await page.screenshot({ path: "/tmp/alexandria-library-final/icons-phone.png", fullPage: true });

  value.desktop.widgets[0]!.options.shortcuts = [{ destination: 0, label: "Files" }];
  value.desktop.widgets[0]!.options.featuredDestination = null;
  await page.reload();
  await iconsFit(widget, 1);
  const fileLink = widget.getByRole("link", { name: "Files", exact: true });

  await fileLink.focus();
  await expectTooltip(page, fileLink, "Files");
  await fileLink.press("Enter");
  await expect(page).toHaveURL(/\/dashboard$/u);
};

const openLibraryOptions = async (page: Page, widget: Locator) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, widget);
  await openWidgetTab(page, "Options");
};

const selectChoice = async (page: Page, name: string, label: string) => {
  const role = name === "Add destination" ? "button" : "combobox";

  await page.getByRole(role, { name, exact: true }).click();
  await page.getByRole("option", { name: label, exact: true }).click();
  await expect(page.getByRole("listbox")).toBeHidden();
};

const customizeMixedLibrary = async (page: Page) => {
  await selectChoice(page, "Shortcut group", "Files and media");
  await page.getByRole("button", { name: "Reorder Music", exact: true }).focus();
  await page.keyboard.press("ArrowUp");
  await page.getByRole("button", { name: "Remove Tags and Categories", exact: true }).click();
  await selectChoice(page, "Add destination", "Trash");
  await page.getByRole("textbox", { name: "Short label for Music", exact: true }).fill("Listen");
  await expect(
    page.getByRole("textbox", { name: "Short label for Music", exact: true }),
  ).toHaveValue("Listen");
  await expect(
    widgets(page).first().getByRole("link", { name: "Listen", exact: true }),
  ).toBeVisible();
  await selectChoice(page, "Featured destination", "Trash");
};

const editMobileLibrary = async (page: Page, widget: Locator) => {
  await closeWidgetSettings(page);
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await selectWidget(page, widget);
  await openWidgetTab(page, "Options");
};

for (const colorScheme of ["light", "dark"] as const) {
  test(`three library styles fit large, tall and wide shapes in ${colorScheme} mode`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme, reducedMotion: "reduce" });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const value = document();
    const state = await fixture(page, value);

    for (const [width, height] of [
      [6, 4],
      [3, 6],
      [12, 2],
      [12, 6],
    ]) {
      state.saved.desktop.widgets.forEach((widget, index) => {
        Object.assign(widget, { x: 0, y: index * height!, width, height });
      });
      await page.reload();
      await expect(widgets(page)).toHaveCount(3);

      for (let index = 0; index < 3; index++) {
        const widget = widgets(page).nth(index);
        await fits(widget);
        await expect(widget.locator(".library-body")).toHaveAttribute(
          "data-library-style",
          String(index),
        );
        await expect(widget.getByRole("link")).toHaveCount(4);
        await expect(
          widget.getByRole("link", { name: "Your Storage", exact: true }),
        ).toHaveAttribute("href", "/my-storage");
      }
    }

    for (let index = 0; index < 3; index++) {
      await widgets(page)
        .nth(index)
        .screenshot({
          path: `/tmp/alexandria-library-shortcuts/style-${index}-${colorScheme}.png`,
        });
    }
  });
}

test("compact phone cards expose all shortcuts through keyboard-accessible overflow", async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const value = document(3, 2);
  value.showWidgetTitles = true;
  await fixture(page, value);

  for (let index = 0; index < 3; index++) {
    const widget = widgets(page).nth(index);
    await fits(widget);
    const more = widget.getByRole("button", { name: /More shortcuts/u });
    await expect(more).toHaveAttribute("aria-expanded", "false");
    await more.focus();
    await more.press("Enter");
    await expect(widget.getByRole("link")).toHaveCount(4);
    await fits(widget);

    const last = widget.getByRole("link", { name: "Your Storage", exact: true });
    await last.focus();
    await expect(last).toBeFocused();
    await expect(last).toBeInViewport();
    await widget.getByRole("button", { name: "Fewer shortcuts", exact: true }).click();
    await expect(more).toHaveAttribute("aria-expanded", "false");
  }

  await page.screenshot({
    path: "/tmp/alexandria-library-shortcuts/compact-phone.png",
    fullPage: true,
  });
  await widgets(page)
    .first()
    .getByRole("button", { name: /More shortcuts/u })
    .click();
  const history = widgets(page).first().getByRole("link", { name: "Access History", exact: true });
  await history.focus();
  await history.press("Enter");
  await expect(page).toHaveURL(/\/access-history$/u);
});

test("style drafts save with Done, reload, and remain independent on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[0]!];
  const state = await fixture(page, value);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Icons", exact: true }).click();
  await expect(widgets(page).first().locator(".library-body")).toHaveAttribute(
    "data-library-style",
    "3",
  );
  await closeWidgetSettings(page);
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await selectWidget(page, widgets(page).first());
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Directory", exact: true }).click();

  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.libraryStyle).toBe(3);
  expect(state.saved.mobile!.widgets[0]!.options.libraryStyle).toBe(1);

  await page.reload();
  await expect(widgets(page).first().locator(".library-body")).toHaveAttribute(
    "data-library-style",
    "3",
  );
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(widgets(page).first().locator(".library-body")).toHaveAttribute(
    "data-library-style",
    "1",
  );
});

test("shortcut links keep whole-card dragging and do not navigate during editing", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[0]!];
  value.desktop.widgets[0]!.options.libraryStyle = 3;
  const state = await fixture(page, value);
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  const widget = widgets(page).first();
  const link = widget.getByRole("link", { name: "File Explorer", exact: true });

  await expect(link).toHaveAttribute("aria-disabled", "true");
  await expect(link).toHaveAttribute("tabindex", "-1");
  await link.dispatchEvent("click");
  await expect(page).toHaveURL(/\/home$/u);
  await dragWidget(page, link, 0, 5);
  await expect(page).toHaveURL(/\/home$/u);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.y).toBe(5);
});

for (const colorScheme of ["light", "dark"] as const) {
  test(`icon squares wrap, expose tooltips and keep every destination reachable in ${colorScheme} mode`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme, reducedMotion: "reduce" });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const value = document();
    value.desktop.widgets = [value.desktop.widgets[0]!];
    value.desktop.widgets[0]!.options = {
      ...value.desktop.widgets[0]!.options,
      libraryStyle: 3,
      shortcutGroup: 3,
      shortcuts: Array.from({ length: 8 }, (_, destination) => ({ destination, label: null })),
      featuredDestination: 7,
    };
    const state = await fixture(page, value);
    const widget = widgets(page).first();

    for (const [width, height] of [
      [6, 4],
      [3, 6],
      [12, 2],
      [12, 6],
    ]) {
      Object.assign(state.saved.desktop.widgets[0]!, { width, height });
      await page.reload();
      await iconsFit(widget, 8);

      if (height === 2) await iconsFit(widget, 8, 48);

      if (width === 6)
        await expect
          .poll(() =>
            widget
              .getByRole("link")
              .first()
              .evaluate((link) => link.getBoundingClientRect().width),
          )
          .toBeGreaterThan(48);
    }

    await iconsFit(widget, 8, 80);
    await expect(widget.locator(".library-glyph").first()).toHaveCSS("width", "32px");

    const first = widget.getByRole("link", { name: "File Explorer", exact: true });
    await expect(widget.getByRole("link").first()).toHaveAccessibleName("File Explorer");
    await first.hover();
    await expectTooltip(page, first, "File Explorer");
    await widget.screenshot({ path: `/tmp/alexandria-library-final/icons-${colorScheme}.png` });
    await checkPhoneIcons(page, state.saved);
  });
}

test("media styles retain their existing destinations", async ({ page }) => {
  const value = document();
  value.desktop.widgets.forEach((widget) => {
    widget.options.shortcutGroup = 1;
  });
  await fixture(page, value);

  for (let index = 0; index < 3; index++) {
    const widget = widgets(page).nth(index);
    await expect(widget.getByRole("link", { name: "Music", exact: true })).toHaveAttribute(
      "href",
      "/streaming/music",
    );
    await expect(widget.getByRole("link", { name: "Your listening", exact: true })).toHaveAttribute(
      "href",
      "/stats",
    );
  }
});

test("custom order, labels and Spotlight feature save independently on desktop and mobile", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[2]!];
  const state = await fixture(page, value);
  const widget = widgets(page).first();

  await openLibraryOptions(page, widget);
  await customizeMixedLibrary(page);
  await expect(widget.locator('[data-featured="true"]')).toHaveAccessibleName("Trash");

  await editMobileLibrary(page, widget);
  await page.getByRole("textbox", { name: "Short label for Trash", exact: true }).fill("Bin");
  await page.getByRole("textbox", { name: "Short label for Trash", exact: true }).press("Tab");
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();

  expect(state.saved.desktop.widgets[0]!.options).toMatchObject({
    shortcutGroup: 3,
    shortcuts: [
      { destination: 6, label: "Listen" },
      { destination: 0, label: null },
      { destination: 8, label: null },
      { destination: 4, label: null },
    ],
    featuredDestination: 4,
  });
  expect(state.saved.mobile!.widgets[0]!.options.shortcuts?.[3]?.label).toBe("Bin");
  await page.reload();
  await expect(widget.locator('[data-featured="true"]')).toHaveAccessibleName("Trash");
  await expect(widget.locator(".library-label")).toHaveText([
    "Trash",
    "Listen",
    "File Explorer",
    "Playlists",
  ]);
  await widget.screenshot({ path: "/tmp/alexandria-library-phase2/custom-spotlight.png" });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(widget.locator('[data-featured="true"]')).toHaveAccessibleName("Bin");
  await fits(widget);
});

test("one and eight custom destinations fit each style and enforce inspector limits", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets.forEach((widget) => {
    widget.options.shortcutGroup = 3;
    widget.options.shortcuts = Array.from({ length: 8 }, (_, destination) => ({
      destination,
      label: null,
    }));
    widget.options.featuredDestination = 7;
  });
  const state = await fixture(page, value);

  for (let index = 0; index < 3; index++) {
    const widget = widgets(page).nth(index);
    await fits(widget);
    await expect(widget.getByRole("link")).toHaveCount(8);
    await widget.getByRole("link").last().focus();
    await expect(widget.getByRole("link").last()).toBeInViewport();
    await widget.screenshot({ path: `/tmp/alexandria-library-phase2/eight-${index}.png` });
  }

  await openLibraryOptions(page, widgets(page).first());
  await expect(page.getByRole("button", { name: "Add destination", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Cancel", exact: true }).click();

  state.saved.desktop.widgets.forEach((widget) => {
    widget.options.shortcuts = [{ destination: 0, label: "<b>Files</b>" }];
    widget.options.featuredDestination = null;
  });
  await page.reload();

  for (let index = 0; index < 3; index++) {
    const widget = widgets(page).nth(index);
    await expect(widget.getByRole("link")).toHaveAccessibleName("<b>Files</b>");
    await expect(widget.locator(".library-label b")).toHaveCount(0);
    await fits(widget);
  }

  await openLibraryOptions(page, widgets(page).first());
  await expect(
    page.getByRole("button", { name: "Remove File Explorer", exact: true }),
  ).toBeDisabled();
});

test("listening destinations open and focus their corresponding sections", async ({ page }) => {
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[1]!];
  value.desktop.widgets[0]!.options.shortcutGroup = 3;
  value.desktop.widgets[0]!.options.shortcuts = [
    { destination: 13, label: null },
    { destination: 12, label: null },
  ];
  await fixture(page, value);

  for (const [name, hash] of [
    ["Recaps", "recaps"],
    ["Timeline", "timeline"],
  ]) {
    await page.goto("/home");
    await widgets(page).first().getByRole("link", { name, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/stats#${hash}$`, "u"));
    await expect(page.locator(`#${hash}`)).toBeFocused();
  }
});
