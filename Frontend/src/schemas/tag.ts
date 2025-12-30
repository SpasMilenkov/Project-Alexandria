import { z } from "zod";

// Create Tag Schema
export const createTagSchema = z.object({
  name: z.string().min(1, "Tag name is required"),
});

export type CreateTagSchema = z.infer<typeof createTagSchema>;

// Update Tag Schema
export const updateTagSchema = z.object({
  name: z.string().min(1, "Tag name is required"),
});

export type UpdateTagSchema = z.infer<typeof updateTagSchema>;

// Search Tags Schema
export const searchTagsSchema = z.object({
  userId: z.uuid().optional().nullable(),
  createdBy: z.uuid().optional().nullable(),
  updatedBy: z.uuid().optional().nullable(),
  createdAfter: z.iso.datetime().optional().nullable(),
  createdBefore: z.iso.datetime().optional().nullable(),
  updatedAfter: z.iso.datetime().optional().nullable(),
  updatedBefore: z.iso.datetime().optional().nullable(),
  nameContains: z.string().optional().nullable(),
  hasFiles: z.boolean().optional().nullable(),
  page: z.number().int().min(0).default(0),
  pageSize: z.number().int().min(1).max(100).default(20),
});

export type SearchTagsSchema = z.infer<typeof searchTagsSchema>;

// Add Tags to File Schema
export const addTagsToFileSchema = z.object({
  tagIds: z.array(z.uuid()).min(1, "At least one tag ID is required"),
});

export type AddTagsToFileSchema = z.infer<typeof addTagsToFileSchema>;

// Search Files by Tags Schema
export const searchFilesByTagsSchema = z.object({
  tagIds: z.array(z.string().uuid()).min(1, "At least one tag ID is required"),
  matchType: z.enum(["any", "all", "exact"]).default("any"),
  userId: z.uuid().optional().nullable(),
  page: z.number().int().min(0).default(0),
  pageSize: z.number().int().min(1).max(100).default(20),
  minFileSize: z.number().int().optional().nullable(),
  maxFileSize: z.number().int().optional().nullable(),
  mimeTypePrefix: z.string().optional().nullable(),
  createdAfter: z.iso.datetime().optional().nullable(),
  createdBefore: z.iso.datetime().optional().nullable(),
});

export type SearchFilesByTagsSchema = z.infer<typeof searchFilesByTagsSchema>;
