<script setup lang="ts">
import { useQuery } from "@pinia/colada";

import type { LinkTargetRef } from "@/api/links";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import NoteBacklinkRow from "@/components/notes/NoteBacklinkRow.vue";
import { getBacklinks } from "@/queries/links";

// The notes that link to an item, newest edit first. Callers supply the heading, since the
// drawer and the note's Connections panel title their sections differently.
const { target, enabled = true } = defineProps<{
  target: LinkTargetRef;
  enabled?: boolean;
}>();

const { data: notes, isPending, error } = useQuery(() => ({
  ...getBacklinks({ id: target.id, kind: target.kind }),
  enabled: enabled && target.id !== "",
}));
</script>

<template>
  <div v-if="isPending" class="flex h-10 items-center justify-center text-gray-500">
    <BlockSpinner :size="16" aria-label="Loading mentions" />
  </div>

  <p v-else-if="error" class="px-2 text-sm text-gray-600 dark:text-gray-400">
    Couldn't load the notes that mention this.
  </p>

  <ul v-else-if="notes && notes.length > 0" class="flex flex-col gap-0.5">
    <NoteBacklinkRow v-for="note in notes" :key="note.fileId" :note="note" />
  </ul>

  <p v-else class="px-2 text-sm text-gray-600 dark:text-gray-400">
    No note mentions this yet. Type [[ in a note to link it.
  </p>
</template>
