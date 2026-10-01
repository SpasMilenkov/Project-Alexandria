import type { Ref } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import type { QueueEntry } from "./types";

export const createQueue = (entries: Ref<QueueEntry[]>) => {
  let sequence = 0;

  const add = (files: MediaFileDto[], group: string | null, next: boolean) => {
    const added = files.map((file) => ({ id: `q-${++sequence}`, file, group }));
    entries.value = next ? [...added, ...entries.value] : [...entries.value, ...added];
  };

  const takeHead = (): QueueEntry | null => {
    const head = entries.value[0] ?? null;
    if (head) entries.value = entries.value.slice(1);
    return head;
  };

  return {
    add,
    takeHead,
    removeAt: (index: number) => {
      entries.value = entries.value.filter((_, i) => i !== index);
    },
    dropThrough: (index: number) => {
      entries.value = entries.value.slice(index + 1);
    },
    clear: () => {
      entries.value = [];
    },
    shuffle: () => {
      const shuffled = [...entries.value];
      for (let i = shuffled.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        const a = shuffled[i];
        const b = shuffled[j];
        if (a && b) {
          shuffled[i] = b;
          shuffled[j] = a;
        }
      }
      entries.value = shuffled;
    },
  };
};
