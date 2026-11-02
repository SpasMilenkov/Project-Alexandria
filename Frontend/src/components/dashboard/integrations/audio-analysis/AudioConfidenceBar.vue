<script setup lang="ts">
import { computed, onMounted, ref } from "vue";

import { formatConfidence } from "@/utils/audio-taxonomy.utils";

const {
  value,
  align = "start",
  color = undefined,
  delay = 0,
  size = "md",
} = defineProps<{
  value: number;
  align?: "start" | "end";
  color?: string;
  delay?: number;
  size?: "sm" | "md";
}>();

// Starts empty and grows on the next frame; reduced motion drops the transition, so it just appears.
const isRevealed = ref(false);

onMounted(() => {
  requestAnimationFrame(() => {
    isRevealed.value = true;
  });
});

const fillStyle = computed(() => ({
  backgroundColor: color,
  transitionDelay: `${delay}ms`,
  width: isRevealed.value ? formatConfidence(value) : "0%",
}));
</script>

<template>
  <div
    class="flex overflow-hidden rounded-full bg-black/8 dark:bg-white/8"
    :class="[size === 'sm' ? 'h-1' : 'h-1.5', align === 'end' ? 'justify-end' : 'justify-start']"
    aria-hidden="true"
  >
    <div
      class="h-full shrink-0 rounded-full transition-[width] duration-700 ease-out motion-reduce:transition-none"
      :class="{ 'bg-gray-400 dark:bg-gray-500': !color }"
      :style="fillStyle"
    />
  </div>
</template>
