import { type Locator, type Page, type Route, expect, test } from "@playwright/test";

import { fixture, seed } from "@e2e/helpers/home-dashboard.js";

interface FakeFolder {
  id: string;
  name: string;
  parentId: string | null;
}

interface FakeFile {
  fileId: string;
  fileName: string;
  directoryId: string | null;
}

interface MoveCall {
  kind: "files" | "directories";
  ids: string[];
  destinationId: string | null;
}

const PHOTOS = "aaaaaaaa-0000-4000-8000-000000000001";
const YEAR = "aaaaaaaa-0000-4000-8000-000000000002";
const SUMMER = "aaaaaaaa-0000-4000-8000-000000000003";
const ROOT_FOLDERS = 250;
const ROOT_FILES = 250;
const PAGE_SIZE = 25;

const pad = (value: number) => String(value).padStart(3, "0");
const folderId = (index: number) => `bbbbbbbb-0000-4000-8000-000000000${pad(index)}`;
const fileId = (index: number) => `cccccccc-0000-4000-8000-000000000${pad(index)}`;

const owner = { email: "alex@example.test", id: "home-fixture", name: "Alex" };
const stamp = "2026-10-01T12:00:00Z";

const buildTree = () => {
  const folders: FakeFolder[] = [
    { id: PHOTOS, name: "Photos", parentId: null },
    { id: YEAR, name: "2024", parentId: PHOTOS },
    { id: SUMMER, name: "Summer", parentId: YEAR },
  ];
  const files: FakeFile[] = [];

  for (let index = 1; index < ROOT_FOLDERS; index++) {
    folders.push({ id: folderId(index), name: `Folder ${pad(index)}`, parentId: null });
  }

  for (let index = 0; index < ROOT_FILES; index++) {
    files.push({ directoryId: null, fileId: fileId(index), fileName: `file-${pad(index)}.txt` });
  }

  files.push({ directoryId: PHOTOS, fileId: fileId(900), fileName: "cover.jpg" });

  return { files, folders };
};

const toFolderDto = (folder: FakeFolder) => ({
  createdAt: stamp,
  id: folder.id,
  isStarred: false,
  name: folder.name,
  ownerUserDto: owner,
  parentId: folder.parentId,
  updatedAt: stamp,
});

const toFileDto = (file: FakeFile) => ({
  createdAt: stamp,
  currentVersion: {
    id: `${file.fileId}-v1`,
    isDeleted: false,
    isEncrypted: false,
    mimeType: "text/plain",
    size: "1024",
    versionNumber: 1,
  },
  deletedAt: null,
  directoryId: file.directoryId,
  fileId: file.fileId,
  fileName: file.fileName,
  mimeType: "text/plain",
  owner,
  tags: [],
  updatedAt: stamp,
});

const paginate = <T>(items: T[], url: URL) => {
  const page = Number(url.searchParams.get("page") ?? 1);
  const pageSize = Number(url.searchParams.get("pageSize") ?? PAGE_SIZE);
  const slice = items.slice((page - 1) * pageSize, page * pageSize);

  return {
    currentPage: page,
    hasNext: page * pageSize < items.length,
    hasPrevious: page > 1,
    items: slice,
    pageSize,
    totalCount: items.length,
    totalPages: Math.ceil(items.length / pageSize),
  };
};

