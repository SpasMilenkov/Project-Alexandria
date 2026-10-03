<script setup lang="ts">
import { computed } from "vue";

import { formatConfidence } from "@/utils/audio-taxonomy.utils";

const {
  value,
  accent = false,
  align = "start",
  size = "md",
} = defineProps<{
  value: number;
  accent?: boolean;
  align?: "start" | "end";
  size?: "sm" | "md";
}>();

const fillStyle = computed(() => {
  const width = formatConfidence(value);
  return accent ? { width, backgroundColor: "var(--ui-primary)" } : { width };
});
</script>

<template>
  <div
    class="flex overflow-hidden rounded-full bg-black/8 dark:bg-white/8"
    :class="[size === 'sm' ? 'h-1' : 'h-1.5', align === 'end' ? 'justify-end' : 'justify-start']"
    aria-hidden="true"
  >
    <div
      class="h-full shrink-0 rounded-full"
      :class="{ 'bg-gray-400 dark:bg-gray-500': !accent }"
      :style="fillStyle"
    />
  </div>
</template>
