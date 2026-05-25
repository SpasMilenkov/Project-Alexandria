import { z } from "zod";

export const createPlaylistSchema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  description: z.string().max(500).optional(),
});

export const updatePlaylistSchema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  description: z.string().max(500).optional(),
});

export type CreatePlaylistSchema = z.infer<typeof createPlaylistSchema>;
export type UpdatePlaylistSchema = z.infer<typeof updatePlaylistSchema>;
