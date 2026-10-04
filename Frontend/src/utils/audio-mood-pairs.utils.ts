export type MoodSide = "left" | "right";

export interface MoodEntryLike {
  key: string;
  name: string;
  value: number;
}

export interface MoodPair<T extends MoodEntryLike = MoodEntryLike> {
  key: string;
  left: T;
  right: T;
  leading: MoodSide;
}

export interface MoodLayout<T extends MoodEntryLike = MoodEntryLike> {
  pairs: MoodPair<T>[];
  singles: T[];
}

// Opposing poles shown as one diverging row. Matched by display name, so an
// axis that is missing from the payload simply falls back to a single scale.
export const MOOD_PAIRS: readonly (readonly [string, string])[] = [
  ["Relaxed", "Aggressive"],
  ["Sad", "Happy"],
];

const normalize = (name: string) => name.trim().toLowerCase();

export const buildMoodPairs = <T extends MoodEntryLike>(entries: T[]): MoodLayout<T> => {
  const byName = new Map(entries.map((entry) => [normalize(entry.name), entry]));
  const usedKeys = new Set<string>();
  const pairs: MoodPair<T>[] = [];

  for (const [leftName, rightName] of MOOD_PAIRS) {
    const left = byName.get(normalize(leftName));
    const right = byName.get(normalize(rightName));
    if (!left || !right) continue;

    usedKeys.add(left.key);
    usedKeys.add(right.key);
    pairs.push({
      key: `${left.key}|${right.key}`,
      left,
      right,
      leading: left.value >= right.value ? "left" : "right",
    });
  }

  const singles = entries.filter((entry) => !usedKeys.has(entry.key));
  return { pairs, singles };
};
