<script setup lang="ts">
import { HomeClockStyle } from "@/enums/home-widget";

const { clockStyle } = defineProps<{ clockStyle: HomeClockStyle }>();

const rings = [
  { radius: 40, progress: 84, color: "#5B7FA6" },
  { radius: 30, progress: 14, color: "#7A9E87" },
  { radius: 20, progress: 60, color: "#C9A84C" },
];
</script>

<template>
  <span class="flex size-24 shrink-0 items-center justify-center" aria-hidden="true">
    <svg v-if="clockStyle === HomeClockStyle.Analog" viewBox="0 0 64 64" class="size-20">
      <circle cx="32" cy="32" r="29" fill="none" stroke="currentColor" stroke-width="2" />
      <path d="M32 12V8 M52 32H56 M32 52V56 M12 32H8" stroke="currentColor" stroke-width="2" />
      <path
        d="M21 21L32 32L46 18"
        fill="none"
        stroke="currentColor"
        stroke-width="3"
        stroke-linecap="round"
      />
      <rect x="26" y="39" width="12" height="6" rx="2" fill="none" stroke="currentColor" />
      <circle cx="32" cy="32" r="3" fill="currentColor" />
    </svg>

    <span
      v-else-if="clockStyle === HomeClockStyle.Material"
      class="flex flex-col items-center gap-2 rounded-2xl bg-gray-100 p-4 text-2xl leading-none font-semibold tracking-tight tabular-nums dark:bg-gray-800"
    >
      <span>10</span>
      <span class="size-1 rounded-full bg-gray-500 dark:bg-gray-400" />
      <span class="text-gray-600 dark:text-gray-400">08</span>
    </span>

    <span v-else-if="clockStyle === HomeClockStyle.Flip" class="flex flex-col gap-2">
      <span v-for="(digits, row) in ['10', '08']" :key="row" class="flex gap-2">
        <span
          v-for="(digit, column) in digits"
          :key="column"
          class="relative flex h-10 w-8 items-center justify-center rounded-lg border border-gray-200/70 bg-gray-100 text-2xl leading-none font-bold tabular-nums dark:border-gray-700/70 dark:bg-gray-800"
        >
          {{ digit }}
          <span class="absolute inset-x-0 top-1/2 border-t border-gray-300 dark:border-gray-700" />
        </span>
      </span>
    </span>

    <svg v-else-if="clockStyle === HomeClockStyle.Rings" viewBox="0 0 96 96" class="size-24">
      <g v-for="ring in rings" :key="ring.radius" fill="none" stroke-width="6">
        <circle cx="48" cy="48" :r="ring.radius" class="stroke-gray-200 dark:stroke-gray-800" />
        <circle
          cx="48"
          cy="48"
          :r="ring.radius"
          :stroke="ring.color"
          stroke-linecap="round"
          pathLength="100"
          :stroke-dasharray="`${ring.progress} 100`"
          transform="rotate(-90 48 48)"
        />
      </g>
      <text
        x="48"
        y="48"
        text-anchor="middle"
        dominant-baseline="central"
        fill="currentColor"
        font-size="10"
        font-weight="600"
      >
        10:08
      </text>
    </svg>

    <span v-else-if="clockStyle === HomeClockStyle.Words" class="flex w-full flex-col gap-2 p-2">
      <span class="text-xs text-gray-600 dark:text-gray-400">It's</span>
      <span class="text-base leading-tight font-semibold">ten past<br />ten</span>
      <span class="text-xs tabular-nums text-gray-600 dark:text-gray-400">10:08</span>
    </span>

    <span v-else class="flex flex-col gap-2 p-2">
      <span class="text-xs text-gray-600 dark:text-gray-400">Good morning</span>
      <span class="text-2xl leading-none font-semibold tracking-tight tabular-nums">10:08</span>
      <span class="text-xs text-gray-600 dark:text-gray-400">Wed, Oct 7</span>
    </span>
  </span>
</template>
