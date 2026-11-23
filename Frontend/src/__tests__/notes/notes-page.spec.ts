import { describe, expect, it } from "vitest";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import {
  NOTES_PAGE_SIZE,
  noteEditGroup,
  notesListQuery,
  notesTrail,
} from "@/features/notes/notes-page";

const FOLDER = "3f2a9c1e-7b4d-4e8a-9c2f-1a2b3c4d5e6f";

// Local times, so the day arithmetic matches whatever zone the suite runs in.
const NOW = new Date(2026, 10, 22, 15, 30);
const daysBefore = (days: number) => new Date(2026, 10, 22 - days, 9, 0);

describe("noteEditGroup", () => {
  it("groups the past week by day, then the past week and month as runs", () => {
    expect(noteEditGroup(daysBefore(0), NOW)).toEqual({ key: "today", label: "Today" });
    expect(noteEditGroup(daysBefore(1), NOW)).toEqual({ key: "yesterday", label: "Yesterday" });
    expect(noteEditGroup(daysBefore(2), NOW).key).toBe("week");
    expect(noteEditGroup(daysBefore(6), NOW).key).toBe("week");
    expect(noteEditGroup(daysBefore(7), NOW).key).toBe("month");
    expect(noteEditGroup(daysBefore(29), NOW).key).toBe("month");
  });

  it("names older months, with the year once it is a past one", () => {
    expect(noteEditGroup(new Date(2026, 2, 4), NOW)).toEqual({ key: "2026-3", label: "March" });
    expect(noteEditGroup(new Date(2025, 11, 31), NOW)).toEqual({
      key: "2025-12",
      label: "December 2025",
    });
  });

  it("reads an edit stamped slightly in the future as today", () => {
    expect(noteEditGroup(new Date(2026, 10, 23, 0, 5), NOW).key).toBe("today");
  });

  it("puts an unreadable date in its own group", () => {
    expect(noteEditGroup("not a date", NOW).key).toBe("undated");
  });
});

describe("notesTrail", () => {
  const NOTES = { id: "notes", name: "Notes" };
  const PROJECTS = { id: "projects", name: "Projects" };

  it("starts the breadcrumb at the Notes folder", () => {
    expect(notesTrail([NOTES, PROJECTS], "notes")).toEqual([NOTES, PROJECTS]);
  });

  it("is empty for a path outside Notes", () => {
    expect(notesTrail([PROJECTS], "notes")).toEqual([]);
    expect(notesTrail([NOTES], undefined)).toEqual([]);
  });
});

describe("notesListQuery", () => {
  it("lists markdown in the folder and below, newest edit first", () => {
    expect(notesListQuery({ folderId: FOLDER, page: 2, search: "" })).toMatchObject({
      currentPage: 2,
      mimeType: "text/markdown",
      nameContains: undefined,
      pageSize: NOTES_PAGE_SIZE,
      sortBy: SortBy.UpdatedAt,
      sortDirection: SortDirection.Desc,
      withinDirectoryId: FOLDER,
    });
  });

  it("searches with the trimmed text, and not at all for blanks", () => {
    expect(notesListQuery({ folderId: FOLDER, page: 0, search: "  paris  " }).nameContains).toBe(
      "paris",
    );
    expect(
      notesListQuery({ folderId: FOLDER, page: 0, search: "   " }).nameContains,
    ).toBeUndefined();
  });
});
