import { fixture, seed } from "@e2e/helpers/home-dashboard.js";
import { type Page, expect, test } from "@playwright/test";

const directoryId = "20202020-2020-4020-8020-202020202020";
const fileId = "30303030-3030-4030-8030-303030303030";

const paginated = (items: unknown[]) => ({
  currentPage: 0,
  hasNext: false,
  hasPrevious: false,
  items,
  pageSize: 20,
  totalCount: items.length,
  totalPages: items.length ? 1 : 0,
});

const searchResult = {
  createdAt: "2026-10-01T12:00:00Z",
  currentVersion: {
    createdAt: "2026-10-01T12:00:00Z",
    id: "40404040-4040-4040-8040-404040404040",
    isDeleted: false,
    isEncrypted: false,
    mimeType: "application/pdf",
    size: "1024",
    versionNumber: 1,
  },
  deletedAt: null,
  directoryId,
  fileId,
  fileName: "quarterly-report.pdf",
  mimeType: "application/pdf",
  owner: { email: "alex@example.test", id: "home-fixture", name: "Alex" },
  tags: [],
  updatedAt: null,
};

const searchFixture = async (page: Page) => {
  await fixture(page, seed(), {}, undefined, async (route) => {
    const path = new URL(route.request().url()).pathname;

    if (path === "/api/files/search") {
      await route.fulfill({ json: paginated([searchResult]) });

      return true;
    }

    if (path === "/api/directories/search") {
      await route.fulfill({ json: paginated([]) });

      return true;
    }

    // The folder the result lives in lists it, so the pointer lands on it after the jump.
    if (path === `/api/files/directory/${directoryId}`) {
      await route.fulfill({ json: paginated([searchResult]) });

      return true;
    }

    if (path.startsWith("/api/directories/") || path.startsWith("/api/files/")) {
      await route.fulfill({ json: paginated([]) });

      return true;
    }

    return false;
  });

  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Nothing here yet" })).toBeVisible();
};

// Nuxt UI binds "meta" to Cmd for a Mac user agent, which Playwright's WebKit reports.
const searchShortcut = (browserName: string) => (browserName === "webkit" ? "Meta+/" : "Control+/");

// Firefox fires no blur when a focused input leaves the page, which used to leave every
// shortcut ignored until the next click. One app-wide guard covers every dialog.
test("search opens again with the shortcut after jumping to a result", async ({
  browserName,
  page,
}) => {
  await searchFixture(page);
  const dialog = page.getByRole("dialog");

  await page.keyboard.press(searchShortcut(browserName));
  await expect(dialog).toBeVisible();

  await page.keyboard.type("report");
  await page.getByRole("option").filter({ hasText: "quarterly-report.pdf" }).click();
  await expect(dialog).toBeHidden();

  await page.keyboard.press(searchShortcut(browserName));
  await expect(dialog).toBeVisible();
});

test("search opens again with the shortcut after opening a result with Enter", async ({
  browserName,
  page,
}) => {
  await searchFixture(page);
  const dialog = page.getByRole("dialog");

  await page.keyboard.press(searchShortcut(browserName));
  await expect(dialog).toBeVisible();

  await page.keyboard.type("report");
  await expect(page.getByRole("option").filter({ hasText: "quarterly-report.pdf" })).toBeVisible();
  await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await expect(dialog).toBeHidden();

  await page.keyboard.press(searchShortcut(browserName));
  await expect(dialog).toBeVisible();
});

test("search opens again with the shortcut after closing it with Escape", async ({
  browserName,
  page,
}) => {
  await searchFixture(page);
  const dialog = page.getByRole("dialog");

  await page.keyboard.press(searchShortcut(browserName));
  await expect(dialog).toBeVisible();

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();

  await page.keyboard.press(searchShortcut(browserName));
  await expect(dialog).toBeVisible();
});

// A different dialog: the shortcut list autofocuses its filter, so it closes with an input focused.
test("search opens after closing the shortcut list with Escape", async ({ browserName, page }) => {
  await searchFixture(page);
  const dialog = page.getByRole("dialog");
  const modifier = browserName === "webkit" ? "Meta" : "Control";

  await page.keyboard.press(`${modifier}+k`);
  await expect(page.getByPlaceholder("Search by action or key")).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();

  await page.keyboard.press(searchShortcut(browserName));
  await expect(page.getByRole("combobox", { name: "Search by name or filter" })).toBeVisible();
});
