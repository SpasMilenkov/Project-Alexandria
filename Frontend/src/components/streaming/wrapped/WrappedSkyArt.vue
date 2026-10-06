<template>
  <svg
    :viewBox="viewBox"
    preserveAspectRatio="xMidYMid slice"
    class="absolute inset-0 block h-full w-full"
    aria-hidden="true"
  >
    <defs>
      <linearGradient :id="`${id}-sky`" x1="0" y1="0" x2="0" y2="1">
        <stop
          v-for="stop in cfg.skyStops"
          :key="stop.offset"
          :offset="stop.offset"
          :stop-color="stop.color"
        />
      </linearGradient>

      <linearGradient :id="`${id}-scrim`" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%" stop-color="#000" stop-opacity="0" />
        <stop offset="100%" stop-color="#000" :stop-opacity="cfg.scrimOpacity" />
      </linearGradient>

      <pattern :id="`${id}-grain`" width="6" height="6" patternUnits="userSpaceOnUse">
        <circle cx="3" cy="3" r="0.96" :fill="cfg.grainColor" />
      </pattern>
    </defs>

    <rect width="900" height="340" :fill="`url(#${id}-sky)`" />

    <circle
      v-for="(star, index) in stars"
      :key="index"
      :cx="star.x"
      :cy="star.y"
      :r="star.r"
      fill="#fff"
      :opacity="star.opacity"
    />

    <circle :cx="cfg.sunX" :cy="cfg.sunY" :r="cfg.glowR2" :fill="cfg.glowColor" opacity="0.16" />
    <circle :cx="cfg.sunX" :cy="cfg.sunY" :r="cfg.glowR1" :fill="cfg.glowColor" opacity="0.32" />
    <circle :cx="cfg.sunX" :cy="cfg.sunY" :r="cfg.sunR" :fill="cfg.sunColor" />
    <path :d="back" :fill="cfg.ridgeBack" opacity="0.85" />
    <path :d="front" :fill="cfg.ridgeFront" />
    <rect width="900" height="340" :fill="`url(#${id}-grain)`" :opacity="cfg.grainOpacity" />
    <rect y="142.8" width="900" height="197.2" :fill="`url(#${id}-scrim)`" />
  </svg>
</template>

<script setup lang="ts">
import { useMediaQuery } from "@vueuse/core";
import { computed, inject, useId } from "vue";

import { wrappedPaletteKey } from "@/composables/useWrappedPalette";
import { WrappedTimeScene } from "@/enums/wrapped-story";
import { getWrappedSkyPreset, skyRidge } from "@/utils/wrapped-sky.utils";

const props = defineProps<{ scene: WrappedTimeScene; compact?: boolean }>();
const id = `sky-${useId().replace(/:/g, "")}`;
const mobile = useMediaQuery("(max-width: 639px)");

const palette = inject(
  wrappedPaletteKey,
  computed(() => null),
);

const cfg = computed(() => getWrappedSkyPreset(props.scene, palette.value));

const viewBox = computed(() =>
  mobile.value || props.compact
    ? `${Math.max(0, Math.min(560, cfg.value.sunX - 170))} 0 340 340`
    : "0 0 900 340",
);

const back = computed(() =>
  skyRidge(
    cfg.value.baseY - cfg.value.ridgeGap,
    26,
    2.6,
    cfg.value.seedA,
    14,
    4.4,
    cfg.value.seedB,
  ),
);

const front = computed(() =>
  skyRidge(cfg.value.baseY, 34, 2.1, cfg.value.seedC, 16, 5.1, cfg.value.seedD),
);

const stars = computed(() => {
  if (!cfg.value.stars) return [];

  let seed = cfg.value.seed ?? 7;

  const random = () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;

    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);

    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;

    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };

  return Array.from({ length: cfg.value.starCount ?? 60 }, () => ({
    x: random() * 900,
    y: random() * 340 * (cfg.value.starMaxY ?? 0.6),
    r: 0.4 + random() * 1.2,
    opacity: 0.25 + random() * 0.6,
  }));
});
</script>
