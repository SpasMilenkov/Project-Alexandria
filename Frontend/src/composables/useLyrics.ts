import { type Ref, computed } from "vue";

export interface LyricLine {
  time: number;
  text: string;
}

const TIME_TAG = /\[(\d{2}):(\d{2})\.(\d{2,3})\]/g;

/**
 * Parses an LRC-formatted synced lyrics string into a sorted list of
 * { time, text } lines. Handles both real newlines and literal "\n"
 * sequences, since some providers escape them in the stored payload.
 * A line with a time tag but no text (an instrumental gap) is kept
 * with an empty string so the UI can render a rest indicator.
 */
export const parseSyncedLyrics = (raw: string): LyricLine[] => {
  const normalized = raw.replace(/\\n/g, "\n");
  const rawLines = normalized.split(/\r?\n/);
  const lines: LyricLine[] = [];

  for (const rawLine of rawLines) {
    const matches = [...rawLine.matchAll(TIME_TAG)];
    if (!matches.length) continue;

    const text = rawLine.replace(TIME_TAG, "").trim();

    for (const match of matches) {
      const minutes = Number(match[1]);
      const seconds = Number(match[2]);
      const fraction = Number(match[3].padEnd(3, "0")) / 1000;
      lines.push({ time: minutes * 60 + seconds + fraction, text });
    }
  }

  return lines.sort((a, b) => a.time - b.time);
};

/**
 * Splits plain (unsynced) lyrics into display lines, normalizing the
 * same escaped-newline quirk as parseSyncedLyrics.
 */
export const parsePlainLyrics = (raw: string): string[] => raw.replace(/\\n/g, "\n").split(/\r?\n/);

export type LyricsFormat = "synced" | "plain";

/**
 * Classifies pasted lyrics as synced or plain by running them through the real
 * synced parser, so anything classified as synced is guaranteed to render as
 * synced. Metadata-only LRC headers ([ar:], [ti:]) without any time-tagged
 * line classify as plain.
 */
export const detectLyricsFormat = (raw: string): LyricsFormat =>
  parseSyncedLyrics(raw).length > 0 ? "synced" : "plain";

/**
 * Tracks which synced line is active for a given playback time using
 * binary search, since lines are sorted ascending. Returns -1 before
 * the first cue.
 */
export const useLyricsSync = (lines: Ref<LyricLine[]>, currentTime: Ref<number>) => {
  const activeIndex = computed(() => {
    const list = lines.value;
    if (!list.length) return -1;

    let lo = 0;
    let hi = list.length - 1;
    let result = -1;

    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      if (list[mid].time <= currentTime.value) {
        result = mid;
        lo = mid + 1;
      } else {
        hi = mid - 1;
      }
    }

    return result;
  });

  return { activeIndex };
};
