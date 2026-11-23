<script setup lang="ts">
import { computed } from "vue";

import type { FileResult } from "@/api/file";
import type { NoteSummaryDto } from "@/api/notes";

import TagBadge from "@/components/dashboard/tags/TagBadge.vue";
import NoteSnippet from "@/components/notes/NoteSnippet.vue";
import { noteTitleOf } from "@/features/notes/note-name";
import { formatCount } from "@/utils/count";
import { formatCalendarDate, formatTimeAgo } from "@/utils/date-formatters";

// Tags past this many are counted rather than shown, so the footer stays one line on a card.
const VISIBLE_TAGS = 2;

const { note, summary } = defineProps<{
  note: FileResult;
  // Undefined while the page's summaries load; the card shows without them meanwhile.
  summary: NoteSummaryDto | undefined;
}>();

const title = computed(() => noteTitleOf(note.fileName));
const editedAt = computed(() => note.updatedAt ?? note.createdAt);

const visibleTags = computed(() => note.tags.slice(0, VISIBLE_TAGS));
const hiddenTagCount = computed(() => Math.max(0, note.tags.length - VISIBLE_TAGS));

const mentions = computed(() => summary?.mentionCount ?? 0);
const mentionLabel = computed(() => `Mentioned in ${formatCount(mentions.value, "note")}`);
</script>

<template>
  <li class="min-w-0">
    <RouterLink
      :to="{ name: 'note-editor', params: { fileId: note.fileId } }"
      class="flex h-full min-h-40 flex-col gap-2 rounded-xl border border-gray-200/70 p-4 transition-colors duration-150 hover:border-gray-300 hover:bg-gray-900/5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:border-gray-700/70 dark:hover:border-gray-600 dark:hover:bg-white/5"
    >
      <span
        class="line-clamp-2 font-semibold wrap-break-word text-gray-900 dark:text-gray-100"
        :title="title"
      >
        {{ title }}
      </span>

      <NoteSnippet v-if="summary?.excerpt" :snippet="summary.excerpt" :lines="4" />

      <span
        v-if="note.matchedInContent"
        class="flex items-center gap-1 text-xs text-gray-600 dark:text-gray-400"
      >
        <UIcon name="i-lucide-text-search" class="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
        Matches in the text
      </span>

      <span class="mt-auto flex min-w-0 items-center justify-between gap-3 pt-2">
        <span class="flex min-w-0 items-center gap-1.5 overflow-hidden">
          <TagBadge v-for="tag in visibleTags" :key="tag.id" :tag="tag" class="min-w-0" />

          <span v-if="hiddenTagCount > 0" class="shrink-0 text-xs text-gray-500 dark:text-gray-500">
            +{{ hiddenTagCount }}
          </span>
        </span>

        <span
          class="flex shrink-0 items-center gap-3 text-xs text-gray-500 tabular-nums dark:text-gray-500"
        >
          <span v-if="mentions > 0" class="flex items-center gap-1" :title="mentionLabel">
            <UIcon name="i-lucide-text-quote" class="h-3.5 w-3.5" aria-hidden="true" />
            <span class="sr-only">{{ mentionLabel }}</span>
            <span aria-hidden="true">{{ mentions }}</span>
          </span>

          <span :title="formatCalendarDate(editedAt)">{{ formatTimeAgo(editedAt) }}</span>
        </span>
      </span>
    </RouterLink>
  </li>
</template>
