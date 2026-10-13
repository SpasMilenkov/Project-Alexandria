<script setup lang="ts">
import { useDocumentVisibility, useElementVisibility } from "@vueuse/core";
import { computed, ref } from "vue";

const { fill, animated = true } = defineProps<{ fill: number; animated?: boolean }>();

const element = ref<HTMLElement | null>(null);
const visible = useElementVisibility(element);
const documentVisibility = useDocumentVisibility();

const running = computed(() => animated && visible.value && documentVisibility.value === "visible");
</script>

<template>
  <div ref="element" class="tank" aria-hidden="true">
    <div v-if="fill > 0" class="tank-liquid" :style="{ height: `${fill}%` }">
      <svg
        class="tank-wave"
        viewBox="0 0 200 12"
        preserveAspectRatio="none"
        :style="{ animationPlayState: running ? 'running' : 'paused' }"
      >
        <path d="M0 6Q25 0 50 6T100 6T150 6T200 6V12H0Z" />
      </svg>
    </div>
    <div class="tank-marks" />
  </div>
</template>

<style scoped>
.tank-liquid {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  background: var(--liquid);
}
.tank-wave {
  position: absolute;
  top: -8px;
  left: 0;
  width: 200%;
  height: 10px;
  fill: var(--liquid);
  animation: storage-wave 4s linear infinite;
}
.tank-marks {
  position: absolute;
  inset: 0;
  background: repeating-linear-gradient(
    to top,
    transparent 0,
    transparent calc(25% - 1px),
    var(--ui-border) calc(25% - 1px),
    var(--ui-border) 25%
  );
}
@keyframes storage-wave {
  to {
    transform: translateX(-50%);
  }
}

@media (prefers-reduced-motion: reduce) {
  .tank-wave {
    animation: none;
  }
}
</style>
