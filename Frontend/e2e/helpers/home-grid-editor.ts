import { type Locator, type Page, expect } from "@playwright/test";

export const openControls = async (page: Page) => {
  const button = page.getByRole("button", { name: "Layout controls", exact: true });
  const drawer = page.getByRole("dialog", { name: "Layout controls", exact: true });

  if ((await button.isVisible()) && !(await drawer.isVisible())) await button.click();
};

export const closeControls = async (page: Page) => {
  const drawer = page.getByRole("dialog", { name: "Layout controls", exact: true });

  if (await drawer.isVisible()) {
    await drawer.getByRole("button", { name: "Back to layout", exact: true }).click();

    await expect(drawer).not.toBeVisible();
  }
};

export const selectWidget = async (page: Page, widget: Locator) => {
  await closeControls(page);
  await widget.focus();
  await widget.press("Enter");

  await expect(widget).toHaveAttribute("data-selected", "true");

  await openControls(page);

  await expect(
    page.getByRole("region", { name: "Widget editing controls", exact: true }),
  ).toBeVisible();
};

export const removeWidget = async (page: Page, widget: Locator) => {
  await selectWidget(page, widget);
  await page
    .getByRole("region", { name: "Widget editing controls", exact: true })
    .getByRole("button", { name: "Remove widget", exact: true })
    .click();
  await closeControls(page);
};

export const gridPoint = async (page: Page, x: number, y: number, offset = { x: 16, y: 16 }) => {
  const grid = page.locator('[aria-label="Home widgets"]');
  const bounds = (await grid.boundingBox())!;
  const columns = Number(await grid.getAttribute("data-grid-columns"));

  return {
    x: bounds.x + x * ((bounds.width + 16) / columns) + offset.x,
    y: bounds.y + y * 80 + offset.y,
  };
};

export const dragWidget = async (page: Page, origin: Locator, x: number, y: number) => {
  await origin.scrollIntoViewIfNeeded();

  const bounds = (await origin.boundingBox())!;
  const card = await origin.evaluate((element) => {
    const section = element.closest("[data-widget-id]")!;
    const rectangle = section.getBoundingClientRect();

    return { x: rectangle.x, y: rectangle.y };
  });
  const start = {
    x: bounds.x + Math.min(24, bounds.width / 2),
    y: bounds.y + Math.min(16, bounds.height / 2),
  };
  const target = await gridPoint(page, x, y, { x: start.x - card.x, y: start.y - card.y });

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 12 });

  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-valid", "true");

  await page.mouse.up();
};

export const dragCatalogue = async (page: Page, type: number, x: number, y: number) => {
  const grid = page.locator('[aria-label="Home widgets"]');

  await grid.scrollIntoViewIfNeeded();

  const catalogue = page.locator(`[data-catalogue-widget="${type}"]`);
  const source = (await catalogue.boundingBox())!;
  const gridBounds = (await grid.boundingBox())!;
  const columns = Number(await grid.getAttribute("data-grid-columns"));
  const width = columns === 12 ? 6 : 4;
  const target = await gridPoint(page, x, y, {
    x: (((gridBounds.width + 16) / columns) * width - 16) / 2,
    y: 32,
  });

  await page.mouse.move(source.x + 16, source.y + 16);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 12 });

  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-valid", "true");

  await page.mouse.up();
};

export const dragTouchWidget = async (page: Page, widget: Locator, x: number, y: number) => {
  await widget.scrollIntoViewIfNeeded();

  const bounds = (await widget.boundingBox())!;
  const start = { x: bounds.x + 16, y: bounds.y + 16 };
  const target = await gridPoint(page, x, y);
  const session = await page.context().newCDPSession(page);

  const gesture = session.send("Input.synthesizeScrollGesture", {
    x: start.x,
    y: start.y,
    xDistance: target.x - start.x,
    yDistance: target.y - start.y,
    gestureSourceType: "touch",
    speed: 300,
  });

  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-valid", "true");

  await gesture;

  await expect(page.locator("[data-placement-ghost]")).toHaveCount(0);

  await session.detach();
};

export const resizePoints = async (
  page: Page,
  widget: Locator,
  edge: string,
  dx: number,
  dy: number,
) => {
  await widget.focus();

  await expect(widget).toHaveAttribute("data-selected", "true");

  const handle = widget.locator("..").locator(`[data-resize-edge="${edge}"]`);

  await handle.scrollIntoViewIfNeeded();

  const bounds = (await handle.boundingBox())!;
  const grid = page.locator('[aria-label="Home widgets"]');
  const gridBounds = (await grid.boundingBox())!;
  const columns = Number(await grid.getAttribute("data-grid-columns"));
  const start = { x: bounds.x + bounds.width / 2, y: bounds.y + bounds.height / 2 };

  return {
    start,
    end: { x: start.x + (dx * (gridBounds.width + 16)) / columns, y: start.y + dy * 80 },
  };
};

export const resizeWidget = async (
  page: Page,
  widget: Locator,
  edge: string,
  dx: number,
  dy: number,
) => {
  const { start, end } = await resizePoints(page, widget, edge, dx, dy);

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(end.x, end.y, { steps: 12 });

  await expect(page.locator('[aria-label="Home widgets"]')).toHaveAttribute(
    "data-resizing",
    "true",
  );
  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-valid", "true");

  await page.mouse.up();

  await expect(page.locator("[data-placement-ghost]")).toHaveCount(0);
};

export const resizeTouchWidget = async (
  page: Page,
  widget: Locator,
  edge: string,
  dx: number,
  dy: number,
) => {
  const { start, end } = await resizePoints(page, widget, edge, dx, dy);
  const session = await page.context().newCDPSession(page);
  const gesture = session.send("Input.synthesizeScrollGesture", {
    x: start.x,
    y: start.y,
    xDistance: end.x - start.x,
    yDistance: end.y - start.y,
    gestureSourceType: "touch",
    speed: 300,
  });

  await expect(page.locator('[aria-label="Home widgets"]')).toHaveAttribute(
    "data-resizing",
    "true",
  );
  await expect(page.locator("[data-placement-ghost]")).toHaveAttribute("data-valid", "true");

  await gesture;

  await expect(page.locator("[data-placement-ghost]")).toHaveCount(0);

  await session.detach();
};
