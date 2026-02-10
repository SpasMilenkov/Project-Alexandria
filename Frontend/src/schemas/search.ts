import { z } from "zod";
import { SortDirection } from "@/enums/SortDirection";
import { OrderBy } from "@/enums/OrderBy";
import { CalendarDate } from "@internationalized/date";

const dateValueSchema = z.instanceof(CalendarDate);

const date = new CalendarDate(2022, 2, 22)

export const baseSearchUiSchema = z.object({
  // Identity & structure
  directoryId: z.uuid().nullish(),
  parentDirectoryId: z.uuid().nullish(),

  // Text search
  nameContains: z.string().nullish(),

  // Ownership & sharing
  ownerId: z.uuid().nullish(),
  isShared: z.boolean().default(false),

  // Time filters (UI types)
  createdAfter: dateValueSchema.nullish(),
  createdBefore: dateValueSchema.nullish(),
  updatedAfter: dateValueSchema.nullish(),
  updatedBefore: dateValueSchema.nullish(),
  deletedAt: dateValueSchema.nullish(),

  // Flags
  isDeleted: z.boolean().default(false),
  isStarred: z.boolean().default(false),

  // Paging & sorting
  currentPage: z.number().int().min(0).default(0),
  pageSize: z.number().int().min(1).default(20),
  sortBy: z.enum(OrderBy).default(OrderBy.Name),
  sortDirection: z.enum(SortDirection).default(SortDirection.Asc),
});

export const fileSearchUiSchema = baseSearchUiSchema.extend({
  minSize: z.number().int().nullish(),
  maxSize: z.number().int().nullish(),
  mimeType: z.string().nullish(),
  onlyDeleted: z.boolean().default(false),
});

export const directorySearchUiSchema = baseSearchUiSchema.extend({
  hasFiles: z.boolean().default(true),
  hasSubdirectories: z.boolean().default(true),
});

export const unifiedSearchUiSchema = baseSearchUiSchema.extend({
  mode: z.enum(["files", "directories", "both"]).default("both"),

  // File-only
  minSize: z.number().int().nullish(),
  maxSize: z.number().int().nullish(),
  mimeType: z.string().nullish(),
  onlyDeleted: z.boolean().default(false),

  // Directory-only
  hasFiles: z.boolean().default(true),
  hasSubdirectories: z.boolean().default(true),
});

export const baseSearchApiSchema = baseSearchUiSchema.transform((v) => ({
  ...v,
  createdAfter: v.createdAfter?.toString() ?? null,
  createdBefore: v.createdBefore?.toString() ?? null,
  updatedAfter: v.updatedAfter?.toString() ?? null,
  updatedBefore: v.updatedBefore?.toString() ?? null,
  deletedAt: v.deletedAt?.toString() ?? null,
}));

export const fileSearchApiSchema = fileSearchUiSchema.transform((v) => ({
  ...v,
  createdAfter: v.createdAfter?.toString() ?? null,
  createdBefore: v.createdBefore?.toString() ?? null,
  updatedAfter: v.updatedAfter?.toString() ?? null,
  updatedBefore: v.updatedBefore?.toString() ?? null,
  deletedAt: v.deletedAt?.toString() ?? null,
}));

export const directorySearchApiSchema = directorySearchUiSchema.transform(
  (v) => ({
    ...v,
    createdAfter: v.createdAfter?.toString() ?? null,
    createdBefore: v.createdBefore?.toString() ?? null,
    updatedAfter: v.updatedAfter?.toString() ?? null,
    updatedBefore: v.updatedBefore?.toString() ?? null,
    deletedAt: v.deletedAt?.toString() ?? null,
  }),
);

export const bothSearchApiSchema = unifiedSearchUiSchema.transform((v) => ({
  ...v,
  createdAfter: v.createdAfter?.toString() ?? null,
  createdBefore: v.createdBefore?.toString() ?? null,
  updatedAfter: v.updatedAfter?.toString() ?? null,
  updatedBefore: v.updatedBefore?.toString() ?? null,
  deletedAt: v.deletedAt?.toString() ?? null,
}));

export const hybridSearchSchema = z.discriminatedUnion("mode", [
  z.object({
    mode: z.literal("files"),
    query: fileSearchApiSchema,
  }),
  z.object({
    mode: z.literal("directories"),
    query: directorySearchApiSchema,
  }),
  z.object({
    mode: z.literal("both"),
    query: bothSearchApiSchema,
  }),
]);

export type UnifiedSearchUiState = z.infer<typeof unifiedSearchUiSchema>;
export type FileSearchQuery = z.infer<typeof fileSearchApiSchema>;
export type DirectorySearchQuery = z.infer<typeof directorySearchApiSchema>;
