import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { logger } from "@/utils/logger";

export type ExplorerViewMode = "grid" | "list";

export interface ExplorerPreferences {
  pageSize: number;
  sortBy: SortBy;
  sortDirection: SortDirection;
  viewMode: ExplorerViewMode;
}

// The backend rejects page sizes above 100.
export const EXPLORER_PAGE_SIZES = [25, 50, 100] as const;

export const EXPLORER_PREFERENCES_KEY = "alexandria:explorer-preferences:v1";

const EXPLORER_SORT_FIELDS = new Set<SortBy>([SortBy.Name, SortBy.CreatedAt, SortBy.UpdatedAt]);

const DEFAULT_PAGE_SIZE = EXPLORER_PAGE_SIZES[0];

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null;

const isPageSize = (value: unknown): value is number =>
  EXPLORER_PAGE_SIZES.some((size) => size === value);

const isSortBy = (value: unknown): value is SortBy =>
  typeof value === "number" && EXPLORER_SORT_FIELDS.has(value);

const isSortDirection = (value: unknown): value is SortDirection =>
  value === SortDirection.Asc || value === SortDirection.Desc;

const isViewMode = (value: unknown): value is ExplorerViewMode =>
  value === "grid" || value === "list";

/**
 * Field-by-field so one stale or hand-edited value falls back alone instead of
 * discarding every other saved choice.
 */
export const parseExplorerPreferences = (
  raw: unknown,
  fallbackViewMode: ExplorerViewMode,
): ExplorerPreferences => {
  const stored = isRecord(raw) ? raw : {};

  return {
    pageSize: isPageSize(stored.pageSize) ? stored.pageSize : DEFAULT_PAGE_SIZE,
    sortBy: isSortBy(stored.sortBy) ? stored.sortBy : SortBy.Name,
    sortDirection: isSortDirection(stored.sortDirection) ? stored.sortDirection : SortDirection.Asc,
    viewMode: isViewMode(stored.viewMode) ? stored.viewMode : fallbackViewMode,
  };
};

export const loadExplorerPreferences = (fallbackViewMode: ExplorerViewMode) => {
  try {
    const raw = window.localStorage.getItem(EXPLORER_PREFERENCES_KEY);

    if (raw === null) return parseExplorerPreferences(null, fallbackViewMode);

    return parseExplorerPreferences(JSON.parse(raw), fallbackViewMode);
  } catch (error) {
    logger.warn("Explorer preferences could not be read, using defaults", error);
    return parseExplorerPreferences(null, fallbackViewMode);
  }
};

export const saveExplorerPreferences = (preferences: ExplorerPreferences) => {
  try {
    window.localStorage.setItem(EXPLORER_PREFERENCES_KEY, JSON.stringify(preferences));
  } catch (error) {
    logger.warn("Explorer preferences could not be saved", error);
  }
};
