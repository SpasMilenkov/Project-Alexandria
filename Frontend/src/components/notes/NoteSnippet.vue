<script setup lang="ts">
import { computed } from "vue";

import LinkTargetName from "@/components/notes/LinkTargetName.vue";
import { snippetParts } from "@/features/notes/backlinks";

// Full class literals, so Tailwind sees each clamp at build time.
const CLAMP = {
  2: "line-clamp-2",
  4: "line-clamp-4",
} as const;

// The line of a note a link sits on, as stored by the server's link index.
const { snippet, lines = 2 } = defineProps<{ snippet: string; lines?: keyof typeof CLAMP }>();

const parts = computed(() => snippetParts(snippet));
</script>

<template>
  <p class="text-sm wrap-break-word text-gray-600 dark:text-gray-400" :class="CLAMP[lines]">
    <template v-for="(part, index) in parts" :key="index">
      <span v-if="part.type === 'text'">{{ part.text }}</span>

      <span v-else-if="part.label" class="font-medium text-gray-900 dark:text-gray-100">
        {{ part.label }}
      </span>

      <LinkTargetName v-else :link="part.ref" />
    </template>
  </p>
</template>
