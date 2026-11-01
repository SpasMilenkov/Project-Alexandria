<template>
  <span
    class="relative inline-grid shrink-0 place-items-center transition-transform duration-200 ease-out"
    :class="{ 'scale-105': emphasized }"
    :style="{ width: `${size}px`, height: `${size}px` }"
  >
    <svg
      class="absolute inset-0 -rotate-90"
      :width="size"
      :height="size"
      :viewBox="`0 0 ${size} ${size}`"
      aria-hidden="true"
    >
      <circle
        :cx="center"
        :cy="center"
        :r="radius"
        fill="none"
        stroke="var(--ui-border-accented)"
        :stroke-width="stroke"
        opacity="0.6"
      />

      <circle
        v-for="arc in arcs"
        :key="arc.key"
        :cx="center"
        :cy="center"
        :r="radius"
        fill="none"
        :stroke="arc.color"
        :stroke-width="arcWidth(arc.key)"
        :stroke-dasharray="`${arc.length * circumference} ${circumference}`"
        :stroke-dashoffset="-arc.offset * circumference"
        :opacity="arcOpacity(arc.key)"
        class="transition-[stroke-width,opacity] duration-150 ease-out"
      />

      <circle
        v-for="arc in arcs"
        :key="`${arc.key}-hit`"
        :cx="center"
        :cy="center"
        :r="radius"
        fill="none"
        stroke="transparent"
        :stroke-width="stroke * 3"
        :stroke-dasharray="`${arc.length * circumference} ${circumference}`"
        :stroke-dashoffset="-arc.offset * circumference"
        pointer-events="stroke"
        @mouseenter="emit('hoverSegment', arc.key)"
        @mouseleave="emit('hoverSegment', null)"
      />
    </svg>

    <slot />
  </span>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { RingArc } from "@/utils/storage-ring";

const {
  arcs,
  size = 56,
  stroke = 3.5,
  activeKey = null,
  emphasized = false,
} = defineProps<{
  arcs: RingArc[];
  size?: number;
  stroke?: number;
  activeKey?: string | null;
  emphasized?: boolean;
}>();

const emit = defineEmits<{
  hoverSegment: [key: string | null];
}>();

const center = computed(() => size / 2);
const radius = computed(() => (size - stroke * 1.6) / 2);
const circumference = computed(() => 2 * Math.PI * radius.value);

const arcWidth = (key: string) => {
  if (key === activeKey) return stroke * 1.6;
  if (emphasized) return stroke * 1.25;

  return stroke;
};

const arcOpacity = (key: string) => {
  if (activeKey === null || key === activeKey) return 1;

  return 0.35;
};
</script>
