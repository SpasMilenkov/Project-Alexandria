<template>
  <span
    ref="rootRef"
    class="flex min-w-0 items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400"
    data-content-row-excerpt
  >
    <UIcon name="i-lucide-text-search" class="w-3.5 h-3.5 shrink-0" aria-hidden="true" />

    <span v-if="excerpt" class="truncate">
      <span class="sr-only">Found in the text:</span>
      <span class="text-gray-500 dark:text-gray-500" aria-hidden="true">…</span>
      <ContentExcerptText :segments="excerpt" />
    </span>

    <span v-else class="truncate">Found in the text</span>
  </span>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useIntersectionObserver } from "@vueuse/core";
import { computed, ref, useTemplateRef } from "vue";

import { leadIntoFirstMatch, markQueryWords } from "@/features/explorer/content-excerpts";
import { getFirstContentMatch } from "@/queries/files";

import ContentExcerptText from "./ContentExcerptText.vue";

// Characters kept before the first match, so it stays on the one line the row has.
const MAX_LEAD = 24;

const { fileId, query, versionId } = defineProps<{
  fileId: string;
  query: string;
  versionId: string;
}>();

// Every row in view asks at once, so rows ask only for the first match, which the server
// cuts cheaply; a row that has asked keeps its answer while it scrolls away.
const root = useTemplateRef<HTMLElement>("rootRef");
const hasBeenSeen = ref(false);

const { stop } = useIntersectionObserver(root, ([entry]) => {
  if (entry?.isIntersecting) {
    hasBeenSeen.value = true;
    stop();
  }
});

const { data } = useQuery(() => ({
  ...getFirstContentMatch({ fileId, query, versionId }),
  enabled: hasBeenSeen.value,
}));

const excerpt = computed(() => {
  const first = data.value?.fragments[0];
  if (!first) {
    return null;
  }
  return leadIntoFirstMatch(markQueryWords(first.segments, query), MAX_LEAD);
});
</script>
