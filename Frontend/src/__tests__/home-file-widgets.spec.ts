import { describe, expect, it } from "vitest";

import type { StorageBreakdown } from "@/api/status";

import { isAuthError } from "@/api/shuffle";
import { HomeRecentFilesSource } from "@/enums/home-widget";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import {
  homeRecentFilesSearch,
  homeStorageCells,
  homeStorageModel,
} from "@/utils/home-file-widgets";

const storage = (patch: Partial<StorageBreakdown> = {}): StorageBreakdown => ({
  sizeByType: { "audio/mpeg": 30, "image/png": 10, "application/pdf": 10, "unknown/type": 10 },
  filesSize: 60,
  previewsSize: 10,
  previewsCount: 2,
  transcodedSize: 10,
  representationsCount: 1,
  usedBytes: 80,
  quotaBytes: 100,
  trashSize: 50,
  oldFiles: [],
  ...patch,
});

describe("Home file widget data", () => {
  it.each([
    [{ response: { status: 401 } }, true],
    [{ response: { status: 403 }, message: "Request failed with status code 403" }, true],
    [{ response: { status: 500 }, message: "Could not read 401.txt" }, false],
    [{ message: "Unauthorized filename" }, false],
    [null, false],
  ])("classifies transport auth errors by response status (%j)", (error, expected) => {
    expect(isAuthError(error)).toBe(expected);
  });

  it("accounts for derived media in quota charts and keeps trash separate", () => {
    const model = homeStorageModel(storage());

    expect(model.categories.reduce((total, category) => total + category.bytes, 0)).toBe(80);
    expect(model.categories.map((category) => category.label)).toEqual([
      "Audio",
      "Images",
      "Documents",
      "Other",
      "Previews",
      "Transcodes",
    ]);
    expect(model.percent).toBe(80);
    expect(model.categories.reduce((total, category) => total + category.share, 0)).toBe(80);
    expect(homeStorageCells(model.categories).filter(Boolean)).toHaveLength(80);
  });

  it("handles empty unlimited accounts without division by zero or fake percentages", () => {
    const model = homeStorageModel(
      storage({
        sizeByType: {},
        filesSize: 0,
        usedBytes: 0,
        previewsSize: 0,
        transcodedSize: 0,
        quotaBytes: 0,
      }),
    );

    expect(model.percent).toBeNull();
    expect(model.fill).toBe(0);
    expect(model.remaining).toBe("No storage limit");
    expect(model.categories).toEqual([]);
    expect(homeStorageCells(model.categories)).toEqual(Array<null>(100).fill(null));
  });

  it("represents unlimited usage as a type composition", () => {
    const model = homeStorageModel(storage({ quotaBytes: 0 }));

    expect(model.percent).toBeNull();
    expect(model.categories.reduce((total, category) => total + category.share, 0)).toBe(100);
    expect(homeStorageCells(model.categories).filter(Boolean)).toHaveLength(100);
  });

  it("allocates fractional waffle cells to the rounded total instead of rounding every category up", () => {
    const categories = homeStorageModel(storage())
      .categories.slice(0, 3)
      .map((category) => ({ ...category, share: 0.6 }));

    expect(homeStorageCells(categories).filter(Boolean)).toHaveLength(2);
    expect(homeStorageCells(categories)).toHaveLength(100);
  });

  it("keeps quota graphics bounded for development data", () => {
    const model = homeStorageModel(storage({ quotaBytes: 40 }));

    expect(model.fill).toBe(100);
    expect(homeStorageCells(model.categories)).toHaveLength(100);
  });

  it("requests created files newest first without mixing the updated feed", () => {
    const query = homeRecentFilesSearch(HomeRecentFilesSource.Created);

    expect(query.sortBy).toBe(SortBy.CreatedAt);
    expect(query.sortDirection).toBe(SortDirection.Desc);
    expect(query.updatedAfter).toBeNull();
    expect(query.currentPage).toBe(0);
    expect(query.pageSize).toBeLessThanOrEqual(100);
    expect(query.isDeleted).toBe(false);
  });

  it("excludes null update dates on the server before paging the updated feed", () => {
    const query = homeRecentFilesSearch(HomeRecentFilesSource.Updated);

    expect(query.sortBy).toBe(SortBy.UpdatedAt);
    expect(query.sortDirection).toBe(SortDirection.Desc);
    expect(query.updatedAfter).toBe("0001-01-01");
    expect(query.nameContains).toBeUndefined();
    expect(query.createdAfter).toBeNull();
  });
});
