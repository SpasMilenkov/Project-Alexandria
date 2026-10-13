import { type Locator, type Page, type Route, expect, test } from "@playwright/test";

import { fixture, seed } from "./helpers/home-dashboard.js";
import { closeWidgetSettings, openWidgetTab, selectWidget } from "./helpers/home-grid-editor.js";

const names = ["Documents", "Photos", "Projects", "Backups", "Music", "Videos"];

const folders = names.map((name, index) => ({
  id: `10101010-1010-4010-8010-${String(index).padStart(12, "0")}`,
  name,
  isStarred: true,
  parentId: null,
  createdAt: "2026-10-01T12:00:00Z",
  updatedAt: null,
  ownerUserDto: { id: "home-fixture", name: "Alex", email: "alex@example.test" },
}));

const document = (width = 6, height = 4) => {
  const value = seed();
  const perRow = Math.floor(12 / width);

  value.desktop.widgets = Array.from({ length: 6 }, (_, index) => ({
    ...value.desktop.widgets[0]!,
    instanceId: index < 3 ? `access-${index}` : `upload-${index - 3}`,
    type: index < 3 ? 4 : 5,
    x: (index % perRow) * width,
    y: Math.floor(index / perRow) * height,
    width,
    height,
    options: {
      ...value.desktop.widgets[0]!.options,
      quickAccessStyle: index % 3,
      quickUploadStyle: Math.max(0, index - 3),
    },
  }));

  return value;
};

const paginated = (items: unknown[]) => ({
  currentPage: 0,
  pageSize: 20,
  items,
  totalCount: items.length,
  totalPages: 1,
  hasNext: false,
  hasPrevious: false,
});

const quickFixture = async (
  page: Page,
  value = document(),
  flags: { empty: boolean; fail: boolean; single?: boolean } = { empty: false, fail: false },
) => {
  const searches: URL[] = [];
  const visits: string[] = [];

  const apiHandler = async (route: Route) => {
    const url = new URL(route.request().url());

    if (url.pathname === "/api/directories/search") {
      searches.push(url);
      const items = flags.single ? folders.slice(0, 1) : folders;

      await route.fulfill({
        status: flags.fail ? 503 : 200,
        json: paginated(flags.empty ? [] : items),
      });

      return true;
    }

    if (url.pathname === "/api/directories/sub") {
      visits.push(url.searchParams.get("directoryId") ?? "");
      await route.fulfill({ json: paginated([]) });

      return true;
    }

    if (/\/api\/directories\/path\//u.test(url.pathname)) {
      await route.fulfill({ json: { pathParts: [{ id: folders[0]!.id, name: "Documents" }] } });

      return true;
    }

    if (["/api/files/sub", "/api/files/root", "/api/directories/root"].includes(url.pathname)) {
      await route.fulfill({ json: paginated([]) });

      return true;
    }

    return false;
  };

  const state = await fixture(page, value, {}, undefined, apiHandler);

  return { state, searches, visits };
};

const fits = async (widget: Locator) => {
  await expect
    .poll(() =>
      widget.evaluate((element) => {
        const bounds = element.getBoundingClientRect();
        const targets = element.querySelectorAll(
          ".access-body, .upload-controls, .upload-dial, .pulse-target",
        );

        return (
          [...targets].every((target) => {
            const rect = target.getBoundingClientRect();

            return (
              rect.left >= bounds.left - 1 &&
              rect.right <= bounds.right + 1 &&
              rect.top >= bounds.top - 1 &&
              rect.bottom <= bounds.bottom + 1
            );
          }) &&
          element.scrollWidth <= element.clientWidth + 1 &&
          element.scrollHeight <= element.clientHeight + 1
        );
      }),
    )
    .toBe(true);
};

