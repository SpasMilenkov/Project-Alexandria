<template>
  <details v-if="detail" class="group text-xs text-gray-600 dark:text-gray-400">
    <summary
      class="w-fit cursor-pointer rounded py-2 underline decoration-gray-400 dark:decoration-gray-500 underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary"
    >
      Behind this story
    </summary>

    <p class="mt-2 max-w-prose leading-relaxed">{{ detail }}</p>
  </details>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { WrappedCardResponse } from "@/api/stats";

import { wrappedStoryNote } from "@/utils/wrapped-copy.utils";

const { card } = defineProps<{ card: WrappedCardResponse }>();

const detail = computed(() => wrappedStoryNote(card));
</script>
