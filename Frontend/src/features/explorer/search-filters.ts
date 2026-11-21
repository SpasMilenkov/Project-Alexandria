import type { z } from "zod";

import { CalendarDate, getLocalTimeZone } from "@internationalized/date";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import {
  type DirectorySearchQuery,
  type FileSearchQuery,
  directorySearchApiSchema,
  fileSearchApiSchema,
  type unifiedSearchUiSchema,
} from "@/schemas/search";
import { formatBytes } from "@/utils/size";

export type DateFilterKey = "deleted" | "modified" | "uploaded";
export type SearchFilterKey = DateFilterKey | "has" | "in" | "is" | "size" | "tag" | "type";
export type SearchScope = "both" | "directories" | "files" | "none";

export interface SearchDateRange {
  from: CalendarDate;
  to: CalendarDate;
}

export type SearchFilter =
  | { directoryId: string; key: "in"; name: string }
  | { key: "type"; value: "files" | "folders" }
  | { key: "tag"; name: string; tagId: string }
  | { key: DateFilterKey; label: string; range: SearchDateRange }
  | { key: "size"; max: number | null; min: number | null }
  | { key: "is"; value: "deleted" | "starred" }
  // Notes are the files anywhere under the Notes folder, so the filter carries which folder that is.
  | { key: "is"; notesFolderId: string; value: "note" }
  | { key: "has"; value: "files" | "subfolders" };

export interface SearchFilterDefinition {
  description: string;
  icon: string;
  key: SearchFilterKey;
}

export interface SearchFilterOption {
  filter: SearchFilter;
  icon: string;
  id: string;
  label: string;
}

export interface SearchTrigger {
  key: SearchFilterKey;
  query: string;
  start: number;
}

export interface SearchFilterContext {
  currentDirectory?: { id: string; name: string } | null;
  filters: SearchFilter[];
  folders?: { id: string; name: string }[];
  // Null until the first note creates the Notes folder; `is:note` isn't offered before that.
  notesFolderId?: string | null;
  tags?: { id: string; name: string }[];
  today: CalendarDate;
}

export interface SearchRequests {
  directories: DirectorySearchQuery | null;
  files: FileSearchQuery | null;
}

type SearchUiInput = z.input<typeof unifiedSearchUiSchema>;

export const searchFilterDefinitions: SearchFilterDefinition[] = [
  { description: "Directly inside a folder", icon: "i-lucide-folder", key: "in" },
  { description: "Only files or only folders", icon: "i-lucide-layers", key: "type" },
  { description: "Files with a tag", icon: "i-lucide-tag", key: "tag" },
  { description: "When it was uploaded", icon: "i-lucide-calendar-plus", key: "uploaded" },
  { description: "When it last changed", icon: "i-lucide-pencil", key: "modified" },
  { description: "File size", icon: "i-lucide-hard-drive", key: "size" },
  { description: "Deleted, starred or notes", icon: "i-lucide-star", key: "is" },
  { description: "Folders with files or subfolders", icon: "i-lucide-folder-tree", key: "has" },
  { description: "When it was deleted", icon: "i-lucide-trash-2", key: "deleted" },
];

const definitionByKey = new Map(searchFilterDefinitions.map((def) => [def.key, def]));

// Keys that can hold several chips, one per value. Every other key holds one
// chip, and picking a new value replaces it.
const multiValueKeys = new Set<SearchFilterKey>(["has", "is", "tag"]);

const keyScopes: Partial<Record<SearchFilterKey, SearchScope>> = {
  has: "directories",
  size: "files",
  tag: "files",
};

const SIZE_UNITS: Record<string, number> = {
  b: 1,
  gb: 1000 ** 3,
  kb: 1000,
  mb: 1000 ** 2,
  tb: 1000 ** 4,
};

const sizePresets: { max: number | null; min: number | null }[] = [
  { max: null, min: 10 * SIZE_UNITS.mb },
  { max: null, min: 100 * SIZE_UNITS.mb },
  { max: null, min: SIZE_UNITS.gb },
  { max: SIZE_UNITS.mb, min: null },
];

export const isSearchFilterKey = (value: string): value is SearchFilterKey =>
  definitionByKey.has(value as SearchFilterKey);

export const isDateFilterKey = (key: SearchFilterKey): key is DateFilterKey =>
  key === "deleted" || key === "modified" || key === "uploaded";

export const isMultiValueSearchKey = (key: SearchFilterKey) => multiValueKeys.has(key);

export const searchFilterIcon = (key: SearchFilterKey) => definitionByKey.get(key)?.icon ?? "";

export const searchFilterIdentity = (filter: SearchFilter): string => {
  if (filter.key === "is" || filter.key === "has") {
    return `${filter.key}:${filter.value}`;
  }
  if (filter.key === "tag") {
    return `tag:${filter.tagId}`;
  }
  return filter.key;
};