const dispatchDrop = async (
  widget: Locator,
  kind: "file" | "zip" | "directory",
  eventType: "dragenter" | "drop",
) => {
  await widget.evaluate(
    (element, args) => {
      const file = new File(["hello"], args.kind === "zip" ? "backup.zip" : "notes.txt", {
        type: "text/plain",
      });
      const fileEntry = {
        isFile: true,
        isDirectory: false,
        name: file.name,
        file: (callback: (value: File) => void) => callback(file),
      };
      let entry: unknown = fileEntry;

      if (args.kind === "directory") {
        entry = {
          isFile: false,
          isDirectory: true,
          name: "Documents",
          createReader: () => {
            let read = false;

            return {
              readEntries: (callback: (entries: unknown[]) => void) => {
                const entries = read ? [] : [fileEntry];
                read = true;
                callback(entries);
              },
            };
          },
        };
      }

      const target = element.querySelector(".upload-body") ?? element;
      const event = new DragEvent(args.eventType, { bubbles: true, cancelable: true });

      Object.defineProperty(event, "dataTransfer", {
        value: { types: ["Files"], files: [file], items: [{ webkitGetAsEntry: () => entry }] },
      });

      target.dispatchEvent(event);
    },
    { kind, eventType },
  );
};

for (const dark of [false, true]) {
  for (const compact of [false, true]) {
    test(`six styles fit ${compact ? "compact" : "large"} cards in ${dark ? "dark" : "light"} mode`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1440, height: 1000 });
      await page.emulateMedia({ colorScheme: dark ? "dark" : "light" });
      const { searches } = await quickFixture(page, document(compact ? 2 : 6, compact ? 2 : 4), {
        empty: false,
        fail: false,
        single: !compact,
      });

      await expect(page.locator(".access-body")).toHaveCount(3);
      await expect(page.locator(".upload-body")).toHaveCount(3);

      for (const widget of await page.locator("[data-widget-id]").all()) await fits(widget);

      const tile = page.locator('[data-widget-id="access-0"] .access-folder').first();
      const tileBounds = await tile.boundingBox();
      expect(tileBounds!.width).toBeLessThanOrEqual(176);
      expect(tileBounds!.height).toBeLessThanOrEqual(128);

      expect(searches).toHaveLength(1);
      expect(searches[0]!.searchParams.get("isStarred")).toBe("true");
      expect(searches[0]!.searchParams.get("isDeleted")).toBe("false");
      expect(searches[0]!.searchParams.get("pageSize")).toBe("20");
      await page.screenshot({
        path: `/tmp/alexandria-quick-widgets-${dark ? "dark" : "light"}-${compact ? "compact" : "large"}.png`,
        fullPage: true,
      });
    });
  }
}

test("mobile compact cards keep every upload action reachable", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const value = document(2, 2);
  value.useSeparateMobileLayout = true;
  value.mobile = {
    columns: 4,
    widgets: value.desktop.widgets.map((widget, index) => ({
      ...widget,
      x: (index % 2) * 2,
      y: Math.floor(index / 2) * 2,
    })),
  };
  await quickFixture(page, value);

  await expect(page.locator(".access-body")).toHaveCount(3);

  for (const widget of await page.locator("[data-widget-id]").all()) await fits(widget);

  const target = page.locator('[data-widget-id="upload-0"]');
  await target.getByRole("button", { name: "More upload options" }).click();
  await expect(page.getByRole("menuitem", { name: "Upload folder" })).toBeVisible();
  await page.getByRole("menuitem", { name: "Upload folder" }).click();
  await expect(page.getByRole("dialog", { name: "Upload Directory" })).toBeVisible();
  await page.screenshot({ path: "/tmp/alexandria-quick-widgets-mobile.png", fullPage: true });
});

test("keyboard opening a favorite navigates the explorer to that folder", async ({ page }) => {
  const { visits } = await quickFixture(page);
  const link = page
    .locator('[data-widget-id="access-0"]')
    .getByRole("button", { name: "Open Documents" });
  await link.focus();
  await page.keyboard.press("Enter");

  await expect(page).toHaveURL(/\/dashboard$/u);
  await expect.poll(() => visits).toContain(folders[0]!.id);
});

test("favorites have empty and isolated retry states", async ({ page }) => {
  const flags = { empty: true, fail: false };
  await quickFixture(page, document(), flags);
  await expect(page.getByText("No favorite folders", { exact: true })).toHaveCount(3);

  flags.empty = false;
  flags.fail = true;
  await page.reload();
  await expect(page.locator('[data-widget-id="access-0"]').getByRole("alert")).toBeVisible({
    timeout: 25000,
  });
  await expect(page.locator(".upload-body")).toHaveCount(3);

  flags.fail = false;
  await page
    .locator('[data-widget-id="access-0"]')
    .getByRole("button", { name: "Try again" })
    .click();
  await expect(page.locator(".access-body")).toHaveCount(3);
});

