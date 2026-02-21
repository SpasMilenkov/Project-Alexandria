import { z } from "zod";
import { SortDirection } from "@/enums/SortDirection";
import { SortBy } from "@/enums/SortBy";
import { CalendarDate, today, getLocalTimeZone } from "@internationalized/date";
const dateValueSchema = z.instanceof(CalendarDate);

type DateField =
  | "createdAfter"
  | "createdBefore"
  | "updatedAfter"
  | "updatedBefore"
  | "deletedAfter"
  | "deletedBefore";

export const baseSearchUiSchema = z
  .object({
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
    deletedBefore: dateValueSchema.nullish(),
    deletedAfter: dateValueSchema.nullish(),

    // Flags
    isDeleted: z.boolean().default(false),
    isStarred: z.boolean().default(false),

    // Paging & sorting
    currentPage: z.number().int().min(0).default(0),
    pageSize: z.number().int().min(1).default(20),
    sortBy: z.enum(SortBy).default(SortBy.Name),
    sortDirection: z.enum(SortDirection).default(SortDirection.Asc),
  })
  .refine(
    (data) =>
      !data.createdAfter ||
      !data.createdBefore ||
      data.createdAfter.compare(data.createdBefore) <= 0,
    {
      message: "Created after cannot be before created before",
      path: ["createdBefore"],
    },
  )
  .refine(
    (data) =>
      !data.updatedAfter ||
      !data.updatedBefore ||
      data.updatedAfter.compare(data.updatedBefore) <= 0,
    {
      message: "Updated after cannot be before updated before",
      path: ["updatedBefore"],
    },
  )
  .refine(
    (data) =>
      !data.deletedAfter ||
      !data.deletedBefore ||
      data.deletedAfter.compare(data.deletedBefore) <= 0,
    {
      message: "Deleted after cannot be before deleted before",
      path: ["updatedBefore"],
    },
  )
  .superRefine((data, ctx) => {
    const now = today(getLocalTimeZone());

    const dateFields: DateField[] = [
      "createdAfter",
      "createdBefore",
      "updatedAfter",
      "updatedBefore",
      "deletedAfter",
      "deletedBefore",
    ];

    for (const field of dateFields) {
      const value = data[field];

      if (value && value.compare(now) > 0) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "Date cannot be in the future",
          path: [field],
        });
      }
    }
  });

export const fileSearchUiSchema = baseSearchUiSchema.extend({
  minSize: z.number().int().nullish(),
  maxSize: z.number().int().nullish(),
  mimeType: z.string().nullish(),
  onlyDeleted: z.boolean().default(false),
});

export const directorySearchUiSchema = baseSearchUiSchema.extend({
  hasFiles: z.boolean().nullable().default(null),
  hasSubdirectories: z.boolean().nullable().default(null),
});

export const unifiedSearchUiSchema = baseSearchUiSchema.extend({
  mode: z.enum(["files", "directories", "both"]).default("both"),

  // File-only
  minSize: z.number().int().nullish(),
  maxSize: z.number().int().nullish(),
  mimeType: z.string().nullish(),
  onlyDeleted: z.boolean().default(false),

  // Directory-only
  hasFiles: z.boolean().nullable().default(null),
  hasSubdirectories: z.boolean().nullable().default(null),
});

export const baseSearchApiSchema = baseSearchUiSchema.transform((v) => ({
  ...v,
  createdAfter: v.createdAfter?.toString() ?? null,
  createdBefore: v.createdBefore?.toString() ?? null,
  updatedAfter: v.updatedAfter?.toString() ?? null,
  updatedBefore: v.updatedBefore?.toString() ?? null,
  deletedBefore: v.deletedBefore?.toString() ?? null,
  deletedAfter: v.deletedAfter?.toString() ?? null,
}));

export const fileSearchApiSchema = fileSearchUiSchema.transform((v) => ({
  ...v,
  createdAfter: v.createdAfter?.toString() ?? null,
  createdBefore: v.createdBefore?.toString() ?? null,
  updatedAfter: v.updatedAfter?.toString() ?? null,
  updatedBefore: v.updatedBefore?.toString() ?? null,
  deletedBefore: v.deletedBefore?.toString() ?? null,
  deletedAfter: v.deletedAfter?.toString() ?? null,
}));

export const directorySearchApiSchema = directorySearchUiSchema.transform(
  (v) => ({
    ...v,
    createdAfter: v.createdAfter?.toString() ?? null,
    createdBefore: v.createdBefore?.toString() ?? null,
    updatedAfter: v.updatedAfter?.toString() ?? null,
    updatedBefore: v.updatedBefore?.toString() ?? null,
    deletedBefore: v.deletedBefore?.toString() ?? null,
    deletedAfter: v.deletedAfter?.toString() ?? null,
  }),
);

export const bothSearchApiSchema = unifiedSearchUiSchema.transform((v) => ({
  ...v,
  createdAfter: v.createdAfter?.toString() ?? null,
  createdBefore: v.createdBefore?.toString() ?? null,
  updatedAfter: v.updatedAfter?.toString() ?? null,
  updatedBefore: v.updatedBefore?.toString() ?? null,
  deletedBefore: v.deletedBefore?.toString() ?? null,
  deletedAfter: v.deletedAfter?.toString() ?? null,
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
