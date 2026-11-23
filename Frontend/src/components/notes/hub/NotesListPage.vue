<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, watch } from "vue";

import type { FileResult } from "@/api/file";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import NoteCard from "@/components/notes/hub/NoteCard.vue";
import {
  type NoteEditGroup,
  type NotesPageLoaded,
  noteEditGroup,
  notesListQuery,
} from "@/features/notes/notes-page";
import { searchFile } from "@/queries/files";
import { getNoteSummaries } from "@/queries/notes";

interface CardGroup extends NoteEditGroup {
  // False when the previous page already put this heading up, so the run continues under it.
  showHeading: boolean;
  notes: FileResult[];
}

// One "Load more" page of notes, rendered straight into the page's shared grid so a group
// flows on across pages. Each page asks for its own excerpts, so the request stays the size of
// a page however far the list has been scrolled.
const {
  folderId,
  search,
  page,
  now,
  previousGroupKey = null,
} = defineProps<{
  folderId: string;
  search: string;
  page: number;
  // One "now" for every page, so all of them group against the same day.
  now: number;
  previousGroupKey?: string | null;
}>();

const emit = defineEmits<{ loaded: [result: NotesPageLoaded] }>();

const { data, isPending, error, refetch } = useQuery(() =>
  searchFile(notesListQuery({ folderId, page, search })),
);

const notes = computed(() => data.value?.items ?? []);
const fileIds = computed(() => notes.value.map((note) => note.fileId));

const { data: summaries } = useQuery(() => ({
  ...getNoteSummaries(fileIds.value),
  enabled: fileIds.value.length > 0,
}));

const summaryById = computed(
  () => new Map((summaries.value ?? []).map((summary) => [summary.fileId, summary])),
);

const groups = computed(() => {
  const result: CardGroup[] = [];

  for (const note of notes.value) {
    const group = noteEditGroup(note.updatedAt ?? note.createdAt, now);
    const current = result.at(-1);

    if (current?.key === group.key) {
      current.notes.push(note);
    } else {
      const continuesPrevious = result.length === 0 && group.key === previousGroupKey;

      result.push({ ...group, notes: [note], showHeading: !continuesPrevious });
    }
  }

  return result;
});

watch(
  data,
  (result) => {
    if (!result) return;

    emit("loaded", {
      hasNext: result.hasNext,
      lastGroupKey: groups.value.at(-1)?.key ?? null,
      total: result.totalCount,
    });
  },
  { immediate: true },
);
</script>

<template>
  <li v-if="isPending" class="col-span-full flex h-24 items-center justify-center text-gray-500">
    <BlockSpinner :size="20" aria-label="Loading notes" />
  </li>

  <li v-else-if="error" class="col-span-full flex flex-col items-center gap-2 py-6 text-center">
    <p class="text-sm text-gray-600 dark:text-gray-400">Couldn't load these notes.</p>
    <UButton label="Try again" size="sm" color="neutral" variant="outline" @click="refetch()" />
  </li>

  <template v-else>
    <template v-for="group in groups" :key="group.key">
      <li v-if="group.showHeading" role="presentation" class="col-span-full px-1 pt-3 first:pt-0">
        <h3 class="text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400">
          {{ group.label }}
        </h3>
      </li>

      <NoteCard
        v-for="note in group.notes"
        :key="note.fileId"
        :note="note"
        :summary="summaryById.get(note.fileId)"
      />
    </template>
  </template>
</template>