test("styles save with Done and remain independent on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const value = document();
  value.desktop.widgets = [value.desktop.widgets[0]!, value.desktop.widgets[3]!];
  Object.assign(value.desktop.widgets[1]!, { x: 6, y: 0 });
  const { state } = await quickFixture(page, value);

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await selectWidget(page, page.locator('[data-widget-id="access-0"]'));
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "Chips", exact: true }).click();
  await selectWidget(page, page.locator('[data-widget-id="upload-0"]'));
  await page.getByRole("button", { name: "Pulse", exact: true }).click();
  await closeWidgetSettings(page);
  await page.getByRole("switch", { name: "Use a separate mobile layout" }).click();
  await page.getByRole("button", { name: "Mobile", exact: true }).click();
  await selectWidget(page, page.locator('[data-widget-id][aria-label="Quick access"]'));
  await openWidgetTab(page, "Style");
  await page.getByRole("button", { name: "List", exact: true }).click();

  expect(state.writes).toHaveLength(0);
  await page.getByRole("button", { name: "Done", exact: true }).click();
  await expect(page.getByRole("button", { name: "Customize", exact: true })).toBeVisible();
  expect(state.saved.desktop.widgets[0]!.options.quickAccessStyle).toBe(2);
  expect(state.saved.desktop.widgets[1]!.options.quickUploadStyle).toBe(2);
  expect(state.saved.mobile!.widgets[0]!.options.quickAccessStyle).toBe(1);

  await page.reload();
  await expect(page.locator(".access-body")).toHaveAttribute("data-access-style", "2");
});

test("file, folder and archive buttons launch the existing root dialogs", async ({ page }) => {
  await quickFixture(page);
  const target = page.locator('[data-widget-id="upload-1"]');

  for (const [action, title] of [
    ["Upload files", "Upload Files"],
    ["Upload folder", "Upload Directory"],
    ["Extract ZIP", "Upload Archive"],
  ]) {
    await target.getByRole("button", { name: action, exact: true }).click();
    const dialog = page.getByRole("dialog", { name: title });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText("Root directory", { exact: true })).toBeVisible();
    await dialog.getByRole("button", { name: "Close", exact: true }).click();
    await expect(dialog).toHaveCount(0);
  }
});

test("external file and folder drops reach the matching dialogs", async ({ page }) => {
  await quickFixture(page);
  const target = page.locator('[data-widget-id="upload-0"]');

  for (const kind of ["file", "directory"] as const) {
    await dispatchDrop(target, kind, "dragenter");
    await expect(target.locator(".upload-body")).toHaveAttribute("data-drag-over", "true");
    await dispatchDrop(target, kind, "drop");

    const title = kind === "file" ? "Upload Files" : "Upload Directory";
    const dialog = page.getByRole("dialog", { name: title });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText("notes.txt", { exact: true })).toBeVisible();
    if (kind === "directory")
      await expect(dialog.getByText("Documents", { exact: true })).toBeVisible();
    await dialog.getByRole("button", { name: "Close", exact: true }).click();
    await expect(dialog).toHaveCount(0);
  }
});

test("ZIP drops offer both choices and can keep the archive intact", async ({ page }) => {
  await quickFixture(page);
  const target = page.locator('[data-widget-id="upload-0"]');
  await dispatchDrop(target, "zip", "drop");
  const choice = page.getByRole("dialog");

  await expect(choice.getByRole("button", { name: /Upload as Archive/u })).toBeVisible();
  await choice.getByRole("button", { name: /Upload as File/u }).click();

  const dialog = page.getByRole("dialog", { name: "Upload Files" });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText("backup.zip", { exact: true })).toBeVisible();
});

test("editing suppresses external drops and Pulse respects reduced motion", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await quickFixture(page);
  const pulse = page.locator('[data-widget-id="upload-2"]');
  await dispatchDrop(pulse, "file", "dragenter");
  await expect(pulse.locator(".upload-body")).toHaveAttribute("data-drag-over", "true");
  await expect(pulse.locator(".pulse-first")).toHaveCSS("animation-name", "none");

  await page.getByRole("button", { name: "Customize", exact: true }).click();
  await dispatchDrop(pulse, "file", "drop");

  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(pulse.locator(".upload-body")).toHaveAttribute("data-drag-over", "false");
});
