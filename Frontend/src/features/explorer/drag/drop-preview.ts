import type { PaginationParams } from "@/types/pagination-params";

import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { rootDirectories, subDirectories } from "@/queries/directories";
import { rootFiles, subFiles } from "@/queries/files";

/**
 * Same first-page parameters as a freshly opened explorer, so a drop preview and
 * the real listing share their cache entries in both directions.
 */
export const PREVIEW_PAGE_PARAMS: PaginationParams = {
  SortBy: SortBy.Name,
  page: 1,
  pageSize: 25,
  sortDirection: SortDirection.Asc,
};

export const previewDirectoriesQuery = (folderId: string | null) => {
  if (folderId === null) return rootDirectories({ ...PREVIEW_PAGE_PARAMS });

  return subDirectories({ id: folderId, params: { ...PREVIEW_PAGE_PARAMS } });
};

export const previewFilesQuery = (folderId: string | null) => {
  if (folderId === null) return rootFiles({ ...PREVIEW_PAGE_PARAMS });

  return subFiles({ id: folderId, params: { ...PREVIEW_PAGE_PARAMS } });
};
