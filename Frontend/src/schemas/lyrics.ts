import { z } from "zod";

import { LyricsProvider } from "@/enums/lyrics-provider";

export const uploadLyricsSchema = z
  .object({
    jobId: z.uuid(),
    plainLyrics: z.string().nullish(),
    syncedLyrics: z.string().nullish(),
    isInstrumental: z.boolean().default(false),
  })
  .refine(
    (data) => data.isInstrumental || data.plainLyrics || data.syncedLyrics,
    {
      message:
        "At least one of plainLyrics or syncedLyrics must be provided when the track is not instrumental",
      path: ["plainLyrics"],
    },
  );

export type UploadLyricsSchema = z.infer<typeof uploadLyricsSchema>;

export const changeProviderSchema = z.object({
  lyricsId: z.uuid(),
  provider: z.enum(LyricsProvider),
});

export type ChangeProviderSchema = z.infer<typeof changeProviderSchema>;
