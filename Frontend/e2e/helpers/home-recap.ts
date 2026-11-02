import { type Locator, expect } from "@playwright/test";

const clippedRecapText = (cards: Locator) =>
  cards.evaluateAll((elements) =>
    elements.flatMap((element) => {
      const face = element.getBoundingClientRect();
      const targets = element.querySelectorAll(
        ".poster-eyebrow, .poster-title, .poster-status, .poster-facts, .poster-value, .poster-facts dt, .poster-open, .recap-heading, .recap-numbers dt, .recap-numbers dd, .recap-face > .recap-extra",
      );

      return [...targets].flatMap((target) => {
        const bounds = target.getBoundingClientRect();

        if (!bounds.width || !bounds.height) return [];

        if (
          target.matches(".recap-primary-value, .poster-value") &&
          target.scrollWidth > target.clientWidth + 1
        )
          return [{ text: target.textContent?.trim(), width: face.width, height: face.height }];

        const stats = target.closest(".recap-numbers, .poster-facts")?.getBoundingClientRect();
        const top = Math.max(face.top, stats?.top ?? face.top);
        const bottom = Math.min(face.bottom, stats?.bottom ?? face.bottom);

        if (
          bounds.top >= top - 1 &&
          bounds.bottom <= bottom + 1 &&
          bounds.left >= face.left - 1 &&
          bounds.right <= face.right + 1
        )
          return [];

        return [{ text: target.textContent?.trim(), width: face.width, height: face.height }];
      });
    }),
  );

// Measured only after fonts load and the layout settles; a single early snapshot under load
// can catch text mid-reflow and report it as clipped.
export const expectRecapContentFits = async (cards: Locator) => {
  await cards.first().evaluate(() => document.fonts.ready);

  await expect
    .poll(() => clippedRecapText(cards), {
      message: "Recap text must fit inside its visible content area",
    })
    .toEqual([]);
};
