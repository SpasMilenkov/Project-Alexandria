import { CalendarDate } from "@internationalized/date";
import { describe, expect, it } from "vitest";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import {
  type SearchFilter,
  type SearchFilterContext,
  buildSearchRequests,
  findSearchTrigger,
  insertSearchKey,
  parseSizeQuery,
  removeSearchFilter,
  resolveSearchScope,
  searchFilterOptions,
  searchNameText,
  stripSearchTrigger,
  suggestSearchKeys,
  upsertSearchFilter,
} from "@/features/explorer/search-filters";

const today = new CalendarDate(2026, 11, 15);

const context = (filters: SearchFilter[] = [], extra: Partial<SearchFilterContext> = {}) => ({
  filters,
  today,
  ...extra,
});

const keysOf = (filters: SearchFilter[], text = "", showAll = true) =>
  suggestSearchKeys(text, context(filters), showAll).map((def) => def.key);

describe("findSearchTrigger", () => {
  it("opens a picker for a known key at the end of the input", () => {
    expect(findSearchTrigger("paris in:Pho")).toEqual({ key: "in", query: "Pho", start: 6 });
  });

  it("keeps spaces in the value so folder names can have them", () => {
    expect(findSearchTrigger("in:My Photos")?.query).toBe("My Photos");
  });

  it("uses the last key when several are typed", () => {
    expect(findSearchTrigger("type:files in:docs")?.key).toBe("in");
  });

  it("ignores words with a colon that are not filter keys", () => {
    expect(findSearchTrigger("notes:draft")).toBeNull();
  });

  it("ignores a key glued to the end of another word", () => {
    expect(findSearchTrigger("photosin:")).toBeNull();
  });
});

describe("search text helpers", () => {
  it("leaves the unfinished filter out of the name search", () => {
    expect(searchNameText("paris uploaded:last")).toBe("paris");
  });

  it("strips the trigger and keeps a space for further typing", () => {
    const text = "paris in:Pho";
    const trigger = findSearchTrigger(text);

    expect(trigger).not.toBeNull();
    expect(stripSearchTrigger(text, trigger!)).toBe("paris ");
  });

  it("replaces the partly typed word with the chosen key", () => {
    expect(insertSearchKey("paris up", "uploaded")).toBe("paris uploaded:");
    expect(insertSearchKey("", "in")).toBe("in:");
  });
});

describe("suggestSearchKeys", () => {
  it("offers every key except deleted: when the list is asked for", () => {
    expect(keysOf([])).toEqual(["in", "type", "tag", "uploaded", "modified", "size", "is", "has"]);
  });

  it("does not open a list on an empty input by itself", () => {
    expect(keysOf([], "", false)).toEqual([]);
  });

  it("matches keys by the start of the last word", () => {
    expect(keysOf([], "paris u")).toEqual(["uploaded"]);
  });

  it("stays quiet after a space unless the list is asked for", () => {
    expect(keysOf([], "paris ", false)).toEqual([]);
    expect(keysOf([], "paris ").length).toBeGreaterThan(0);
  });

  it("offers deleted: once deleted items are shown", () => {
    expect(keysOf([{ key: "is", value: "deleted" }])).toContain("deleted");
  });

  it("hides single-value keys that already have a chip", () => {
    expect(keysOf([{ key: "type", value: "files" }])).not.toContain("type");
  });

  it("hides file-only keys once the search is limited to folders", () => {
    const keys = keysOf([{ key: "type", value: "folders" }]);

    expect(keys).not.toContain("tag");
    expect(keys).not.toContain("size");
  });

  it("keeps offering tag: after a tag chip so several can be combined", () => {
    expect(keysOf([{ key: "tag", name: "travel", tagId: "t1" }])).toContain("tag");
  });

  it("hides folder-only keys once the search is limited to files", () => {
    const keys = keysOf([{ key: "type", value: "files" }]);

    expect(keys).not.toContain("has");
    expect(keys).toContain("size");
  });
});

describe("searchFilterOptions", () => {
  it("still offers the other type when replacing the type chip", () => {
    const options = searchFilterOptions("type", "", context([{ key: "type", value: "files" }]));

    expect(options.map((option) => option.id)).toEqual(["files", "folders"]);
  });

  it("drops is: values already applied", () => {
    const options = searchFilterOptions("is", "", context([{ key: "is", value: "deleted" }]));

    expect(options.map((option) => option.id)).toEqual(["starred"]);
  });

  it("offers is:note once a Notes folder exists", () => {
    const without = searchFilterOptions("is", "", context());
    const withNotes = searchFilterOptions("is", "", context([], { notesFolderId: "notes" }));

    expect(without.map((option) => option.id)).toEqual(["deleted", "starred"]);
    expect(withNotes.map((option) => option.id)).toEqual(["deleted", "starred", "note"]);
  });

  it("hides is:note once the search is limited to folders", () => {
    const options = searchFilterOptions(
      "is",
      "",
      context([{ key: "type", value: "folders" }], { notesFolderId: "notes" }),
    );

    expect(options.map((option) => option.id)).not.toContain("note");
  });

  it("puts the current folder first and skips it in the search results", () => {
    const options = searchFilterOptions(
      "in",
      "",
      context([], {
        currentDirectory: { id: "a", name: "Photos" },
        folders: [
          { id: "a", name: "Photos" },
          { id: "b", name: "Docs" },
        ],
      }),
    );

    expect(options.map((option) => option.label)).toEqual(["This folder (Photos)", "Docs"]);
  });

  it("lists searched tags and skips ones already applied", () => {
    const options = searchFilterOptions(
      "tag",
      "tr",
      context([{ key: "tag", name: "travel", tagId: "t1" }], {
        tags: [
          { id: "t1", name: "travel" },
          { id: "t2", name: "trains" },
        ],
      }),
    );

    expect(options.map((option) => option.label)).toEqual(["trains"]);
  });

  it("builds date presets from today", () => {
    const [todayOption, week] = searchFilterOptions("uploaded", "", context());

    expect(todayOption.filter).toMatchObject({ range: { from: today, to: today } });
    expect(week.filter).toMatchObject({
      label: "last 7 days",
      range: { from: new CalendarDate(2026, 11, 9), to: today },
    });
  });

  it("turns a typed size into a single option", () => {
    const options = searchFilterOptions("size", ">10mb", context());

    expect(options).toHaveLength(1);
    expect(options[0].label).toBe("Larger than 10 MB");
  });
});