const explorerFixture = async (page: Page) => {
  const tree = buildTree();
  const moves: MoveCall[] = [];

  const foldersIn = (parentId: string | null) =>
    tree.folders.filter((folder) => folder.parentId === parentId).map(toFolderDto);
  const filesIn = (directoryId: string | null) =>
    tree.files.filter((file) => file.directoryId === directoryId).map(toFileDto);

  const pathTo = (id: string) => {
    const parts: { id: string; name: string }[] = [];
    let current = tree.folders.find((folder) => folder.id === id);

    while (current) {
      parts.unshift({ id: current.id, name: current.name });
      const parentId = current.parentId;
      current = tree.folders.find((folder) => folder.id === parentId);
    }

    return parts;
  };

  // Listing reads answer from the in-memory tree; null means "not a listing".
  const listingFor = (url: URL): unknown => {
    const path = url.pathname;
    const lastSegment = path.split("/").at(-1) ?? "";

    if (path === "/api/directories/root") return paginate(foldersIn(null), url);
    if (path === "/api/files/root") return paginate(filesIn(null), url);
    if (path === "/api/directories/sub") {
      return paginate(foldersIn(url.searchParams.get("directoryId")), url);
    }
    if (path.startsWith("/api/files/directory/")) return paginate(filesIn(lastSegment), url);
    if (path.startsWith("/api/directories/path/")) return { pathParts: pathTo(lastSegment) };

    return null;
  };

  const applyMove = (path: string, body: Record<string, unknown>): boolean => {
    const destinationId = body.destinationId as string | null;

    if (path === "/api/files/move") {
      const ids = body.fileIds as string[];

      moves.push({ destinationId, ids, kind: "files" });
      for (const file of tree.files) {
        if (ids.includes(file.fileId)) file.directoryId = destinationId;
      }

      return true;
    }

    if (path === "/api/directories/move") {
      const ids = body.directoryIds as string[];

      moves.push({ destinationId, ids, kind: "directories" });
      for (const folder of tree.folders) {
        if (ids.includes(folder.id)) folder.parentId = destinationId;
      }

      return true;
    }

    return false;
  };

  const handler = async (route: Route) => {
    const url = new URL(route.request().url());

    if (route.request().method() === "POST") {
      const body = route.request().postDataJSON() as Record<string, unknown>;

      if (!applyMove(url.pathname, body)) return false;

      await route.fulfill({ body: "", status: 200 });

      return true;
    }

    const listing = listingFor(url);

    if (listing === null) return false;

    await route.fulfill({ json: listing });

    return true;
  };

  await fixture(page, seed(), {}, undefined, handler);
  await page.goto("/dashboard");
  await expect(page.locator(`[data-dir-id="${PHOTOS}"]`)).toBeVisible();

  return { moves, tree };
};

const center = async (locator: Locator) => {
  const box = await locator.boundingBox();

  if (!box) throw new Error("element is not visible");

  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
};

const pickUp = async (page: Page, locator: Locator) => {
  const start = await center(locator);

  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(start.x + 12, start.y + 12, { steps: 3 });

  return start;
};

const dragTo = async (page: Page, locator: Locator) => {
  const end = await center(locator);

  await page.mouse.move(end.x, end.y, { steps: 8 });

  return end;
};

const expand = async (button: Locator, items: Locator, total: number) => {
  for (let loaded = await items.count(); loaded < total; loaded = await items.count()) {
    await button.click();
    await expect(items).not.toHaveCount(loaded);
  }
};

const loadEverything = async (page: Page) => {
  await expand(
    page.getByRole("button", { name: "Show more folders" }),
    page.locator("[data-dir-id]"),
    ROOT_FOLDERS,
  );
  await expand(
    page.getByRole("button", { name: "Show more files" }),
    page.locator("[data-file-id]"),
    ROOT_FILES,
  );
};

