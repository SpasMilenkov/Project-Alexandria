import { z } from "zod";

export const createPlaylistSchema = z.object({
  name: z.string().min(1),
  description: z.string().optional(),
  coverFile: z
    .instanceof(File)
    .refine((f) => f.size <= 4 * 1024 * 1024, "Max 4MB")
    .refine(
      (f) => ["image/jpeg", "image/png", "image/webp", "image/gif"].includes(f.type),
      "Invalid type",
    )
    .optional(),
});

export const updatePlaylistSchema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  description: z.string().max(500).optional(),
  coverFile: z
    .instanceof(File)
    .refine((f) => f.size <= 4 * 1024 * 1024, "Max 4MB")
    .refine(
      (f) => ["image/jpeg", "image/png", "image/webp", "image/gif"].includes(f.type),
      "Invalid type",
    )
    .optional(),
});

export type CreatePlaylistSchema = z.infer<typeof createPlaylistSchema>;
export type UpdatePlaylistSchema = z.infer<typeof updatePlaylistSchema>;
