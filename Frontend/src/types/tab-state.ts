import type { DirectorySummaryDto, FileSummary } from "@/api/directory";
import type { SortBy } from "@/enums/SortBy";
import type { SortDirection } from "@/enums/SortDirection";

export interface ExplorerState {
  currentDirId: string | null;
  directoryPath: string;
  directories: DirectorySummaryDto[];
  files: FileSummary[];
  currentDirectoryPage: number;
  totalSubDirectories: number;
  sortDirsBy: SortBy;
  sortDirsDirection: SortDirection;
  currentFilePage: number;
  totalSubFiles: number;
  sortFilesBy: SortBy;
  sortFilesDirection: SortDirection;
  viewMode: "grid" | "list";
}
