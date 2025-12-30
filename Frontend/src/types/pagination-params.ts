import type { OrderBy } from "@/enums/OrderBy";
import type { SortDirection } from "@/enums/SortDirection";

export interface PaginationParams {
  page: number;
  pageSize: number;
  orderBy: OrderBy;
  sortDirection: SortDirection;
}
