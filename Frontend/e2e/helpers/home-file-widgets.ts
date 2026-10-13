import type { Page, Route } from "@playwright/test";

import { fixture, seed } from "./home-dashboard.js";

export const widgetDocument = (width = 6, height = 4) => {
  const document = seed();
  const options = document.desktop.widgets[0]!.options;
  const perRow = Math.floor(12 / width);

  document.desktop.widgets = Array.from({ length: 10 }, (_, index) => ({
    instanceId: `${index < 5 ? "storage" : "recent"}-${index % 5}`,
    type: index < 5 ? 2 : 3,
    size: 1,
    x: (index % perRow) * width,
    y: Math.floor(index / perRow) * height,
    width,
    height,
    showTitle: null,
    options: {
      ...options,
      storageStyle: index % 5,
      recentFilesStyle: index % 5,
      recentFilesSource: 0,
    },
  }));

  return document;
};

export const sampleFiles = () =>
  Array.from({ length: 24 }, (_, index) => ({
    fileId: `file-${index}`,
    fileName: ["Mountain.jpg", "Quarterly report.pdf", "Evening walk.mp4", "Archive.zip"][
      index % 4
    ]!.replace(".", `-${index}.`),
    mimeType: ["image/jpeg", "application/pdf", "video/mp4", "application/zip"][index % 4]!,
    directoryId: "folder-1",
    createdAt: new Date(Date.UTC(2026, 9, 8, 12, 0) - index * 3_600_000).toISOString(),
    updatedAt:
      index % 3 === 0
        ? null
        : new Date(Date.UTC(2026, 9, 8, 11, 0) - index * 1_800_000).toISOString(),
    deletedAt: null,
    currentVersion: {
      id: `version-${index}`,
      size: 1048576 * (index + 1),
      mimeType: ["image/jpeg", "application/pdf", "video/mp4", "application/zip"][index % 4]!,
      versionNumber: 1,
      isDeleted: false,
      isEncrypted: index === 4,
    },
    tags: [],
    owner: { id: "home-fixture", name: "Alex", email: "alex@example.test" },
  }));

export const sampleStorage = () => ({
  sizeByType: {
    "audio/mpeg": 28e9,
    "video/mp4": 32e9,
    "image/jpeg": 8e9,
    "application/pdf": 8e9,
    "application/zip": 4e9,
  },
  filesSize: 80e9,
  previewsSize: 3e9,
  previewsCount: 24,
  transcodedSize: 9e9,
  representationsCount: 4,
  usedBytes: 92e9,
  quotaBytes: 150e9,
  trashSize: 5e9,
  oldFiles: [{ id: "old-1", fileName: "Old.pdf", mimeType: "application/pdf" }],
});

export const fileWidgetFixture = async (
  page: Page,
  document = widgetDocument(),
  appearance: Record<string, unknown> = {},
  flags: {
    files?: ReturnType<typeof sampleFiles>;
    empty?: boolean;
    unlimited?: boolean;
    quotaBytes?: number;
    failStorage?: boolean;
    missingThumbnail?: boolean;
    waitStorage?: Promise<void>;
  } = {},
) => {
  const requests = {
    storage: 0,
    storageSummaries: 0,
    searches: [] as URL[],
    thumbnails: 0,
    directories: 0,
  };
  const files = flags.files ?? sampleFiles();

  const storageResponse = async (route: Route) => {
    requests.storage += 1;
    if (flags.waitStorage) await flags.waitStorage;

    if (flags.failStorage) {
      await route.fulfill({ status: 503, json: { message: "Storage unavailable" } });
    } else {
      const value = sampleStorage();
      if (flags.unlimited) value.quotaBytes = 0;
      if (flags.quotaBytes !== undefined) value.quotaBytes = flags.quotaBytes;
      if (flags.empty)
        Object.assign(value, {
          sizeByType: {},
          filesSize: 0,
          previewsSize: 0,
          transcodedSize: 0,
          usedBytes: 0,
          trashSize: 0,
          oldFiles: [],
        });

      await route.fulfill({ json: value });
    }
  };

  const detailsResponse = async (route: Route, url: URL) => {
    let body: unknown = null;

    if (url.pathname === "/api/files/versions") {
      const file = files.find((entry) => entry.fileId === url.searchParams.get("fileId"));
      body = {
        items: file ? [file.currentVersion] : [],
        currentPage: 0,
        pageSize: 10,
        totalCount: file ? 1 : 0,
        totalPages: file ? 1 : 0,
      };
    } else if (url.pathname.endsWith("/tags")) {
      body = { tags: [] };
    } else if (url.pathname.endsWith("/preview")) {
      body = { previewUrl: null, textPreview: null, archivePreview: null };
    } else if (url.pathname === "/api/tags/search" || url.pathname === "/api/directories/search") {
      body = { items: [], currentPage: 0, pageSize: 10, totalCount: 0, totalPages: 0 };
    } else {
      return false;
    }

    await route.fulfill({ json: body });

    return true;
  };

  const handler = async (route: Route): Promise<boolean> => {
    const url = new URL(route.request().url());

    if (url.pathname === "/api/storage/my-storage") {
      if (url.searchParams.get("summary") === "true") requests.storageSummaries += 1;
      await storageResponse(route);

      return true;
    }

    if (url.pathname === "/api/files/search") {
      requests.searches.push(url);
      const updated = url.searchParams.get("sortBy") === "2";
      let items = files;
      if (updated)
        items = items
          .filter((file) => file.updatedAt !== null)
          .sort((first, second) => second.updatedAt!.localeCompare(first.updatedAt!));
      if (flags.empty) items = [];

      await route.fulfill({
        json: {
          items,
          totalCount: items.length,
          currentPage: 0,
          pageSize: 40,
          totalPages: items.length ? 1 : 0,
        },
      });

      return true;
    }

    if (url.pathname.includes("/thumbnail")) {
      requests.thumbnails += 1;
      if (flags.missingThumbnail) await route.fulfill({ status: 404 });
      else
        await route.fulfill({
          contentType: "image/svg+xml",
          body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 360 240"><rect width="360" height="240" fill="#577F78"/><path d="M0 240L130 60L250 190L330 100L360 240" fill="#A9C6AA"/><circle cx="280" cy="50" r="25" fill="#EDDDA0"/></svg>',
        });

      return true;
    }

    const file = files.find((entry) => url.pathname === `/api/files/${entry.fileId}`);
    if (file) {
      await route.fulfill({ json: file });

      return true;
    }

    if (url.pathname.startsWith("/api/directories")) requests.directories += 1;

    return detailsResponse(route, url);
  };

  const state = await fixture(page, document, appearance, undefined, handler);

  return { state, requests };
};