describe("parseSizeQuery", () => {
  it("reads lower bounds, upper bounds and ranges in decimal units", () => {
    expect(parseSizeQuery(">10mb")).toEqual({ max: null, min: 10_000_000 });
    expect(parseSizeQuery("<1.5gb")).toEqual({ max: 1_500_000_000, min: null });
    expect(parseSizeQuery("10mb-1gb")).toEqual({ max: 1_000_000_000, min: 10_000_000 });
  });

  it("treats a bare number as megabytes", () => {
    expect(parseSizeQuery(">5")).toEqual({ max: null, min: 5_000_000 });
  });

  it("rejects unreadable or inverted sizes", () => {
    expect(parseSizeQuery("big")).toBeNull();
    expect(parseSizeQuery("10")).toBeNull();
    expect(parseSizeQuery("1gb-10mb")).toBeNull();
  });
});

describe("filter list changes", () => {
  it("replaces a single-value chip and adds a second is: chip", () => {
    const start: SearchFilter[] = [{ key: "type", value: "files" }];
    const replaced = upsertSearchFilter(start, { key: "type", value: "folders" });
    const both = upsertSearchFilter(upsertSearchFilter(replaced, { key: "is", value: "deleted" }), {
      key: "is",
      value: "starred",
    });

    expect(replaced).toEqual([{ key: "type", value: "folders" }]);
    expect(both).toHaveLength(3);
  });

  it("drops the deleted date range together with is:deleted", () => {
    const filters: SearchFilter[] = [
      { key: "is", value: "deleted" },
      { key: "deleted", label: "today", range: { from: today, to: today } },
    ];

    expect(removeSearchFilter(filters, filters[0])).toEqual([]);
  });
});

describe("resolveSearchScope", () => {
  it("narrows to one side and reports filters that cannot combine", () => {
    expect(resolveSearchScope([])).toBe("both");
    expect(resolveSearchScope([{ key: "size", max: null, min: 1 }])).toBe("files");
    expect(
      resolveSearchScope([
        { key: "size", max: null, min: 1 },
        { key: "has", value: "files" },
      ]),
    ).toBe("none");
  });
});

describe("buildSearchRequests", () => {
  it("sends nothing without a name or filter", () => {
    expect(buildSearchRequests("", [])).toEqual({ directories: null, files: null });
  });

  it("maps filters onto both queries and sorts filter-only searches newest first", () => {
    const requests = buildSearchRequests("", [
      { directoryId: "11111111-1111-4111-8111-111111111111", key: "in", name: "Photos" },
      {
        key: "uploaded",
        label: "this year",
        range: { from: new CalendarDate(2026, 1, 1), to: today },
      },
      { key: "is", value: "deleted" },
    ]);

    expect(requests.files).toMatchObject({
      createdAfter: "2026-01-01",
      createdBefore: "2026-11-15",
      isDeleted: true,
      parentDirectoryId: "11111111-1111-4111-8111-111111111111",
      sortBy: SortBy.UpdatedAt,
      sortDirection: SortDirection.Desc,
    });
    expect(requests.directories).toMatchObject({ isDeleted: true });
  });

  it("searches files anywhere below the Notes folder for is:note", () => {
    const notesFolderId = "22222222-2222-4222-8222-222222222222";
    const requests = buildSearchRequests("trip", [{ key: "is", notesFolderId, value: "note" }]);

    expect(requests.directories).toBeNull();
    expect(requests.files).toMatchObject({ nameContains: "trip", withinDirectoryId: notesFolderId });
    expect(requests.files?.parentDirectoryId).toBeUndefined();
  });

  it("only queries files when a file-only filter is set", () => {
    const requests = buildSearchRequests("report", [{ key: "size", max: null, min: 10 }]);

    expect(requests.directories).toBeNull();
    expect(requests.files).toMatchObject({ minSize: 10, nameContains: "report" });
  });

  it("sends every tag chip and limits the search to files", () => {
    const requests = buildSearchRequests("", [
      { key: "tag", name: "travel", tagId: "11111111-1111-4111-8111-111111111111" },
      // Any Guid the backend accepts must pass, even outside the RFC variant.
      { key: "tag", name: "family", tagId: "00000000-0000-0000-0000-000000000001" },
    ]);

    expect(requests.directories).toBeNull();
    expect(requests.files?.tagIds).toEqual([
      "11111111-1111-4111-8111-111111111111",
      "00000000-0000-0000-0000-000000000001",
    ]);
  });

  it("only queries folders for starred items", () => {
    const requests = buildSearchRequests("", [{ key: "is", value: "starred" }]);

    expect(requests.files).toBeNull();
    expect(requests.directories).toMatchObject({ isStarred: true });
  });
});
