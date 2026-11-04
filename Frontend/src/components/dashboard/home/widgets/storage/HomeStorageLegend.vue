<script setup lang="ts">
import { ref } from "vue";

import type { HomeStorageCategory } from "@/utils/home-file-widgets";

import { formatBytes } from "@/utils/size.utils";

defineProps<{ categories: HomeStorageCategory[] }>();

const element = ref<HTMLElement | null>(null);
defineExpose({ element });
</script>

<template>
  <div ref="element">
    <div v-for="category in categories" :key="category.label" class="legend-row">
      <i :style="{ backgroundColor: category.color }" />
      <span>{{ category.label }}</span>
      <b>{{ formatBytes(category.bytes) }}</b>
    </div>
  </div>
</template>

<style scoped>
.legend-row {
  display: flex;
  align-items: center;
  gap: 8px;
  height: 2em;
  flex-shrink: 0;
  font-size: inherit;
  color: var(--ui-text-muted);
}

.legend-row i {
  width: 0.667em;
  height: 0.667em;
  border-radius: 2px;
  flex-shrink: 0;
}

.legend-row span {
  flex: 1;
  min-width: 0;
}

.legend-row b {
  color: var(--ui-text);
  font-weight: 500;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
</style>
