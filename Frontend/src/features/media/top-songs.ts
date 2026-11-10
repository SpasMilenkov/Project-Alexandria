import type { TopSong, TopSongsQuery } from "@/api/listening-rankings";

import { formatDuration } from "@/utils/date-formatters";

export const homeTopSongsPeriod = (now = new Date()): TopSongsQuery => ({
  from: new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - 6),
  ).toISOString(),
  to: new Date(Math.floor(now.getTime() / 60_000) * 60_000).toISOString(),
  limit: 5,
});

export const homeTopSongTitle = (song: TopSong) => song.file.title?.trim() || song.file.fileName;

export const homeTopSongArtist = (song: TopSong) => song.file.artist?.trim() || "Unknown artist";

export const homeTopSongBar = (song: TopSong, leadingSeconds: number) => {
  if (!Number.isFinite(leadingSeconds) || leadingSeconds <= 0) return 0;
  if (!Number.isFinite(song.totalListenedSeconds) || song.totalListenedSeconds <= 0) return 0;

  return Math.min(100, (song.totalListenedSeconds / leadingSeconds) * 100);
};

export const homeTopSongListened = (song: TopSong) =>
  formatDuration(song.totalListenedSeconds, { largestUnit: "hour" });
