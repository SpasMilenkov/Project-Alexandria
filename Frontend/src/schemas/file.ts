import { z } from "zod";
// Update File Metadata Schema
export const updateFileMetadataSchema = z.object({
  hasPreview: z.boolean().nullish(),
  id: z.guid(),
  name: z.string().nullish(),
});

export type UpdateFileMetadataSchema = z.infer<typeof updateFileMetadataSchema>;

// Generate Signed URL Schema
export const generateSignedUrlSchema = z.object({
  expiry: z.string().optional(),
  name: z.string().min(1, "File name is required"),
  path: z.string().nullish(),
});

export type GenerateSignedUrlSchema = z.infer<typeof generateSignedUrlSchema>;

// Copy files schema
export const copyFilesSchema = z
  .object({
    destinationId: z.string().uuid(),
    directoryIds: z.array(z.string().uuid()).min(1).optional(),
    fileIds: z.array(z.string().uuid()).min(1).optional(),
  })
  .refine((data) => data.fileIds?.length || data.directoryIds?.length, {
    message: "Provide at least fileIds or directoryIds",
  });

export type CopyFilesSchema = z.infer<typeof copyFilesSchema>;