const filterScope = (filter: SearchFilter): SearchScope => {
  if (filter.key === "type") {
    return filter.value === "files" ? "files" : "directories";
  }
  if (filter.key === "is" && filter.value === "starred") {
    return "directories";
  }
  if (filter.key === "is" && filter.value === "note") {
    return "files";
  }
  return keyScopes[filter.key] ?? "both";
};

const intersectScopes = (a: SearchScope, b: SearchScope): SearchScope => {
  if (a === b || b === "both") {
    return a;
  }
  if (a === "both") {
    return b;
  }
  return "none";
};

export const resolveSearchScope = (filters: SearchFilter[]): SearchScope =>
  filters.reduce<SearchScope>(
    (scope, filter) => intersectScopes(scope, filterScope(filter)),
    "both",
  );

// Typing `key:` at the end of the input opens that filter's picker. The value
// cannot contain a colon, so the last `key:` always wins and names with
// colons that are not filter keys stay plain text.
export const findSearchTrigger = (text: string): SearchTrigger | null => {
  const match = /(?<lead>^|\s)(?<key>[a-z]+):(?<query>[^:]*)$/iu.exec(text);
  if (!match?.groups) {
    return null;
  }

  const { lead, query } = match.groups;
  const key = match.groups.key.toLowerCase();
  if (!isSearchFilterKey(key)) {
    return null;
  }

  return { key, query, start: match.index + lead.length };
};

export const stripSearchTrigger = (text: string, trigger: SearchTrigger): string => {
  const before = text.slice(0, trigger.start).trimEnd();
  return before ? `${before} ` : "";
};

export const searchNameText = (text: string): string => {
  const trigger = findSearchTrigger(text);
  const nameText = trigger ? text.slice(0, trigger.start) : text;
  return nameText.trim();
};

const lastWord = (text: string) => text.slice(text.search(/\S*$/u));

export const insertSearchKey = (text: string, key: SearchFilterKey): string => {
  const word = lastWord(text);
  return `${text.slice(0, text.length - word.length)}${key}:`;
};

export const upsertSearchFilter = (filters: SearchFilter[], filter: SearchFilter) => {
  const identity = searchFilterIdentity(filter);
  const index = filters.findIndex((existing) => searchFilterIdentity(existing) === identity);
  if (index === -1) {
    return [...filters, filter];
  }
  return filters.map((existing, i) => (i === index ? filter : existing));
};

export const removeSearchFilter = (filters: SearchFilter[], filter: SearchFilter) => {
  const identity = searchFilterIdentity(filter);
  const remaining = filters.filter((existing) => searchFilterIdentity(existing) !== identity);

  // A deleted date range means nothing once deleted items are no longer shown.
  if (identity === "is:deleted") {
    return remaining.filter((existing) => existing.key !== "deleted");
  }
  return remaining;
};

export const searchDatePresets = (today: CalendarDate) => [
  { id: "today", label: "Today", range: { from: today, to: today } },
  { id: "week", label: "Last 7 days", range: { from: today.subtract({ days: 6 }), to: today } },
  { id: "month", label: "Last 30 days", range: { from: today.subtract({ days: 29 }), to: today } },
  {
    id: "year",
    label: "This year",
    range: { from: new CalendarDate(today.year, 1, 1), to: today },
  },
  {
    id: "last-year",
    label: "Last year",
    range: {
      from: new CalendarDate(today.year - 1, 1, 1),
      to: new CalendarDate(today.year - 1, 12, 31),
    },
  },
];

// "3 Nov 2026", or a compact range such as "3 – 10 Nov 2026", in the viewer's locale.
export const formatSearchDateRange = (from: CalendarDate, to: CalendarDate) => {
  const formatter = new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
  const zone = getLocalTimeZone();

  if (from.compare(to) === 0) {
    return formatter.format(from.toDate(zone));
  }
  return formatter.formatRange(from.toDate(zone), to.toDate(zone));
};

export const formatSearchSize = (bytes: number) =>
  formatBytes(bytes, { roundingPrecision: 1 }).replace(".0 ", " ");

const parseSizeAmount = (text: string): number | null => {
  const match = /^(?<amount>\d+(?:\.\d+)?)\s*(?<unit>b|kb|mb|gb|tb)?$/u.exec(text.trim());
  if (!match?.groups) {
    return null;
  }

  // A bare number reads as megabytes, the unit people reach for most.
  const unit = SIZE_UNITS[match.groups.unit ?? "mb"];
  return Math.round(Number(match.groups.amount) * unit);
};

