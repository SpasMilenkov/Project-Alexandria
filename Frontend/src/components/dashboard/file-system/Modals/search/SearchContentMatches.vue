<template>
  <section class="flex flex-col gap-3" :aria-labelledby="headingId">
    <h4
      :id="headingId"
      class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-gray-600 dark:text-gray-400"
    >
      <UIcon name="i-lucide-text-search" class="w-4 h-4" aria-hidden="true" />
      Found in the text
    </h4>

    <div v-if="isPending" class="flex h-16 items-center justify-center text-gray-500">
      <BlockSpinner :size="20" />
    </div>

    <p v-else-if="error" class="text-sm text-gray-600 dark:text-gray-400">
      Couldn't load the excerpts.
    </p>

    <p v-else-if="excerpts.length === 0" class="text-sm text-gray-600 dark:text-gray-400">
      The words are in this document, but no excerpt could be cut around them.
    </p>

    <ol v-else class="flex flex-col gap-3">
      <li
        v-for="(segments, index) in excerpts"
        :key="index"
        class="border-l-2 border-gray-300/80 pl-3 text-sm leading-relaxed wrap-break-word text-gray-700 dark:border-gray-600/80 dark:text-gray-300"
        data-content-excerpt
      >
        <span class="text-gray-500 dark:text-gray-500" aria-hidden="true">…</span>
        <ContentExcerptText :segments="segments" />
        <span class="text-gray-500 dark:text-gray-500" aria-hidden="true">…</span>
      </li>
    </ol>

    <p v-if="data?.isTruncated" class="text-xs text-gray-500 dark:text-gray-500">
      This document is long, so only its start is searched.
    </p>
  </section>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, useId } from "vue";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { markQueryWords } from "@/features/explorer/content-excerpts";
import { getContentMatches } from "@/queries/files";

import ContentExcerptText from "./ContentExcerptText.vue";

const { fileId, query, versionId } = defineProps<{
  fileId: string;
  query: string;
  versionId: string;
}>();

const headingId = useId();

// Same key as the excerpt under the result row, so a row that already showed one opens here at once.
const { data, error, isPending } = useQuery(() => getContentMatches({ fileId, query, versionId }));

const excerpts = computed(() =>
  (data.value?.fragments ?? []).map((fragment) => markQueryWords(fragment.segments, query)),
);
</script>
