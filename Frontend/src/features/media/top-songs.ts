import type { TopSong, TopSongsQuery } from "@/api/listening-rankings";

export const homeTopSongsPeriod = (now = new Date()): TopSongsQuery => ({
  from: new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - 6),
  ).toISOString(),
  to: new Date(Math.floor(now.getTime() / 60_000) * 60_000).toISOString(),
  limit: 5,
});

export const homeTopSongTitle = (song: TopSong) => song.file.title?.trim() || song.file.fileName;

export const homeTopSongArtist = (song: TopSong) => song.file.artist?.trim() || "Unknown artist";

export const homeTopSongBar = (song: TopSong, leadingCount: number) => {
  if (!Number.isFinite(leadingCount) || leadingCount <= 0) return 0;
  if (!Number.isFinite(song.qualifiedPlayCount) || song.qualifiedPlayCount <= 0) return 0;

  return Math.min(100, (song.qualifiedPlayCount / leadingCount) * 100);
};

export const homeTopSongPlays = (song: TopSong) => {
  const count = song.qualifiedPlayCount;

  return `${count.toLocaleString()} ${count === 1 ? "play" : "plays"}`;
};
