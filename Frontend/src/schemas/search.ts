import { z } from "zod";
import type { DateValue } from "@internationalized/date";
import { SortDirection } from "@/enums/SortDirection";
import { OrderBy } from "@/enums/OrderBy";

const dateValueSchema = z.custom<DateValue>(
  (val) => val && typeof val === "object" && "calendar" in val,
  { message: "Expected DateValue" },
);

export const directorySearchQuerySchema = z.object({
  directoryId: z.nullish(z.guid()),
  parentDirectoryId: z.guid().optional().nullable(),
  nameContains: z.string().optional().nullable(),
  ownerId: z.guid().optional().nullable(),
  isShared: z.boolean().optional().nullable(),

  // Accept DateValue and transform to ISO string for API
  createdAfter: dateValueSchema
    .transform((d) => d.toString()) // DateValue has a toString() method that returns YYYY-MM-DD
    .optional()
    .nullable(),
  createdBefore: dateValueSchema
    .transform((d) => d.toString())
    .optional()
    .nullable(),
  updatedAfter: dateValueSchema
    .transform((d) => d.toString())
    .optional()
    .nullable(),
  deletedAt: dateValueSchema
    .transform((d) => d.toString())
    .optional()
    .nullable(),

  hasFiles: z.boolean().optional().nullable(),
  hasSubdirectories: z.boolean().optional().nullable(),
  isDeleted: z.boolean().optional().nullable(),
  isStarred: z.boolean().optional().nullable(),
  currentPage: z.number().min(0),
  pageSize: z.number().min(1),
  sortBy: z.nullish(z.enum(OrderBy)),
  sortDirection: z.nullish(z.enum(SortDirection)),
});

export type DirectorySearchQuerySchema = z.infer<
  typeof directorySearchQuerySchema
>;

// export interface SearchDirectoryRequest {
//   // Identity & structure
//   directoryId?: string | null;
//   parentDirectoryId?: string | null;

//   // Text search
//   nameContains?: string | null;

//   // Ownership & sharing
//   ownerId?: string | null;
//   isShared?: boolean | null;

//   // Time filters (ISO 8601 strings for API calls)
//   createdAfter?: string | null;
//   createdBefore?: string | null;
//   updatedAfter?: string | null;
//   updatedBefore?: string | null;
//   deletedAt?: string | null;

//   // Contents
//   hasFiles?: boolean | null;
//   hasSubdirectories?: boolean | null;

//   // Flags
//   isDeleted?: boolean;
//   isStarred?: boolean;

//   // Paging & sorting
//   currentPage?: number;
//   pageSize?: number;
//   sortBy?: DirectorySortBy;
//   sortDirection?: SortDirection;
// }
