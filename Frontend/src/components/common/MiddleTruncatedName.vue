<script setup lang="ts">
import { computed } from "vue";

import { getFileExtension } from "@/utils/files/file-name";

// Truncates in the middle so the end of a name (and a file's extension) stays readable.
// Typography is inherited from the caller so it fits any list or widget.
const { name, keepExtension = true } = defineProps<{
  name: string;
  keepExtension?: boolean;
}>();

const TAIL_MAX = 12;

const parts = computed(() => {
  const characters = Array.from(name);
  const extension = keepExtension ? getFileExtension(name) : "";
  const extensionLength = extension ? Array.from(extension).length + 1 : 0;
  const tailLength = Math.max(
    extensionLength,
    Math.min(TAIL_MAX, Math.floor(characters.length / 2)),
  );
  const splitAt = characters.length - tailLength;

  return {
    end: characters.slice(splitAt).join(""),
    start: characters.slice(0, splitAt).join(""),
  };
});
</script>

<template>
  <span class="middle-file-name" :title="name" :aria-label="name">
    <span class="name-start">{{ parts.start }}</span>
    <span class="name-end"
      ><span>{{ parts.end }}</span></span
    >
  </span>
</template>

<style scoped>
.middle-file-name {
  display: flex;
  min-width: 0;
  overflow: hidden;
  white-space: nowrap;
}

/* Flex items drop leading and trailing spaces, so a split landing on a space would glue the words together. */
.name-start {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: pre;
}

.name-end {
  display: flex;
  flex-shrink: 0;
  justify-content: flex-end;
  max-width: calc(100% - 1.5em);
  overflow: hidden;
}

.name-end > span {
  flex-shrink: 0;
  white-space: pre;
}
</style>
