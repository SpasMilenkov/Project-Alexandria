import type { SortBy, SortDirection, UserRole } from "@/enums";


export interface UserDetailsDto {
  id: string;
  userName: string;
  email: string;
  isLockedOut: boolean;
  role: UserRole | null;
  lockedOutStarted: string | null;
  createdAt: string;
  updatedAt: string | null;
  deletedAt: string | null;
}


export interface UpdateUserDto {
  userName?: string | null;
  email?: string | null;
  role?: UserRole | null;
}

export interface UserQueryDto {
  page: number;
  pageSize: number;
  sortBy: SortBy;
  sortDirection: SortDirection;
  userName?: string | null;
  userEmail?: string | null;
  role?: UserRole | null;
  isLockedOut?: boolean | null;
  showDeleted: boolean;
  showDeletedOnly: boolean;
  createdBefore?: string | null;
  createdAfter?: string | null;
  updatedBefore?: string | null;
  updatedAfter?: string | null;
  deletedBefore?: string | null;
  deletedAfter?: string | null;
  lockedOutBefore?: string | null;
  lockedOutAfter?: string | null;
}
