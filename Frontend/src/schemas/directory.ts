import { z } from "zod";

// Create Directory Schema
export const createDirectorySchema = z.object({
  name: z.string().min(1, "Directory name is required"),
  parentId: z.uuid().optional().nullable(),
});

export type CreateDirectorySchema = z.infer<typeof createDirectorySchema>;

// Update Directory Schema
export const updateDirectorySchema = z.object({
  directoryId: z.uuid(),
  name: z.string().min(1, "Directory name is required"),
});

export type UpdateDirectorySchema = z.infer<typeof updateDirectorySchema>;

// Move Directory Schema
export const moveDirectorySchema = z.object({
  destinationId: z.uuid().optional(),
  directoryIds: z.array(z.uuid()),
});

export type MoveDirectorySchema = z.infer<typeof moveDirectorySchema>;

// Delete Directory Schema
export const deleteDirectorySchema = z.object({
  force: z.boolean().default(false),
});

export type DeleteDirectorySchema = z.infer<typeof deleteDirectorySchema>;
