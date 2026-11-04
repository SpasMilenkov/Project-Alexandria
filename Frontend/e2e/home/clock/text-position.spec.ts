import { type Locator, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";

const verifyCaption = async (
  card: Locator,
  anchor: { horizontal: number; vertical: number; row: number },
  index: number,
) => {
  const area = (await card.locator(".clock-alignment").boundingBox())!;
  const caption = (await card.locator(".clock-details").boundingBox())!;
  const face = (await card
    .locator(
      ".clock-quiet-time, .clock-dial, .clock-material-time, .clock-flip-face, .clock-rings-face, .clock-words-face",
    )
    .boundingBox())!;

  expect(
    Math.abs(caption.x - area.x - (anchor.horizontal * (area.width - caption.width)) / 2),
  ).toBeLessThan(2);

  if (anchor.vertical !== 0 || anchor.horizontal !== 1) {
    expect(
      Math.abs(caption.y - area.y - (anchor.row * (area.height - caption.height)) / 2),
    ).toBeLessThan(2);
  }

  const overlapWidth =
    Math.min(caption.x + caption.width, face.x + face.width) - Math.max(caption.x, face.x);
  const overlapHeight =
    Math.min(caption.y + caption.height, face.y + face.height) - Math.max(caption.y, face.y);

  expect(overlapWidth <= 1 || overlapHeight <= 1, `overlap in ${index}`).toBe(true);
  expect(face.x).toBeGreaterThanOrEqual(area.x - 1);
  expect(face.y).toBeGreaterThanOrEqual(area.y - 1);
  expect(face.x + face.width).toBeLessThanOrEqual(area.x + area.width + 1);
  expect(face.y + face.height).toBeLessThanOrEqual(area.y + area.height + 1);
  expect(
    await card.evaluate(
      (element) =>
        element.scrollWidth <= element.clientWidth + 1 &&
        element.scrollHeight <= element.clientHeight + 1,
    ),
    `overflow in ${index}`,
  ).toBe(true);
};

const shapeSets = [
  {
    name: "regular",
    shapes: [
      { width: 6, height: 4 },
      { width: 12, height: 4 },
      { width: 3, height: 6 },
    ],
  },
  {
    name: "compact",
    shapes: [
      { width: 3, height: 2 },
      { width: 2, height: 3 },
      { width: 4, height: 2 },
    ],
  },
];

for (const shapeSet of shapeSets) {
  for (let style = 0; style < 6; style++) {
    test(`clock style ${style} keeps nine caption anchors readable across ${shapeSet.name} shapes and devices`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1440, height: 1100 });
      await page.clock.setFixedTime(new Date("2026-10-08T12:30:09Z"));

      const initial = seed();
      const original = initial.desktop.widgets[0]!;
      const anchors = [1, 0, 2].flatMap((vertical, row) =>
        [0, 1, 2].map((horizontal) => ({ vertical, horizontal, row })),
      );
      initial.desktop.widgets = shapeSet.shapes.flatMap((shape, shapeIndex) =>
        anchors.map((anchor, index) => ({
          ...original,
          ...shape,
          instanceId: `caption-${shapeIndex}-${index}`,
          y: (shapeIndex * 9 + index) * 8,
          options: {
            ...original.options,
            clockStyle: style,
            showSeconds: true,
            horizontalAlignment: 1,
            verticalAlignment: 0,
            textAlignment: anchor.horizontal,
            textVerticalAlignment: anchor.vertical,
          },
        })),
      );
      initial.useSeparateMobileLayout = true;
      initial.mobile = {
        columns: 4,
        widgets: initial.desktop.widgets.map((widget) => ({ ...widget, width: 4 })),
      };
      const state = await fixture(page, initial, { fontFamily: "mono", backgroundColor: null });

      for (const width of [1440, 414]) {
        await page.setViewportSize({ width, height: 1100 });

        for (const colorScheme of ["light", "dark"] as const) {
          await page.emulateMedia({ colorScheme });

          for (const [index, card] of (await page.locator("[data-widget-id]").all()).entries()) {
            const anchor = anchors[index % 9]!;
            await verifyCaption(card, anchor, index);
          }

          if (style === 0 && shapeSet.name === "regular") {
            await page
              .locator('[data-widget-id="caption-1-2"]')
              .screenshot({ path: `/tmp/alexandria-caption-${width}-${colorScheme}.png` });
          }
        }
      }

      expect(state.writes).toHaveLength(0);
    });
  }
}