export const parseSizeQuery = (
  query: string,
): { max: number | null; min: number | null } | null => {
  const text = query.trim().toLowerCase();

  if (text.startsWith(">")) {
    const min = parseSizeAmount(text.slice(1));
    return min === null ? null : { max: null, min };
  }

  if (text.startsWith("<")) {
    const max = parseSizeAmount(text.slice(1));
    return max === null ? null : { max, min: null };
  }

  const parts = text.split("-");
  if (parts.length !== 2) {
    return null;
  }

  const min = parseSizeAmount(parts[0]);
  const max = parseSizeAmount(parts[1]);
  if (min === null || max === null || min > max) {
    return null;
  }
  return { max, min };
};

const sizeRangeLabel = (min: number | null, max: number | null) => {
  if (min !== null && max !== null) {
    return `${formatSearchSize(min)} to ${formatSearchSize(max)}`;
  }
  if (min !== null) {
    return `> ${formatSearchSize(min)}`;
  }
  if (max !== null) {
    return `< ${formatSearchSize(max)}`;
  }
  return "any";
};

const sizeOptionLabel = (min: number | null, max: number | null) => {
  if (min !== null && max !== null) {
    return `Between ${formatSearchSize(min)} and ${formatSearchSize(max)}`;
  }
  if (min !== null) {
    return `Larger than ${formatSearchSize(min)}`;
  }
  return `Smaller than ${formatSearchSize(max ?? 0)}`;
};

export const searchFilterValueLabel = (filter: SearchFilter): string => {
  if (filter.key === "in" || filter.key === "tag") {
    return filter.name;
  }
  if (filter.key === "size") {
    return sizeRangeLabel(filter.min, filter.max);
  }
  if (filter.key === "type" || filter.key === "is" || filter.key === "has") {
    return filter.value;
  }
  return filter.label;
};

const matchesQuery = (label: string, query: string) =>
  label.toLowerCase().includes(query.trim().toLowerCase());

const listOptions = (key: SearchFilterKey, query: string, ctx: SearchFilterContext) => {
  if (key === "type") {
    return [
      { filter: { key, value: "files" }, icon: "i-lucide-file", id: "files", label: "Files" },
      {
        filter: { key, value: "folders" },
        icon: "i-lucide-folder",
        id: "folders",
        label: "Folders",
      },
    ] satisfies SearchFilterOption[];
  }

  if (key === "is") {
    const options: SearchFilterOption[] = [
      {
        filter: { key, value: "deleted" },
        icon: "i-lucide-trash-2",
        id: "deleted",
        label: "Deleted",
      },
      { filter: { key, value: "starred" }, icon: "i-lucide-star", id: "starred", label: "Starred" },
    ];

    if (ctx.notesFolderId) {
      options.push({
        filter: { key, notesFolderId: ctx.notesFolderId, value: "note" },
        icon: "i-lucide-notebook-pen",
        id: "note",
        label: "Note",
      });
    }

    return options;
  }

  if (key === "has") {
    return [
      { filter: { key, value: "files" }, icon: "i-lucide-file", id: "files", label: "Files" },
      {
        filter: { key, value: "subfolders" },
        icon: "i-lucide-folder-tree",
        id: "subfolders",
        label: "Subfolders",
      },
    ] satisfies SearchFilterOption[];
  }

  if (isDateFilterKey(key)) {
    return searchDatePresets(ctx.today).map<SearchFilterOption>((preset) => ({
      filter: { key, label: preset.label.toLowerCase(), range: preset.range },
      icon: "i-lucide-calendar",
      id: preset.id,
      label: preset.label,
    }));
  }

  if (key === "size") {
    const typed = parseSizeQuery(query);
    const presets = sizePresets.map<SearchFilterOption>((preset) => {
      const label = sizeOptionLabel(preset.min, preset.max);
      return {
        filter: { key, max: preset.max, min: preset.min },
        icon: "i-lucide-hard-drive",
        id: label,
        label,
      };
    });

    if (!typed) {
      return presets;
    }
    return [
      {
        filter: { key, ...typed },
        icon: "i-lucide-hard-drive",
        id: "typed",
        label: sizeOptionLabel(typed.min, typed.max),
      },
    ];
  }

  if (key === "tag") {
    return (ctx.tags ?? []).map<SearchFilterOption>((tag) => ({
      filter: { key, name: tag.name, tagId: tag.id },
      icon: "i-lucide-tag",
      id: tag.id,
      label: tag.name,
    }));
  }

  const current = ctx.currentDirectory;
  const folders = (ctx.folders ?? []).filter((folder) => folder.id !== current?.id);
  const options = folders.map<SearchFilterOption>((folder) => ({
    filter: { directoryId: folder.id, key: "in", name: folder.name },
    icon: "i-lucide-folder",
    id: folder.id,
    label: folder.name,
  }));

  if (current && (!query.trim() || matchesQuery(current.name, query))) {
    options.unshift({
      filter: { directoryId: current.id, key: "in", name: current.name },
      icon: "i-lucide-folder",
      id: current.id,
      label: `This folder (${current.name})`,
    });
  }
  return options;
};