test.describe("explorer drag to move", () => {
  test.use({ viewport: { height: 1800, width: 1600 } });

  test("marquee selects many items without long frames", async ({ page, browserName }) => {
    test.setTimeout(90_000);
    await explorerFixture(page);
    await loadEverything(page);

    const listing = page.locator("[data-drop-kind='background']");
    const box = await listing.boundingBox();

    if (!box) throw new Error("listing is not visible");

    if (browserName === "chromium") {
      await page.evaluate(() => {
        const store = window as unknown as { longTasks: number[] };

        store.longTasks = [];
        new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) store.longTasks.push(entry.duration);
        }).observe({ type: "longtask" });
      });
    }

    await page.mouse.move(box.x + 6, box.y + 6);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width - 20, box.y + box.height - 10, { steps: 25 });
    // Holding near the bottom edge scrolls the listing and extends the rectangle.
    await page.waitForTimeout(600);
    await page.mouse.up();

    const status = page.getByText(/\d+ selected/u);

    await expect(status).toBeVisible();

    const selected = Number((await status.textContent())?.match(/\d+/u)?.[0] ?? 0);

    expect(selected).toBeGreaterThan(20);
    expect(await listing.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);

    if (browserName === "chromium") {
      const longTasks = await page.evaluate(
        () => (window as unknown as { longTasks: number[] }).longTasks,
      );

      expect(longTasks.filter((duration) => duration > 100)).toEqual([]);
    }

    // The click that ends a marquee must not clear the fresh selection.
    await expect(status).toBeVisible();
  });

  test("drops a file into a folder and undoes it", async ({ page }) => {
    const { moves } = await explorerFixture(page);
    const file = page.locator(`[data-file-id="${fileId(0)}"]`);
    const photos = page.locator(`[data-dir-id="${PHOTOS}"]`);

    await pickUp(page, file);
    await expect(page.getByText("Move 1 item")).toBeVisible();
    await dragTo(page, photos);
    await expect(page.getByText("Move to Photos").first()).toBeVisible();
    await expect(photos).toHaveAttribute("data-drop-active", "valid");
    await page.mouse.up();

    await expect(file).toHaveCount(0);
    expect(moves).toEqual([{ destinationId: PHOTOS, ids: [fileId(0)], kind: "files" }]);

    const toast = page.getByText('Moved "file-000.txt" to Photos');

    await expect(toast).toBeVisible();
    await page.getByRole("button", { name: "Undo" }).click();

    await expect.poll(() => moves.length).toBe(2);
    expect(moves[1]).toEqual({ destinationId: null, ids: [fileId(0)], kind: "files" });
    await expect(page.locator(`[data-file-id="${fileId(0)}"]`)).toBeVisible();
  });

  test("spring-loaded preview drills into subfolders and drops there", async ({ page }) => {
    const { moves } = await explorerFixture(page);
    const file = page.locator(`[data-file-id="${fileId(1)}"]`);
    const photos = page.locator(`[data-dir-id="${PHOTOS}"]`);

    await pickUp(page, file);
    await dragTo(page, photos);
    await page.waitForTimeout(750);

    const panel = page.locator("[data-drop-panel]");

    await expect(panel).toBeVisible();
    await expect(panel.getByText("cover.jpg")).toBeVisible();

    const yearRow = panel.locator(`[data-drop-dir-id="${YEAR}"]`);

    await dragTo(page, yearRow);
    await expect(yearRow).toHaveAttribute("data-drop-active", "valid");
    await page.waitForTimeout(750);

    await expect(
      panel.locator("[data-drop-kind='preview-trail']", { hasText: "2024" }),
    ).toBeVisible();
    await expect(panel.locator(`[data-drop-dir-id="${SUMMER}"]`)).toBeVisible();

    // Release on the panel background: move into the folder it shows.
    const panelBox = await panel.boundingBox();

    if (!panelBox) throw new Error("panel is not visible");

    await page.mouse.move(panelBox.x + panelBox.width / 2, panelBox.y + panelBox.height - 12, {
      steps: 4,
    });
    await page.mouse.up();

    await expect.poll(() => moves.length).toBe(1);
    expect(moves[0]).toEqual({ destinationId: YEAR, ids: [fileId(1)], kind: "files" });
    await expect(panel).toHaveCount(0);
  });

  test("moves a mixed selection up through the breadcrumb", async ({ page }) => {
    const { moves } = await explorerFixture(page);

    await page.locator(`[data-dir-id="${PHOTOS}"] button`).dblclick();
    await expect(page.locator(`[data-dir-id="${YEAR}"]`)).toBeVisible();

    const year = page.locator(`[data-dir-id="${YEAR}"] button`);
    const cover = page.locator(`[data-file-id="${fileId(900)}"] button`);

    await year.click();
    await cover.click({ modifiers: ["ControlOrMeta"] });
    await expect(page.getByText("2 selected")).toBeVisible();

    await pickUp(page, cover);
    await expect(page.getByText("Move 2 items")).toBeVisible();

    const home = page.locator("[data-drop-kind='breadcrumb'][data-drop-dir-id='root']").last();

    await dragTo(page, home);
    await expect(home).toHaveAttribute("data-drop-active", "valid");
    await page.mouse.up();

    await expect.poll(() => moves.length).toBe(2);
    expect(moves).toContainEqual({ destinationId: null, ids: [fileId(900)], kind: "files" });
    expect(moves).toContainEqual({ destinationId: null, ids: [YEAR], kind: "directories" });
  });

  test("refuses invalid targets and cancels on Escape", async ({ page }) => {
    const { moves } = await explorerFixture(page);
    const photos = page.locator(`[data-dir-id="${PHOTOS}"]`);

    await pickUp(page, photos);
    await page.mouse.move(...(Object.values(await center(photos)) as [number, number]), {
      steps: 4,
    });
    await expect(page.getByText("Can't move into itself")).toBeVisible();
    await page.mouse.up();

    const file = page.locator(`[data-file-id="${fileId(2)}"]`);
    const folder = page.locator(`[data-dir-id="${folderId(1)}"]`);

    await pickUp(page, file);
    await dragTo(page, folder);
    await page.keyboard.press("Escape");
    await page.mouse.up();

    await page.waitForTimeout(200);
    expect(moves).toEqual([]);
    await expect(file).toBeVisible();
  });
});

