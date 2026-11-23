<script setup lang="ts">
import { computed } from "vue";

import type { NoteBacklinkDto } from "@/api/links";

import NoteSnippet from "@/components/notes/NoteSnippet.vue";
import { noteTitleOf } from "@/features/notes/note-name";
import { formatCalendarDate, formatTimeAgo } from "@/utils/date-formatters";

const { note } = defineProps<{ note: NoteBacklinkDto }>();

const title = computed(() => noteTitleOf(note.name));
</script>

<template>
  <li>
    <RouterLink
      :to="{ name: 'note-editor', params: { fileId: note.fileId } }"
      class="flex flex-col gap-1 rounded-lg px-2 py-1.5 transition-colors duration-150 hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary"
    >
      <span class="flex min-w-0 items-center gap-2">
        <UIcon
          name="i-lucide-notebook-text"
          class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500"
          aria-hidden="true"
        />

        <span class="min-w-0 flex-1 truncate text-sm font-medium text-gray-900 dark:text-gray-100">
          {{ title }}
        </span>

        <span
          class="shrink-0 text-xs text-gray-500 dark:text-gray-500"
          :title="formatCalendarDate(note.updatedAt)"
        >
          {{ formatTimeAgo(note.updatedAt) }}
        </span>
      </span>

      <NoteSnippet v-if="note.snippet" :snippet="note.snippet" class="pl-6" />
    </RouterLink>
  </li>
</template>
