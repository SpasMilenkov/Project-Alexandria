<script setup lang="ts">
import { computed, useId } from "vue";

import { HomeDailyCoverStyle } from "@/enums/home-widget";
import { homeDailyCover } from "@/utils/home-daily-cover";

const { day, coverStyle } = defineProps<{ day: string; coverStyle: HomeDailyCoverStyle }>();

const id = `daily-${useId().replace(/:/gu, "")}`;
const art = computed(() =>
  homeDailyCover(day, coverStyle === HomeDailyCoverStyle.DatePoster ? 3 : 0),
);
</script>

<template>
  <svg
    viewBox="0 0 200 200"
    preserveAspectRatio="xMidYMid slice"
    aria-hidden="true"
    class="daily-art block h-full w-full"
  >
    <defs>
      <pattern :id="`${id}-dots`" width="5" height="5" patternUnits="userSpaceOnUse">
        <circle cx="2.5" cy="2.5" r="1.1" :fill="art.palette[2]" />
      </pattern>
      <filter :id="`${id}-grain`">
        <feTurbulence type="fractalNoise" baseFrequency=".9" numOctaves="2" :seed="art.seed % 99" />
        <feColorMatrix values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 .5 0" />
      </filter>
      <linearGradient :id="`${id}-sky`" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" :stop-color="art.palette[2]" />
        <stop offset=".65" :stop-color="art.palette[1]" />
        <stop offset="1" :stop-color="art.palette[3]" />
      </linearGradient>
    </defs>

    <rect width="200" height="200" :fill="art.palette[3]" />

    <template v-if="coverStyle === HomeDailyCoverStyle.Bauhaus">
      <g
        v-for="(cell, index) in art.cells"
        :key="index"
        :transform="`translate(${cell.x} ${cell.y})`"
        :fill="cell.fill"
      >
        <rect v-if="cell.kind === 0" width="50" height="50" />
        <circle v-else-if="cell.kind === 1" cx="25" cy="25" r="25" />
        <path v-else-if="cell.kind === 2" d="M0 50A50 50 0 0 1 50 0V50Z" />
        <path v-else-if="cell.kind === 3" d="M0 0H50V50Z" />
        <circle v-else cx="25" cy="25" r="12" />
      </g>
    </template>

    <template v-else-if="coverStyle === HomeDailyCoverStyle.Horizon">
      <rect width="200" height="200" :fill="`url(#${id}-sky)`" />
      <circle :cx="art.sun.x" :cy="art.sun.y" :r="art.sun.radius" :fill="art.palette[0]" />
      <path
        v-for="(ridge, index) in art.ridges"
        :key="index"
        :d="ridge.path"
        :fill="art.palette[2]"
        :fill-opacity="ridge.opacity"
      />
    </template>

    <template v-else>
      <g class="daily-inks">
        <circle
          v-for="(circle, index) in art.circles"
          :key="index"
          :cx="circle.x"
          :cy="circle.y"
          :r="circle.radius"
          :fill="circle.fill"
        />
      </g>
      <circle
        :cx="art.halftone.x"
        :cy="art.halftone.y"
        :r="art.halftone.radius"
        :fill="`url(#${id}-dots)`"
      />
      <rect width="200" height="200" :filter="`url(#${id}-grain)`" opacity=".35" />
    </template>
  </svg>
</template>

<style scoped>
.daily-art {
  isolation: isolate;
}

.daily-inks circle {
  mix-blend-mode: multiply;
}
</style>
