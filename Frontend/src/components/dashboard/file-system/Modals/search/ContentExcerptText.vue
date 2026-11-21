<template>
  <!-- Segments carry their own spacing, so nothing may sit between them. -->
  <span
    ><component
      :is="segment.isMatch ? 'mark' : 'span'"
      v-for="(segment, index) in segments"
      :key="index"
      :class="{ [MARK_CLASS]: segment.isMatch }"
      >{{ segment.text }}</component
    ></span
  >
</template>

<script setup lang="ts">
import type { ContentMatchSegment } from "@/api/file";

// A highlighter wash, apart from the accent, which stays for selection. Plain text
// inside, so nothing from a document is rendered as markup.
const MARK_CLASS =
  "rounded-sm bg-yellow-300/45 font-medium text-gray-900 dark:bg-yellow-300/25 dark:text-gray-100";

defineProps<{ segments: ContentMatchSegment[] }>();
</script>
