import { type Locator, type Page, expect, test } from "@playwright/test";

import { fileWidgetFixture, sampleFiles, widgetDocument } from "./helpers/home-file-widgets.js";
import {
  closeWidgetSettings,
  dashboardSettings,
  dragWidget,
  openWidgetTab,
  selectWidget,
} from "./helpers/home-grid-editor.js";

test.use({ timezoneId: "UTC" });

const contained = async (widget: Locator) => {
  await expect
    .poll(() =>
      widget.evaluate((element) => {
        const bounds = element.getBoundingClientRect();
        const targets = element.querySelectorAll(
          ".storage-dial, .tank, .file-row, .file-tile, .table-row, .deck-hero, .legend-row",
        );

        return (
          [...targets].every((target) => {
            const rectangle = target.getBoundingClientRect();
            if (!rectangle.width || !rectangle.height) return true;

            return (
              rectangle.left >= bounds.left - 1 &&
              rectangle.right <= bounds.right + 1 &&
              rectangle.top >= bounds.top - 1 &&
              rectangle.bottom <= bounds.bottom + 1
            );
          }) && element.scrollHeight <= element.clientHeight + 1
        );
      }),
    )
    .toBe(true);
};

for (const dark of [false, true]) {
  test(`all ten styles render in ${dark ? "dark" : "light"} mode with shared data`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.emulateMedia({ colorScheme: dark ? "dark" : "light", reducedMotion: "reduce" });
    const { requests } = await fileWidgetFixture(page, widgetDocument(), {
      backgroundColor: dark ? "#101113" : "#f5f5f4",
    });

    await expect(page.locator(".storage-body")).toHaveCount(5);
    await expect(page.locator(".recent-body")).toHaveCount(5);
    await expect(page.getByRole("alert")).toHaveCount(0);
    await expect(page.getByText(/cleanup|could free|not accessed/iu)).toHaveCount(0);
    for (const widget of await page.locator("[data-widget-id]").all()) await contained(widget);

    expect(requests.storage).toBe(2);
    expect(requests.storageSummaries).toBe(1);
    expect(requests.searches).toHaveLength(1);
    expect(requests.directories).toBe(0);
    for (const index of [1, 2, 3, 4]) {
      await page
        .locator(`[data-widget-id="recent-${index}"]`)
        .screenshot({ path: `/tmp/alexandria-recent-${index}-${dark ? "dark" : "light"}.png` });
    }

    await page.screenshot({
      path: `/tmp/alexandria-file-widgets-${dark ? "dark" : "light"}.png`,
      fullPage: true,
    });
  });
}

test("compact, banner, tall and large widgets retain whole rows and proportional graphics", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const { state } = await fileWidgetFixture(page, widgetDocument(2, 2));

  for (const [width, height] of [
    [2, 2],
    [6, 2],
    [2, 6],
    [4, 4],
    [12, 6],
  ]) {
    state.saved = widgetDocument(width!, height!);
    await page.reload();
    await expect(page.locator(".storage-body")).toHaveCount(5);
    await expect(page.locator(".recent-body")).toHaveCount(5);
    for (const widget of await page.locator("[data-widget-id]").all()) await contained(widget);

    const bounds = (await page
      .locator('[data-widget-id="storage-1"] .storage-dial')
      .boundingBox())!;
    expect(Math.abs(bounds.width - bounds.height)).toBeLessThan(1);
    expect(bounds.width).toBeGreaterThan(24);

    if (width === 12) {
      const waffle = page.locator('[data-widget-id="storage-2"]');
      const textSize = await waffle
        .locator(".storage-value")
        .evaluate((element) => Number.parseFloat(getComputedStyle(element).fontSize));
      const labelSize = await waffle
        .locator(".storage-legend")
        .evaluate((element) => Number.parseFloat(getComputedStyle(element).fontSize));

      expect(textSize).toBeGreaterThan(48);
      expect(labelSize).toBeGreaterThan(16);
      await waffle.screenshot({ path: "/tmp/alexandria-storage-large-type.png" });
    }
  }
});

