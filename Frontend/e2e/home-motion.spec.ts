import { expect, test } from "@playwright/test";

import { fixture } from "./helpers/home-dashboard.js";

test("resize and displaced cards animate without delaying geometry or saving", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.emulateMedia({ reducedMotion: "no-preference" });

  const state = await fixture(page);

  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const clock = page.locator('[data-widget-id="default-clock"]');

  await clock.focus();

  const before = (await clock.boundingBox())!;

  const during = await clock.locator("..").evaluate(async (element) => {
    element
      .querySelector('[data-resize-edge="bottom"]')!
      .dispatchEvent(
        new KeyboardEvent("keydown", { key: "ArrowDown", bubbles: true, cancelable: true }),
      );
    await new Promise(requestAnimationFrame);

    const board = element.parentElement!;
    const animations = board.getAnimations({ subtree: true });

    for (const animation of animations) {
      animation.pause();
      animation.currentTime = 75;
    }

    return {
      count: animations.length,
      height: element.getBoundingClientRect().height,
      row: (element as HTMLElement).style.gridRow,
      libraryRow: board.querySelector<HTMLElement>('[data-home-motion="default-library"]')!.style
        .gridRow,
      frames: animations.map((animation) => (animation.effect as KeyframeEffect).getKeyframes()),
    };
  });

  expect(during.count).toBe(2);
  expect(during.height).toBeGreaterThan(before.height);
  expect(during.height).toBeLessThan(before.height + 80);
  expect(during.row).toBe("1 / span 5");
  expect(during.libraryRow).toBe("6 / span 6");
  expect(JSON.stringify(during.frames)).not.toContain("scale");
  expect(state.writes).toHaveLength(0);

  await page.emulateMedia({ reducedMotion: "reduce" });

  await expect
    .poll(() =>
      clock
        .locator("..")
        .evaluate((element) => element.parentElement!.getAnimations({ subtree: true }).length),
    )
    .toBe(0);

  expect((await clock.boundingBox())!.height).toBe(before.height + 80);

  await clock.press("ArrowRight");

  expect(
    await clock
      .locator("..")
      .evaluate((element) => element.parentElement!.getAnimations({ subtree: true }).length),
  ).toBe(0);

  await page.getByRole("button", { name: "Preview", exact: true }).click();

  expect(state.writes).toHaveLength(0);
});

test("Preview clears in-flight movement and uses the full desktop width", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await fixture(page);
  await page.getByRole("button", { name: "Customize", exact: true }).click();

  const clock = page.locator('[data-widget-id="default-clock"]');

  await clock.focus();

  const before = (await clock.boundingBox())!;

  await clock.evaluate(async (element) => {
    element.dispatchEvent(
      new KeyboardEvent("keydown", {
        key: "ArrowRight",
        bubbles: true,
        cancelable: true,
      }),
    );
    await new Promise(requestAnimationFrame);

    for (const animation of element.parentElement!.getAnimations()) animation.pause();
  });
  await page.getByRole("button", { name: "Preview", exact: true }).click();

  await expect
    .poll(() =>
      clock
        .locator("..")
        .evaluate((element) => element.parentElement!.getAnimations({ subtree: true }).length),
    )
    .toBe(0);

  expect((await clock.boundingBox())!.width).toBeGreaterThan(before.width);
  expect(
    await clock.locator("..").evaluate((element) => (element as HTMLElement).style.gridColumn),
  ).toBe("2 / span 6");
});
