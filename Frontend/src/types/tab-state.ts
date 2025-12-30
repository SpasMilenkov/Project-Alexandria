import type { DirectorySummaryDto, FileSummary } from "@/api/directory";
import type { OrderBy } from "@/enums/OrderBy";
import type { SortDirection } from "@/enums/SortDirection";

export interface ExplorerState {
  currentDirId: string | null;
  directoryPath: string;
  directories: DirectorySummaryDto[];
  files: FileSummary[];
  currentDirectoryPage: number;
  totalSubDirectories: number;
  sortDirsBy: OrderBy;
  sortDirsDirection: SortDirection;
  currentFilePage: number;
  totalSubFiles: number;
  sortFilesBy: OrderBy;
  sortFilesDirection: SortDirection;
  viewMode: "grid" | "list";
}