test("storage legends keep category labels and values in all four presentations", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.emulateMedia({ colorScheme: "dark", reducedMotion: "reduce" });

  const document = widgetDocument(6, 6);
  document.desktop.widgets = document.desktop.widgets.filter((widget) => widget.type === 2);
  await fileWidgetFixture(page, document);

  for (const index of [0, 1, 2, 4]) {
    const widget = page.locator(`[data-widget-id="storage-${index}"]`);
    const legend = widget.locator(".storage-legend");

    await expect(legend.getByText("Video", { exact: true })).toBeVisible();
    await expect(legend.getByText("Audio", { exact: true })).toBeVisible();
    await expect(legend.locator("b").first()).toContainText("GB");
    await contained(widget);

    await widget.screenshot({ path: `/tmp/alexandria-storage-legend-${index}.png` });
  }
});

test("phone widgets fit the projected four-column layout", async ({ page }) => {
  await page.setViewportSize({ width: 414, height: 896 });
  await page.emulateMedia({ colorScheme: "dark", reducedMotion: "reduce" });
  await fileWidgetFixture(page, widgetDocument(2, 3), { backgroundColor: "#101113" });

  await expect(page.locator(".recent-body")).toHaveCount(5);
  for (const widget of await page.locator("[data-widget-id]").all()) await contained(widget);

  await page.screenshot({ path: "/tmp/alexandria-file-widgets-phone.png", fullPage: true });
});

test("two-column audio lists keep filenames and metadata inside each row", async ({ page }) => {
  await page.setViewportSize({ width: 1000, height: 800 });

  const document = widgetDocument(2, 6);
  document.desktop.widgets = document.desktop.widgets.filter(
    (widget) => widget.instanceId === "recent-0",
  );
  const files = sampleFiles().map((file, index) => ({
    ...file,
    fileName: `Long recording session with a descriptive filename-${index}.mp3`,
    mimeType: "audio/mpeg",
    currentVersion: { ...file.currentVersion, mimeType: "audio/mpeg", size: 40558920 },
  }));
  await fileWidgetFixture(page, document, {}, { files });

  const widget = page.locator('[data-widget-id="recent-0"]');
  await expect(widget.locator(".file-row").first()).toBeVisible();
  await expect
    .poll(() =>
      widget.locator(".file-row").evaluateAll((rows) =>
        rows.every((row) => {
          const bounds = row.getBoundingClientRect();

          return [...row.querySelectorAll(".file-main, .middle-file-name, .file-secondary")].every(
            (content) => {
              const rectangle = content.getBoundingClientRect();
              if (!rectangle.width || !rectangle.height) return true;

              return (
                rectangle.top >= bounds.top &&
                rectangle.bottom <= bounds.bottom &&
                rectangle.left >= bounds.left &&
                rectangle.right <= bounds.right
              );
            },
          );
        }),
      ),
    )
    .toBe(true);

  await widget.screenshot({ path: "/tmp/alexandria-recent-audio-narrow.png" });
});

test("table switches cached created and updated feeds without saving a layout", async ({
  page,
}) => {
  const document = widgetDocument();
  document.desktop.widgets = document.desktop.widgets.filter(
    (widget) => widget.instanceId === "recent-4",
  );
  const { requests, state } = await fileWidgetFixture(page, document);
  const table = page.locator('[data-widget-id="recent-4"]');

  await expect(table.getByRole("button", { name: "Open Mountain-0.jpg" })).toBeVisible();
  await table.getByRole("combobox", { name: "Recent files feed" }).click();
  await page.getByRole("option", { name: "Updated", exact: true }).click();

  await expect(table.getByRole("button", { name: "Open Quarterly report-1.pdf" })).toBeVisible();
  await expect(table.getByRole("button", { name: "Open Mountain-0.jpg" })).toHaveCount(0);
  expect(requests.searches.at(-1)!.searchParams.get("updatedAfter")).toBe("0001-01-01");
  expect(state.writes).toHaveLength(0);

  await table.getByRole("combobox", { name: "Recent files feed" }).click();
  await page.getByRole("option", { name: "Created", exact: true }).click();
  await expect(table.getByRole("button", { name: "Open Mountain-0.jpg" })).toBeVisible();
  expect(requests.searches).toHaveLength(2);
});

