import { z } from "zod";

// A year is valid when it is exactly four digits and falls between 1000 and next
// year (inclusive). Mirrors MetadataValidation.IsValidYear on the backend; both
// must accept the same values.
export const metadataYearSchema = z
  .string()
  .regex(/^\d{4}$/, "Year must be a 4-digit year (e.g. 1999).")
  .refine(
    (value) => {
      const year = Number(value);
      return year >= 1000 && year <= new Date().getFullYear() + 1;
    },
    { message: "Year must be a 4-digit year (e.g. 1999)." },
  );

// Update File Metadata Schema
export const updateFileMetadataSchema = z.object({
  album: z.string().max(255).nullish(),
  artist: z.string().max(255).nullish(),
  hasPreview: z.boolean().nullish(),
  id: z.guid(),
  name: z.string().nullish(),
  title: z.string().max(255).nullish(),
  year: metadataYearSchema.nullish(),
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
    destinationId: z.uuid(),
    directoryIds: z.array(z.uuid()).min(1).optional(),
    fileIds: z.array(z.uuid()).min(1).optional(),
  })
  .refine((data) => data.fileIds?.length || data.directoryIds?.length, {
    message: "Provide at least fileIds or directoryIds",
  });

export type CopyFilesSchema = z.infer<typeof copyFilesSchema>;
