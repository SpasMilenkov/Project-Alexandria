<script setup lang="ts">
import { computed } from "vue";

import {
  type HomeClockReadout,
  homeClockNumbers,
  homeClockRingProgress,
  type useHomeClockTiming,
} from "@/utils/home-clock";

const {
  clock,
  timing,
  timeZone = undefined,
  faceOnly = false,
} = defineProps<{
  clock: HomeClockReadout;
  timing: ReturnType<typeof useHomeClockTiming>;
  timeZone?: string;
  faceOnly?: boolean;
}>();

const numbers = computed(() => homeClockNumbers(timing.timestamp.value, timeZone));

const progress = computed(() => {
  const { hour24, minuteNumber, secondNumber } = numbers.value;
  let second = secondNumber;

  if (timing.motionActive.value)
    second += Math.max(0, timing.frameTimestamp.value - timing.timestamp.value) / 1000;

  return homeClockRingProgress(hour24, minuteNumber, second);
});

const rings = computed(() => [
  { name: "Hour", radius: 88, color: "#5B7FA6", progress: progress.value.hour, value: clock.hour },
  {
    name: "Minute",
    radius: 70,
    color: "#7A9E87",
    progress: progress.value.minute,
    value: clock.minute,
  },
  {
    name: "Second",
    radius: 52,
    color: "#C9A84C",
    progress: progress.value.second,
    value: numbers.value.secondNumber,
  },
]);
</script>

<template>
  <div
    class="clock-rings-face"
    :class="{ 'clock-rings-face--face-only': faceOnly }"
    aria-hidden="true"
  >
    <svg
      class="clock-rings-dial shrink-0 text-gray-900 dark:text-gray-100"
      viewBox="0 0 200 200"
      shape-rendering="geometricPrecision"
    >
      <template v-for="ring in rings" :key="ring.name">
        <circle
          cx="100"
          cy="100"
          :r="ring.radius"
          fill="none"
          class="stroke-gray-200 dark:stroke-gray-800"
          stroke-width="12"
        />

        <circle
          class="clock-ring-progress"
          :data-ring="ring.name.toLowerCase()"
          cx="100"
          cy="100"
          :r="ring.radius"
          fill="none"
          :stroke="ring.color"
          stroke-width="12"
          stroke-linecap="round"
          pathLength="100"
          :stroke-dasharray="`${ring.progress} 100`"
          transform="rotate(-90 100 100)"
        />
      </template>

      <text
        x="100"
        :y="clock.period ? 96 : 100"
        text-anchor="middle"
        dominant-baseline="central"
        :font-size="clock.second ? 18 : 24"
        font-weight="700"
        fill="currentColor"
        class="tabular-nums"
      >
        {{ clock.digits }}
      </text>

      <text
        v-if="clock.period"
        x="100"
        y="119"
        text-anchor="middle"
        dominant-baseline="central"
        font-size="12"
        font-weight="600"
        fill="currentColor"
        class="text-gray-600 dark:text-gray-400"
      >
        {{ clock.period }}
      </text>
    </svg>

    <div v-if="!faceOnly" class="clock-rings-legend text-gray-600 dark:text-gray-400">
      <div v-for="ring in rings" :key="ring.name" class="clock-rings-legend-row">
        <span class="clock-rings-swatch" :style="{ backgroundColor: ring.color }"></span>
        <span>{{ ring.name }}</span>
        <b class="text-gray-900 tabular-nums dark:text-gray-100">{{ ring.value }}</b>
      </div>
    </div>
  </div>
</template>

<style scoped>
.clock-rings-face {
  display: flex;
  align-items: center;
  justify-content: var(--clock-items);
  gap: 1rem;
  width: 100%;
}

.clock-rings-dial {
  width: max(2rem, min(100cqi, calc(100cqb - var(--clock-caption-reserve))));
  max-width: 100%;
  aspect-ratio: 1;
}

.clock-rings-legend {
  display: none;
  font-size: inherit;
  line-height: 1.5;
}

.clock-rings-legend-row {
  display: grid;
  grid-template-columns: 0.667em auto minmax(2ch, auto);
  align-items: center;
  gap: 0.667em;
  margin-block: 0.667em;
}

.clock-rings-legend-row b {
  text-align: right;
}

.clock-rings-swatch {
  width: 0.667em;
  height: 0.667em;
  border-radius: 2px;
}

@container clock-body (min-width: 340px) and (min-height: 180px) and (min-aspect-ratio: 3 / 2) {
  .clock-rings-dial {
    width: max(2rem, min(50cqi, calc(100cqb - var(--clock-caption-reserve))));
  }

  .clock-rings-legend {
    display: block;
  }
}

.clock-rings-face--face-only {
  align-items: var(--clock-block-items);
  gap: 0;
  height: 100%;
}

.clock-rings-face--face-only .clock-rings-dial {
  width: min(100cqi, 100cqb);
}
</style>
