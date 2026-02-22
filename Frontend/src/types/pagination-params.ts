import type { SortBy } from "@/enums/SortBy";
import type { SortDirection } from "@/enums/SortDirection";

export interface PaginationParams {
  page: number;
  pageSize: number;
  SortBy: SortBy;
  sortDirection: SortDirection;
}