const customizeMobileStorage = async (page: Page) => {
  await dashboardSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await expect(page.getByRole("button", { name: "Mobile", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );

  await selectWidget(page, page.locator('[data-widget-id][aria-label="Storage"]'));
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Tank", exact: true }).click();
  await expect(page.getByRole("button", { name: "Tank", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await expect(
    page.locator('[data-widget-id][aria-label="Storage"] .storage-body'),
  ).toHaveAttribute("data-storage-style", "4");
};

const customizeFileStyles = async (page: Page) => {
  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, page.locator('[data-widget-id="storage-0"]'));
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Ring", exact: true }).click();
  await expect(page.locator('[data-widget-id="storage-0"] .storage-body')).toHaveAttribute(
    "data-storage-style",
    "1",
  );

  await selectWidget(page, page.locator('[data-widget-id="recent-0"]'));
  await page.getByRole("button", { name: "Timeline", exact: true }).click();
  await openWidgetTab(page, "Options");
  await page.getByRole("combobox", { name: "Files to show" }).click();
  await page.getByRole("option", { name: "Recently updated", exact: true }).click();
  await closeWidgetSettings(page);
  await customizeMobileStorage(page);
};

test("style and feed options save on Done independently on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const document = widgetDocument();
  document.desktop.widgets = document.desktop.widgets.filter((widget) =>
    ["storage-0", "recent-0"].includes(widget.instanceId),
  );
  const { state } = await fileWidgetFixture(page, document);

  await customizeFileStyles(page);
  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.storageStyle).toBe(1);
  expect(state.saved.desktop.widgets[1]!.options.recentFilesStyle).toBe(2);
  expect(state.saved.desktop.widgets[1]!.options.recentFilesSource).toBe(1);
  expect(state.saved.mobile!.widgets[0]!.options.storageStyle).toBe(4);

  await page.reload();
  await expect(page.locator('[data-widget-id="storage-0"] .storage-body')).toHaveAttribute(
    "data-storage-style",
    "1",
  );
});

test("loading and failed storage stay isolated and retry succeeds", async ({ page }) => {
  let release = () => {};
  const flags = {
    failStorage: true,
    waitStorage: new Promise<void>((resolve) => {
      release = resolve;
    }),
  };
  await fileWidgetFixture(page, widgetDocument(), {}, flags);

  await expect(page.locator('[data-widget-id="storage-0"]').getByRole("status")).toBeVisible();
  await expect(page.locator('[data-widget-id="recent-0"] .file-row').first()).toBeVisible();
  release();
  await expect(page.locator('[data-widget-id="storage-0"]').getByRole("alert")).toBeVisible({
    timeout: 25000,
  });

  flags.failStorage = false;
  await page
    .locator('[data-widget-id="storage-0"]')
    .getByRole("button", { name: "Try again" })
    .click();
  await expect(page.locator(".storage-body")).toHaveCount(5);
});

test("empty unlimited accounts show honest storage and empty states", async ({ page }) => {
  await fileWidgetFixture(page, widgetDocument(), {}, { empty: true, unlimited: true });

  await expect(page.getByText("No recent files", { exact: true })).toHaveCount(5);
  await expect(page.locator(".storage-body")).toHaveCount(5);
  await expect(page.getByText(/NaN|Infinity/u)).toHaveCount(0);
  await expect(page.locator(".tank-liquid")).toHaveCount(0);
  await expect(page.getByText("No storage limit", { exact: true }).first()).toBeVisible();
});

test("disabled thumbnails issue no image requests", async ({ page }) => {
  const { requests } = await fileWidgetFixture(page, widgetDocument(), {
    thumbnailsEnabled: false,
  });

  await expect(page.locator(".recent-body")).toHaveCount(5);
  await expect(page.locator(".recent-body img")).toHaveCount(0);
  expect(requests.thumbnails).toBe(0);
});

test("missing and encrypted thumbnails retain file icons", async ({ page }) => {
  await fileWidgetFixture(page, widgetDocument(), {}, { missingThumbnail: true });
  const tile = page.getByRole("button", { name: "Open Mountain-0.jpg" }).first();

  await expect(
    page.locator('[data-widget-id="recent-1"] .file-tile').first().locator('[aria-hidden="true"]'),
  ).toBeVisible();
  await expect(
    page.locator('[data-widget-id="recent-1"] [aria-label="Open Mountain-4.jpg"] img'),
  ).toHaveCount(0);
  await expect(tile).toBeVisible();
});

test("the full file row remains draggable during customization", async ({ page }) => {
  const document = widgetDocument(2, 3);
  document.desktop.widgets = document.desktop.widgets.filter(
    (widget) => widget.instanceId === "recent-0",
  );
  const { state } = await fileWidgetFixture(page, document);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await dragWidget(page, page.locator('[data-widget-id="recent-0"] .file-row').first(), 8, 0);
  await page.getByRole("button", { name: "Done", exact: true }).click();

  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.x).toBe(8);
  await expect(page.getByRole("dialog")).toHaveCount(0);
});

test("recent file rows open the shared file details flow", async ({ page }) => {
  const document = widgetDocument();
  document.desktop.widgets = document.desktop.widgets.filter(
    (widget) => widget.instanceId === "recent-0",
  );
  await fileWidgetFixture(page, document);
  await page.getByRole("button", { name: "Open Quarterly report-1.pdf" }).click();

  await expect(page.getByRole("dialog", { name: "Quarterly report-1.pdf" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Details", exact: true })).toBeVisible();
  await expect(page.locator('[data-widget-id="recent-0"] .recent-body')).toBeVisible();
});

test("Deck shows supported document thumbnails and opens file details", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "no-preference" });

  const document = widgetDocument();
  document.desktop.widgets = document.desktop.widgets.filter(
    (widget) => widget.instanceId === "recent-3",
  );
  const files = sampleFiles();
  const featured = files[0]!;
  featured.fileName = "Project brief.pdf";
  featured.mimeType = "application/pdf";
  featured.currentVersion.mimeType = "application/pdf";

  await fileWidgetFixture(page, document, { thumbnailsEnabled: true }, { files });

  const hero = page.locator(".deck-hero");
  const thumbnail = hero.locator("img");
  await expect(thumbnail).toHaveAttribute("src", "/api/files/file-0/versions/version-0/thumbnail");
  await expect(thumbnail).not.toHaveClass(/invisible/u);

  await hero.hover();
  await expect
    .poll(() => hero.evaluate((element) => getComputedStyle(element).transform))
    .toBe("matrix(1, 0, 0, 1, 0, -2)");

  await hero.click();
  await expect(page.getByRole("dialog", { name: "Project brief.pdf" })).toBeVisible();
});

test("empty recent files can open the existing upload flow", async ({ page }) => {
  const document = widgetDocument();
  document.desktop.widgets = document.desktop.widgets.filter(
    (widget) => widget.instanceId === "recent-0",
  );
  await fileWidgetFixture(page, document, {}, { empty: true });
  await page.getByRole("button", { name: "Upload", exact: true }).click();

  await expect(page.getByRole("dialog", { name: "Upload Files", exact: true })).toBeVisible();
  await expect(
    page.getByText("Drop files here or select to upload", { exact: true }),
  ).toBeVisible();
});

test("Tank changes from amber to red as the quota approaches", async ({ page }) => {
  const document = widgetDocument();
  document.desktop.widgets = document.desktop.widgets.filter(
    (widget) => widget.instanceId === "storage-4",
  );
  const flags = { quotaBytes: 100e9 };
  await fileWidgetFixture(page, document, {}, flags);
  const tank = page.locator(".tank-layout");

  await expect(tank).toHaveClass(/near-limit/u);
  await expect(tank).not.toHaveClass(/almost-limit/u);
  await expect(tank.getByText("92%", { exact: true })).toBeVisible();
  flags.quotaBytes = 95e9;
  await page.reload();

  await expect(tank).toHaveClass(/almost-limit/u);
  await expect(tank.getByText("97%", { exact: true })).toBeVisible();
});
