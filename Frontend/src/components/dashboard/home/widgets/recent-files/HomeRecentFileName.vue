<script setup lang="ts">
import { computed } from "vue";

import { getFileExtension } from "@/utils/file-name.utils";

const { fileName } = defineProps<{ fileName: string }>();

const parts = computed(() => {
  const characters = Array.from(fileName);
  const extension = getFileExtension(fileName);
  const extensionLength = extension ? Array.from(extension).length + 1 : 0;
  const tailLength = Math.max(extensionLength, Math.min(12, Math.floor(characters.length / 2)));
  const splitAt = characters.length - tailLength;

  return {
    start: characters.slice(0, splitAt).join(""),
    end: characters.slice(splitAt).join(""),
  };
});
</script>

<template>
  <span class="middle-file-name" :title="fileName" :aria-label="fileName">
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
  font-size: 12px;
  font-weight: 500;
}

.name-start {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
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
}
</style>
