<script setup lang="ts">
import { shallowRef, watch } from "vue";

const { digit, motionActive = true } = defineProps<{ digit: string; motionActive?: boolean }>();

const previous = shallowRef<string>();

watch([() => digit, () => motionActive], ([next, active], [old, wasActive]) => {
  previous.value = active && wasActive && next !== old ? old : undefined;
});
</script>

<template>
  <b
    class="clock-flip-card border border-gray-200/70 bg-gray-100 dark:border-gray-700/70 dark:bg-gray-800"
  >
    <span
      :key="digit"
      class="clock-flip-digit"
      :class="{ 'clock-flip-digit--changing': motionActive && previous !== undefined }"
      >{{ digit }}</span
    >

    <template v-if="motionActive && previous !== undefined">
      <span
        :key="`old-bottom-${digit}`"
        class="clock-flip-leaf clock-flip-leaf--old-bottom"
        :data-digit="previous"
      />
      <span
        :key="`old-top-${digit}`"
        class="clock-flip-leaf clock-flip-leaf--old-top"
        :data-digit="previous"
      />
      <span
        :key="`new-bottom-${digit}`"
        class="clock-flip-leaf clock-flip-leaf--new-bottom"
        :data-digit="digit"
      />
    </template>
  </b>
</template>

<style scoped>
.clock-flip-card {
  position: relative;
  display: grid;
  place-items: center;
  width: 0.8em;
  height: 1.1em;
  line-height: 1;
  border-radius: 0.15em;
  perspective: 12rem;
}

.clock-flip-card::after {
  position: absolute;
  top: 50%;
  right: 0;
  left: 0;
  height: 2px;
  background: var(--ui-border);
  content: "";
  transform: translateY(-50%);
  z-index: 4;
}

.clock-flip-digit {
  display: block;
}

.clock-flip-leaf {
  position: absolute;
  right: 0;
  left: 0;
  height: 50%;
  overflow: hidden;
  background: inherit;
  backface-visibility: hidden;
}

.clock-flip-leaf::before {
  position: absolute;
  inset-inline: 0;
  display: grid;
  place-items: center;
  height: 200%;
  content: attr(data-digit);
}

.clock-flip-leaf--old-top {
  top: 0;
  z-index: 3;
  border-radius: inherit;
  border-bottom-right-radius: 0;
  border-bottom-left-radius: 0;
  transform-origin: bottom;
  animation: clock-flip-top 350ms ease-in both;
}

.clock-flip-leaf--old-bottom,
.clock-flip-leaf--new-bottom {
  bottom: 0;
  border-radius: inherit;
  border-top-right-radius: 0;
  border-top-left-radius: 0;
}

.clock-flip-leaf--old-bottom::before,
.clock-flip-leaf--new-bottom::before {
  top: -100%;
}

.clock-flip-leaf--old-bottom {
  z-index: 1;
  animation: clock-flip-old-bottom 350ms step-end both;
}

.clock-flip-leaf--new-bottom {
  z-index: 2;
  transform-origin: top;
  animation: clock-flip-bottom 350ms ease-out both;
}

@keyframes clock-flip-top {
  0% {
    transform: rotateX(0);
    opacity: 1;
  }

  49.9% {
    opacity: 1;
  }

  50%,
  100% {
    transform: rotateX(-90deg);
    opacity: 0;
  }
}

@keyframes clock-flip-old-bottom {
  0% {
    opacity: 1;
  }

  100% {
    opacity: 0;
  }
}

@keyframes clock-flip-bottom {
  0%,
  50% {
    transform: rotateX(90deg);
  }

  100% {
    transform: rotateX(0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .clock-flip-leaf {
    display: none;
  }
}
</style>
