import { formatCount } from "@/utils/count";

// Listening stats only count audio, so a qualified play always reads as a song played.
export const formatSongsPlayed = (count: number) => `${formatCount(count, "song")} played`;
