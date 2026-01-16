import { z } from "zod";

// Update File Metadata Schema
export const updateFileMetadataSchema = z.object({
  name: z.string().optional().nullable(),
  hasPreview: z.boolean().optional().nullable(),
});

export type UpdateFileMetadataSchema = z.infer<typeof updateFileMetadataSchema>;

// Generate Signed URL Schema
export const generateSignedUrlSchema = z.object({
  name: z.string().min(1, "File name is required"),
  path: z.string().optional().nullable(),
  expiry: z.string().optional(), // duration format
});

export type GenerateSignedUrlSchema = z.infer<typeof generateSignedUrlSchema>;

// Copy files schema

export const copyFilesSchema = z
  .object({
    fileIds: z.array(z.guid()).min(1).optional(),
    directoryIds: z.array(z.guid()).min(1).optional(),
    destinationId: z.guid(),
  })
  .refine(
    (data) => data.fileIds?.length || data.directoryIds?.length,
    {
      message: "Provide at least fileIds or directoryIds",
    }
  );

export type CopyFilesSchema = z.infer<typeof copyFilesSchema>;
