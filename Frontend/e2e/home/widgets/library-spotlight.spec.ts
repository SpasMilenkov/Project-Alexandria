import { type Locator, type Page, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";

const document = () => {
  const value = seed();
  value.desktop.widgets = [
    {
      ...value.desktop.widgets[1]!,
      x: 0,
      y: 0,
      width: 12,
      height: 6,
      options: {
        ...value.desktop.widgets[1]!.options,
        libraryStyle: 2,
        shortcutGroup: 1,
        featuredDestination: 11,
      },
    },
  ];

  return value;
};

const feature = (page: Page) => page.locator('[data-featured="true"]');

const artworkFits = async (link: Locator) => {
  await expect(link.locator(".library-feature-art")).toBeVisible();
  await expect(link.locator(".library-feature-art")).toHaveAttribute("aria-hidden", "true");
  await expect(link.locator(".spotlight-glyph path")).not.toHaveCount(0);
  await expect
    .poll(async () => {
      const artwork = await link.locator(".spotlight-glyph").innerHTML();
      const destination = await link.locator(".library-glyph").innerHTML();

      return artwork === destination;
    })
    .toBe(true);
  await expect
    .poll(() =>
      link.evaluate((element) => {
        const bounds = element.getBoundingClientRect();
        const art = element.querySelector(".library-feature-art")!.getBoundingClientRect();
        const copy = element.querySelector(".library-copy")!.getBoundingClientRect();
        const glyph = element.querySelector(".spotlight-glyph")!.getBoundingClientRect();
        const orbit = element.querySelector(".spotlight-orbit")!.getBoundingClientRect();

        return (
          art.height > 0 &&
          glyph.width >= 32 &&
          art.left >= bounds.left &&
          art.right <= bounds.right &&
          art.top >= bounds.top &&
          art.bottom <= copy.top &&
          orbit.top >= art.top - 1 &&
          orbit.bottom <= art.bottom + 1 &&
          copy.bottom <= bounds.bottom &&
          element.scrollHeight <= element.clientHeight + 1 &&
          element.scrollWidth <= element.clientWidth + 1
        );
      }),
    )
    .toBe(true);
};

const compactLayout = async (page: Page) => {
  const link = feature(page);

  await page.reload();
  await expect(link.locator(".library-feature-art")).toBeHidden();
  await expect(link.locator(".library-icon")).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(link.locator(".library-feature-art")).toBeHidden();
  await expect(link).toHaveAttribute("href", "/dashboard");
};

for (const colorScheme of ["light", "dark"] as const) {
  test(`Spotlight artwork fills the large featured card and adapts to destinations in ${colorScheme}`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme, reducedMotion: "reduce" });
    await page.setViewportSize({ width: 1440, height: 1000 });
    const state = await fixture(page, document());
    const link = feature(page);
    await expect(link).toHaveAccessibleName("Your listening");
    await expect(link).toHaveAttribute("href", "/stats");
    await artworkFits(link);
    await link.screenshot({ path: `/tmp/alexandria-spotlight/listening-${colorScheme}.png` });

    Object.assign(state.saved.desktop.widgets[0]!.options, {
      shortcutGroup: 3,
      shortcuts: Array.from({ length: 8 }, (_, destination) => ({
        destination,
        label: destination === 4 ? "A collection of things to revisit later" : null,
      })),
      featuredDestination: 4,
    });
    await page.reload();
    await expect(link).toHaveAccessibleName("A collection of things to revisit later");
    await artworkFits(link);
    await link.screenshot({ path: `/tmp/alexandria-spotlight/custom-${colorScheme}.png` });

    Object.assign(state.saved.desktop.widgets[0]!.options, {
      shortcuts: [{ destination: 0, label: "Files" }],
      featuredDestination: null,
    });
    await page.reload();
    await expect(link).toHaveAccessibleName("Files");
    await artworkFits(link);
    await link.focus();
    await expect(link).toBeFocused();

    Object.assign(state.saved.desktop.widgets[0]!, { height: 2 });
    await compactLayout(page);
  });
}
