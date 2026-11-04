<script setup lang="ts">
import { computed } from "vue";

import {
  homeClockDateWindow,
  homeClockHandAngles,
  homeClockNumbers,
  type useHomeClockTiming,
} from "@/utils/home-clock";

const {
  timing,
  timeZone = undefined,
  faceOnly = false,
} = defineProps<{
  timing: ReturnType<typeof useHomeClockTiming>;
  timeZone?: string;
  faceOnly?: boolean;
}>();

const ticks = Array.from({ length: 60 }, (_, index) => index);
const numerals = [
  { label: "12", x: 100, y: 42 },
  { label: "3", x: 158, y: 100 },
  { label: "6", x: 100, y: 158 },
  { label: "9", x: 42, y: 100 },
];

const numbers = computed(() => homeClockNumbers(timing.timestamp.value, timeZone));

const minuteTimestamp = computed(() => Math.floor(timing.timestamp.value / 60_000) * 60_000);

const dateWindow = computed(() => homeClockDateWindow(minuteTimestamp.value, timeZone));

const hands = computed(() => {
  const { hour24, minuteNumber, secondNumber } = numbers.value;
  const angles = homeClockHandAngles(hour24, minuteNumber, secondNumber);

  if (timing.motionActive.value)
    angles.second += (Math.max(0, timing.frameTimestamp.value - timing.timestamp.value) / 1000) * 6;

  return angles;
});
</script>

<template>
  <svg
    class="clock-dial shrink-0 text-gray-900 dark:text-gray-100"
    viewBox="0 0 200 200"
    shape-rendering="geometricPrecision"
    aria-hidden="true"
  >
    <circle
      cx="100"
      cy="100"
      r="98"
      stroke-width="2"
      class="fill-gray-100 stroke-gray-200 dark:fill-gray-800 dark:stroke-gray-700"
    />
    <line
      v-for="tick in ticks"
      :key="tick"
      x1="100"
      :y1="tick % 5 === 0 ? 10 : 14"
      x2="100"
      y2="20"
      :transform="`rotate(${tick * 6} 100 100)`"
      stroke="currentColor"
      :stroke-width="tick % 5 === 0 ? 3 : 1.5"
      stroke-linecap="round"
    />

    <text
      v-for="numeral in numerals"
      :key="numeral.label"
      :x="numeral.x"
      :y="numeral.y"
      text-anchor="middle"
      dominant-baseline="central"
      font-size="16"
      font-weight="600"
      fill="currentColor"
    >
      {{ numeral.label }}
    </text>

    <rect
      v-if="!faceOnly"
      x="76"
      y="116"
      width="48"
      height="18"
      rx="5"
      class="fill-gray-50 stroke-gray-200 dark:fill-gray-900 dark:stroke-gray-700"
    />
    <text
      v-if="!faceOnly"
      class="clock-date-window"
      x="100"
      y="125.5"
      text-anchor="middle"
      dominant-baseline="central"
      font-size="10"
      font-weight="700"
      fill="currentColor"
    >
      {{ dateWindow }}
    </text>

    <line
      class="clock-hour-hand"
      x1="100"
      y1="108"
      x2="100"
      y2="58"
      :transform="`rotate(${hands.hour} 100 100)`"
      stroke="currentColor"
      stroke-width="7"
      stroke-linecap="round"
    />
    <line
      class="clock-minute-hand"
      x1="100"
      y1="110"
      x2="100"
      y2="32"
      :transform="`rotate(${hands.minute} 100 100)`"
      stroke="currentColor"
      stroke-width="4"
      stroke-linecap="round"
    />
    <line
      class="clock-second-hand text-gray-600 dark:text-gray-400"
      x1="100"
      y1="118"
      x2="100"
      y2="22"
      :transform="`rotate(${hands.second} 100 100)`"
      stroke="currentColor"
      stroke-width="2.5"
      vector-effect="non-scaling-stroke"
      stroke-linecap="round"
    />
    <circle cx="100" cy="100" r="6" fill="currentColor" />
  </svg>
</template>

<style scoped>
.clock-second-hand {
  /* Soften pixel-boundary shimmer while the thin hand sweeps. */
  filter: blur(0.3px);
}
</style>
