<script setup lang="ts">
import { computed } from "vue";

import { type HomeClockReadout, homeClockWords } from "@/utils/home-clock";

const { clock, faceOnly = false } = defineProps<{
  clock: HomeClockReadout;
  faceOnly?: boolean;
}>();

const words = computed(() => homeClockWords(clock.hour24, clock.minuteNumber));
</script>

<template>
  <div
    class="clock-words-face"
    :class="{
      'clock-words-face--face-only': faceOnly,
      'clock-words-face--seconds': Boolean(clock.second),
    }"
    aria-hidden="true"
  >
    <p
      v-if="!faceOnly"
      class="clock-words-intro text-xs font-semibold text-gray-600 dark:text-gray-400"
    >
      It's
    </p>

    <p class="clock-words-phrase font-bold tracking-tight text-gray-900 dark:text-gray-100">
      <span>{{ words.lead }}</span
      >{{ " " }}
      <span class="text-gray-600 dark:text-gray-400">{{ words.ending }}</span>
    </p>

    <p
      v-if="!faceOnly"
      class="clock-words-period text-xs font-semibold text-gray-600 dark:text-gray-400"
    >
      {{ words.period }}
    </p>

    <p
      v-if="!faceOnly || clock.second"
      class="clock-words-exact text-xs text-gray-600 tabular-nums dark:text-gray-400"
    >
      {{ clock.time }}
    </p>
  </div>
</template>

<style scoped>
.clock-words-face {
  display: flex;
  flex-direction: column;
  align-items: var(--clock-items);
  gap: 0.5rem;
  width: 100%;
}

.clock-words-phrase {
  width: 100%;
  text-align: var(--clock-face-text);
  font-size: max(1rem, min(12cqi, calc((100cqb - var(--clock-caption-reserve) - 4rem) / 2.2)));
  line-height: 1.1;
  overflow-wrap: anywhere;
}

@container clock-body (max-height: 180px) {
  .clock-words-face {
    gap: 0.25rem;
  }

  .clock-words-intro {
    display: none;
  }

  .clock-words-phrase {
    font-size: max(1rem, min(9cqi, calc((100cqb - var(--clock-caption-reserve) - 2.5rem) / 2.2)));
  }
}

@container clock-body (max-height: 100px) {
  .clock-words-period {
    display: none;
  }

  .clock-words-phrase {
    font-size: 0.875rem;
  }
}

.clock-words-face--face-only {
  justify-content: var(--clock-block-items);
  gap: 0;
  height: 100%;
}

.clock-words-face--face-only .clock-words-phrase {
  font-size: max(1rem, min(15cqi, 30cqb));
}

.clock-words-face--face-only.clock-words-face--seconds {
  gap: 0.5rem;
}

.clock-words-face--face-only.clock-words-face--seconds .clock-words-phrase {
  font-size: max(1rem, min(15cqi, calc((100cqb - 1.5rem) / 3.3)));
}
</style>