export const searchFilterOptions = (
  key: SearchFilterKey,
  query: string,
  ctx: SearchFilterContext,
): SearchFilterOption[] => {
  // Replacing a single-value chip must stay possible even when that chip is
  // what narrows the scope, so the scope check ignores filters of this key.
  const baseScope = resolveSearchScope(ctx.filters.filter((filter) => filter.key !== key));
  const applied = new Set(ctx.filters.map(searchFilterIdentity));
  // Folder and tag options come from a name search, and size reads its own syntax.
  const filterByText = key !== "in" && key !== "size" && key !== "tag";

  return listOptions(key, query, ctx).filter((option) => {
    if (multiValueKeys.has(key) && applied.has(searchFilterIdentity(option.filter))) {
      return false;
    }
    if (intersectScopes(baseScope, filterScope(option.filter)) === "none") {
      return false;
    }
    return !filterByText || matchesQuery(option.label, query);
  });
};

const isKeyAvailable = (def: SearchFilterDefinition, ctx: SearchFilterContext) => {
  const used = ctx.filters.some((filter) => filter.key === def.key);
  if (used && !multiValueKeys.has(def.key)) {
    return false;
  }

  const scope = resolveSearchScope(ctx.filters);
  if (intersectScopes(scope, keyScopes[def.key] ?? "both") === "none") {
    return false;
  }

  if (def.key === "deleted") {
    return ctx.filters.some((filter) => filter.key === "is" && filter.value === "deleted");
  }
  // Their options load as the user types, so an empty list says nothing yet.
  if (def.key === "in" || def.key === "tag") {
    return true;
  }
  return searchFilterOptions(def.key, "", ctx).length > 0;
};

// Lists the filter keys to offer for the word being typed. The full list only
// shows when asked for, so an empty input does not open a list on its own.
export const suggestSearchKeys = (
  text: string,
  ctx: SearchFilterContext,
  showAll = false,
): SearchFilterDefinition[] => {
  const word = lastWord(text).toLowerCase();
  const available = searchFilterDefinitions.filter((def) => isKeyAvailable(def, ctx));

  if (!word) {
    return showAll ? available : [];
  }
  return available.filter((def) => def.key.startsWith(word));
};

const applyFilter = (state: SearchUiInput, filter: SearchFilter) => {
  if (filter.key === "in") {
    state.parentDirectoryId = filter.directoryId;
  } else if (filter.key === "uploaded") {
    state.createdAfter = filter.range.from;
    state.createdBefore = filter.range.to;
  } else if (filter.key === "modified") {
    state.updatedAfter = filter.range.from;
    state.updatedBefore = filter.range.to;
  } else if (filter.key === "deleted") {
    state.deletedAfter = filter.range.from;
    state.deletedBefore = filter.range.to;
  } else if (filter.key === "tag") {
    state.tagIds = [...(state.tagIds ?? []), filter.tagId];
  } else if (filter.key === "size") {
    state.minSize = filter.min;
    state.maxSize = filter.max;
  } else if (filter.key === "is" && filter.value === "deleted") {
    state.isDeleted = true;
  } else if (filter.key === "is" && filter.value === "note") {
    state.withinDirectoryId = filter.notesFolderId;
  } else if (filter.key === "is") {
    state.isStarred = true;
  } else if (filter.key === "has" && filter.value === "files") {
    state.hasFiles = true;
  } else if (filter.key === "has") {
    state.hasSubdirectories = true;
  }
};

export const buildSearchRequests = (
  nameText: string,
  filters: SearchFilter[],
  pageSize = 20,
): SearchRequests => {
  const none: SearchRequests = { directories: null, files: null };
  const scope = resolveSearchScope(filters);

  if ((!nameText && filters.length === 0) || scope === "none") {
    return none;
  }

  // The backend ranks by relevance whenever there is search text, so the sort
  // only matters for filter-only searches, where newest first reads best.
  const state: SearchUiInput = { nameContains: nameText || undefined, pageSize };
  if (!nameText) {
    state.sortBy = SortBy.UpdatedAt;
    state.sortDirection = SortDirection.Desc;
  }
  filters.forEach((filter) => applyFilter(state, filter));

  const files = scope === "directories" ? null : fileSearchApiSchema.safeParse(state);
  const directories = scope === "files" ? null : directorySearchApiSchema.safeParse(state);

  return {
    directories: directories?.success ? directories.data : null,
    files: files?.success ? files.data : null,
  };
};
