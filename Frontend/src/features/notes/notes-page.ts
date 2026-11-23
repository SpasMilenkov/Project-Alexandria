import type { PaginationParams } from "@/types/pagination-params";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { type FileSearchQuery, fileSearchApiSchema } from "@/schemas/search";
import {
  type DateInput,
  calendarDaysBetween,
  formatDaysAgo,
  formatMonth,
  formatMonthYear,
} from "@/utils/date-formatters";

export interface NotesFolderPart {
  id: string;
  name: string;
}

/** A run of notes on the Notes page that were last edited around the same time. */
export interface NoteEditGroup {
  key: string;
  label: string;
}

/** What one "Load more" page of the Notes page reports once it has loaded. */
export interface NotesPageLoaded {
  hasNext: boolean;
  total: number;
  // The group the page ends in, so the next page can carry on under the same heading.
  lastGroupKey: string | null;
}

/** Notes per "Load more"; a page's summaries go in one request. */
export const NOTES_PAGE_SIZE = 30;

// Folders in the tree come in one go; a vault with more than this per level is unusual.
export const FOLDER_PAGE: PaginationParams = {
  page: 1,
  pageSize: 100,
  SortBy: SortBy.Name,
  sortDirection: SortDirection.Asc,
};

const MARKDOWN = "text/markdown";

/**
 * One page of the notes list: markdown in the folder and every folder below it, most recently
 * edited first. A search looks at names and body text, as file search always does. Other files
 * inside Notes (pictures, PDFs) are a vault's attachments, so they stay out.
 */
export const notesListQuery = ({
  folderId,
  search,
  page,
}: {
  folderId: string;
  search: string;
  page: number;
}): FileSearchQuery =>
  fileSearchApiSchema.parse({
    currentPage: page,
    mimeType: MARKDOWN,
    nameContains: search.trim() === "" ? undefined : search.trim(),
    pageSize: NOTES_PAGE_SIZE,
    sortBy: SortBy.UpdatedAt,
    sortDirection: SortDirection.Desc,
    withinDirectoryId: folderId,
  });

const UNDATED: NoteEditGroup = { key: "undated", label: "Undated" };

/**
 * Which heading a note sits under on the Notes page, the way notes apps group a newest-first
 * list: today, yesterday, the past week, the past month, then one group per calendar month,
 * with the year added once it is no longer the current one. Each group is one contiguous run of
 * a newest-first list, so pages can continue a group the previous page started.
 */
export const noteEditGroup = (value: DateInput, now: DateInput): NoteEditGroup => {
  const date = new Date(value);
  const days = calendarDaysBetween(date, now);

  if (!Number.isFinite(days)) return UNDATED;

  // A clock slightly ahead of the viewer's still reads as an edit made today.
  if (days <= 0) return { key: "today", label: formatDaysAgo(0, { capitalize: true }) };
  if (days === 1) return { key: "yesterday", label: formatDaysAgo(1, { capitalize: true }) };
  if (days < 7) return { key: "week", label: "Previous 7 days" };
  if (days < 30) return { key: "month", label: "Previous 30 days" };

  const key = `${date.getFullYear()}-${date.getMonth() + 1}`;

  if (date.getFullYear() === new Date(now).getFullYear()) return { key, label: formatMonth(date) };

  return { key, label: formatMonthYear(date) };
};

/**
 * The part of a folder's path from the Notes folder down, for the page's breadcrumb. Empty when
 * the folder isn't inside Notes, which only happens while the path is still loading.
 */
export const notesTrail = (
  pathParts: readonly NotesFolderPart[],
  notesFolderId: string | undefined,
): NotesFolderPart[] => {
  const start = pathParts.findIndex((part) => part.id === notesFolderId);

  if (start === -1) return [];

  return pathParts.slice(start);
};
