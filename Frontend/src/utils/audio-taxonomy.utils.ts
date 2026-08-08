export interface ParsedGenre {
  parent: string;
  name: string;
  raw: string;
}

export interface MoodEntry {
  key: string;
  name: string;
  value: number;
}

const genreCache = new Map<string, ParsedGenre>();

// Genre labels come from the discogs taxonomy as "Parent---Subgenre" strings,
// e.g. "Rock---Pop Rock". Splits that into a parent tag and a display name.
// The parent/subgenre names are inferred from the label itself, so no bundled
// genre taxonomy is needed on the frontend.
export const parseGenreLabel = (raw: string): ParsedGenre => {
  const cached = genreCache.get(raw);
  if (cached) {
    return cached;
  }
  const separatorIndex = raw.indexOf("---");
  const parent = separatorIndex === -1 ? raw : raw.slice(0, separatorIndex);
  const name = separatorIndex === -1 ? raw : raw.slice(separatorIndex + 3);
  const parsed: ParsedGenre = { name, parent, raw };
  genreCache.set(raw, parsed);
  return parsed;
};

// Mood keys look like "mood_happy:happy" (axis:pole). The friendly name is the
// pole part, capitalized on demand ("happy" -> "Happy"), so no bundled mood
// taxonomy is needed either.
export const moodDisplayName = (key: string): string => {
  const pole = key.split(":")[1] ?? key;
  return pole.charAt(0).toUpperCase() + pole.slice(1);
};

// Bipolar mood axes shown as individual meters. The instrumental/voice pole
// is excluded here since it's binary and reads better as a single badge,
// see votePole below.
export const MOOD_AXIS_KEYS = [
  "mood_aggressive:aggressive",
  "mood_happy:happy",
  "mood_party:party",
  "mood_relaxed:relaxed",
  "mood_sad:sad",
  "mood_acoustic:acoustic",
] as const;

export const readMoodScore = (scores: Record<string, number>, key: string): number => {
  const pole = key.split(":")[1] ?? key;
  return scores[key] ?? scores[pole] ?? 0;
};

export const buildMoodEntries = (scores: Record<string, number>): MoodEntry[] =>
  MOOD_AXIS_KEYS.map((key) => ({
    key,
    name: moodDisplayName(key),
    value: readMoodScore(scores, key),
  })).sort((a, b) => b.value - a.value);

export const voicePole = (scores: Record<string, number>): { name: string; value: number } => {
  const instrumental = readMoodScore(scores, "voice_instrumental:instrumental");
  const voice = readMoodScore(scores, "voice_instrumental:voice");
  return instrumental >= voice
    ? { name: "Instrumental", value: instrumental }
    : { name: "Vocal", value: voice };
};

export const formatConfidence = (value: number): string => `${Math.round(value * 100)}%`;