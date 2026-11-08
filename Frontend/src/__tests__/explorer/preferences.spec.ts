import { beforeEach, describe, expect, it } from "vitest";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import {
  EXPLORER_PREFERENCES_KEY,
  loadExplorerPreferences,
  parseExplorerPreferences,
} from "@/features/explorer/preferences";

describe("parseExplorerPreferences", () => {
  it("uses defaults and the given view mode when nothing is stored", () => {
    expect(parseExplorerPreferences(null, "list")).toEqual({
      pageSize: 25,
      sortBy: SortBy.Name,
      sortDirection: SortDirection.Asc,
      viewMode: "list",
    });
  });

  it("keeps every valid stored field", () => {
    const stored = {
      pageSize: 50,
      sortBy: SortBy.CreatedAt,
      sortDirection: SortDirection.Desc,
      viewMode: "grid",
    };

    expect(parseExplorerPreferences(stored, "list")).toEqual(stored);
  });

  it("replaces only the invalid fields", () => {
    const parsed = parseExplorerPreferences(
      { pageSize: 1000, sortBy: SortBy.TotalSize, sortDirection: SortDirection.Desc, viewMode: 7 },
      "grid",
    );

    expect(parsed).toEqual({
      pageSize: 25,
      sortBy: SortBy.Name,
      sortDirection: SortDirection.Desc,
      viewMode: "grid",
    });
  });
});

describe("loadExplorerPreferences", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("falls back to defaults when the stored value is not valid JSON", () => {
    window.localStorage.setItem(EXPLORER_PREFERENCES_KEY, "{not json");

    expect(loadExplorerPreferences("grid").pageSize).toBe(25);
  });
});
