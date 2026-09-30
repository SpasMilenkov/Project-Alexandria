import type { MediaFileDto } from "@/api/streaming";

import type { Origin, UpNextItem } from "./types";

export interface HistoryEntry {
  file: MediaFileDto;
  origin: Origin;
}

export const createHistory = (capacity = 100, onChange: () => void = () => undefined) => {
  let entries: HistoryEntry[] = [];
  let index = -1;

  const clamp = () => {
    if (entries.length > capacity) entries = entries.slice(entries.length - capacity);
    if (index >= entries.length) index = entries.length - 1;
  };

  return {
    push: (file: MediaFileDto, origin: Origin) => {
      entries = [...entries.slice(0, index + 1), { file, origin }];
      index = entries.length - 1;
      clamp();
      onChange();
    },
    stepBack: (): HistoryEntry | null => {
      if (index <= 0) return null;
      index -= 1;
      onChange();
      return entries[index] ?? null;
    },
    stepForward: (): HistoryEntry | null => {
      if (index < 0 || index >= entries.length - 1) return null;
      index += 1;
      onChange();
      return entries[index] ?? null;
    },
    atHead: () => index <= 0,
    hasForward: () => index < entries.length - 1,
    current: (): HistoryEntry | null => entries[index] ?? null,
    forwardEntries: (): HistoryEntry[] => entries.slice(index + 1),
    toUpNext: (): UpNextItem[] =>
      entries.slice(index + 1).map((entry, queueIndex) => ({
        kind: "history" as const,
        file: entry.file,
        queueIndex,
        position: -1,
        group: null,
      })),
    clear: () => {
      entries = [];
      index = -1;
      onChange();
    },
  };
};
