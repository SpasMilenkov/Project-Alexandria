<script setup lang="ts">
import { useElementHover, usePreferredReducedMotion, useResizeObserver } from "@vueuse/core";
import { computed, nextTick, onMounted, ref, watch } from "vue";

const { text } = defineProps<{ text: string }>();

// Scroll speed in px per second, plus a pause at each end built into the keyframes.
const SPEED = 40;
const MIN_DURATION_S = 6;

// Matches the 15% hold at each end of the keyframes.
const END_HOLD = 0.15;

const box = ref<HTMLElement | null>(null);
const overflow = ref(0);
const motion = usePreferredReducedMotion();
const hovered = useElementHover(box);

const measure = () => {
  if (!box.value) return;

  overflow.value = Math.max(0, box.value.scrollWidth - box.value.clientWidth);
};

useResizeObserver(box, measure);
onMounted(measure);

watch(
  () => text,
  async () => {
    await nextTick();
    measure();
  },
);

// Hover parks the title at its start so the opening words can be read at rest.
const scrolling = computed(() => overflow.value > 0 && motion.value !== "reduce" && !hovered.value);

const scrollStyle = computed(() => {
  if (!scrolling.value) return undefined;

  const duration = Math.max(MIN_DURATION_S, overflow.value / SPEED + 4);

  // Skip the opening hold so the title starts moving right away; the turnarounds keep theirs.
  return {
    "--marquee-distance": `-${overflow.value}px`,
    animationDuration: `${duration}s`,
    animationDelay: `-${(duration * END_HOLD).toFixed(2)}s`,
  };
});
</script>

<template>
  <span
    ref="box"
    class="block overflow-hidden whitespace-nowrap"
    :class="{ 'text-ellipsis': !scrolling }"
    :title="text"
  >
    <span :class="{ 'marquee inline-block': scrolling }" :style="scrollStyle">{{ text }}</span>
  </span>
</template>

<style scoped>
.marquee {
  animation: marquee linear infinite alternate;
}

@keyframes marquee {
  0%,
  15% {
    transform: translateX(0);
  }

  85%,
  100% {
    transform: translateX(var(--marquee-distance));
  }
}
</style>