test.describe("explorer drag across tabs", () => {
  test.use({ viewport: { height: 1800, width: 1600 } });

  test("holding over another tab switches to it and drops into its folder", async ({ page }) => {
    const { moves } = await explorerFixture(page);

    await page.getByRole("button", { name: "New tab" }).click();
    await page.locator(`[data-dir-id="${PHOTOS}"] button`).dblclick();
    await expect(page.locator(`[data-dir-id="${YEAR}"]`)).toBeVisible();

    const tabs = page.locator("[data-explorer-tab-strip] [data-tab-id]");

    await expect(tabs).toHaveCount(2);
    await tabs.first().getByRole("tab").click();

    const file = page.locator(`[data-file-id="${fileId(3)}"]`);

    await expect(file).toBeVisible();
    await pickUp(page, file);
    await dragTo(page, tabs.nth(1));
    await expect(page.getByText("Hold to open").first()).toBeVisible();
    await page.waitForTimeout(750);

    // The drag survives the remount and targets the newly shown listing.
    await expect(page.locator(`[data-dir-id="${YEAR}"]`)).toBeVisible();

    const listing = page.locator("[data-drop-kind='background']");
    const box = await listing.boundingBox();

    if (!box) throw new Error("listing is not visible");

    await page.mouse.move(box.x + box.width - 40, box.y + box.height - 40, { steps: 6 });
    await expect(listing).toHaveAttribute("data-drop-active", "valid");
    await page.mouse.up();

    await expect.poll(() => moves.length).toBe(1);
    expect(moves[0]).toEqual({ destinationId: PHOTOS, ids: [fileId(3)], kind: "files" });
  });
});

test.describe("explorer touch drag", () => {
  test.use({ hasTouch: true, viewport: { height: 1800, width: 1600 } });

  test("a long press lifts an item and a short swipe does not", async ({ page, browserName }) => {
    test.skip(browserName !== "chromium", "touch is driven through the Chromium DevTools protocol");

    const { moves } = await explorerFixture(page);
    const client = await page.context().newCDPSession(page);
    const file = await center(page.locator(`[data-file-id="${fileId(4)}"]`));

    const touch = (type: "touchStart" | "touchMove" | "touchEnd", x: number, y: number) =>
      client.send("Input.dispatchTouchEvent", {
        touchPoints: type === "touchEnd" ? [] : [{ x, y }],
        type,
      });

    const glide = async (from: { x: number; y: number }, to: { x: number; y: number }) => {
      for (let step = 1; step <= 10; step++) {
        await touch(
          "touchMove",
          from.x + ((to.x - from.x) * step) / 10,
          from.y + ((to.y - from.y) * step) / 10,
        );
      }
    };

    // A quick swipe scrolls instead of picking anything up.
    await touch("touchStart", file.x, file.y);
    await glide(file, { x: file.x, y: file.y - 200 });
    await touch("touchEnd", 0, 0);
    await expect(page.locator("[data-explorer-drag]")).toHaveCount(0);

    const listing = page.locator("[data-drop-kind='background']");

    await expect.poll(() => listing.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
    await listing.evaluate((element) => {
      element.scrollTop = 0;
    });

    const lifted = await center(page.locator(`[data-file-id="${fileId(4)}"]`));
    const target = await center(page.locator(`[data-dir-id="${PHOTOS}"]`));

    await touch("touchStart", lifted.x, lifted.y);
    await page.waitForTimeout(480);
    await glide(lifted, target);
    await expect(page.locator("html[data-explorer-drag]")).toHaveCount(1);
    await touch("touchEnd", 0, 0);

    await expect.poll(() => moves.length).toBe(1);
    expect(moves[0]).toEqual({ destinationId: PHOTOS, ids: [fileId(4)], kind: "files" });
  });
});
