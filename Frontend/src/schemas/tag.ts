import { z } from "zod";

// Create Tag Schema
export const createTagSchema = z.object({
  color: z.string().min(1, "Tag color is required"),
  description: z.string().max(255, "name cannot be longer than 255 symbols").nullish(),
  icon: z.string().min(1, "Tag icon is required"),
  name: z
    .string()
    .min(1, "Tag name is required")
    .max(255, "Name cannot be longer than 255 symbols"),
});

export type CreateTagSchema = z.infer<typeof createTagSchema>;

// Update Tag Schema
export const updateTagSchema = z.object({
  color: z.string().min(1, "Tag color is required"),
  description: z.string().max(255, "name cannot be longer than 255 symbols").nullish(),
  icon: z.string().min(1, "Tag icon is required"),
  name: z.string().min(1, "Tag name is required"),
});

export type UpdateTagSchema = z.infer<typeof updateTagSchema>;

// Search Tags Schema
export const searchTagsSchema = z.object({
  createdAfter: z.iso.datetime().optional().nullable(),
  createdBefore: z.iso.datetime().optional().nullable(),
  createdBy: z.uuid().optional().nullable(),
  excludeOnFile: z.uuid().nullish(),
  hasFiles: z.boolean().optional().nullable(),
  nameContains: z.string().optional().nullable(),
  page: z.number().int().min(0).default(0),
  pageSize: z.number().int().min(1).max(100).default(20),
  updatedAfter: z.iso.datetime().optional().nullable(),
  updatedBefore: z.iso.datetime().optional().nullable(),
  updatedBy: z.uuid().optional().nullable(),
  userId: z.uuid().optional().nullable(),
});

export type SearchTagsSchema = z.infer<typeof searchTagsSchema>;

// Add Tags to File Schema
export const addTagsToFileSchema = z.object({
  tagIds: z.array(z.uuid()).min(1, "At least one tag ID is required"),
});

export type AddTagsToFileSchema = z.infer<typeof addTagsToFileSchema>;

// Search Files by Tags Schema
export const searchFilesByTagsSchema = z.object({
  createdAfter: z.iso.datetime().optional().nullable(),
  createdBefore: z.iso.datetime().optional().nullable(),
  matchType: z.enum(["any", "all", "exact"]).default("any"),
  maxFileSize: z.number().int().optional().nullable(),
  mimeTypePrefix: z.string().optional().nullable(),
  minFileSize: z.number().int().optional().nullable(),
  page: z.number().int().min(0).default(0),
  pageSize: z.number().int().min(1).max(100).default(20),
  tagIds: z.array(z.uuid()).min(1, "At least one tag ID is required"),
  userId: z.uuid().optional().nullable(),
});

export type SearchFilesByTagsSchema = z.infer<typeof searchFilesByTagsSchema>;
