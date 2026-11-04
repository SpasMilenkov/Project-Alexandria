<template>
  <figure
    v-if="recipe"
    class="m-0"
    :data-art-family="recipe.family"
    :data-art-variant="recipe.variant"
  >
    <svg viewBox="0 0 800 360" class="block h-auto w-full" aria-hidden="true">
      <defs>
        <pattern :id="textureId" width="5" height="5" patternUnits="userSpaceOnUse">
          <circle cx="1" cy="1" r="0.65" fill="var(--wrapped-cutout)" />
        </pattern>

        <clipPath :id="clipId"><rect width="800" height="360" /></clipPath>
      </defs>

      <g :clip-path="`url(#${clipId})`">
        <path
          d="M 32 44 L 32 32 L 44 32 M 756 32 L 768 32 L 768 44 M 32 316 L 32 328 L 44 328 M 756 328 L 768 328 L 768 316"
          fill="none"
          stroke="var(--wrapped-line)"
        />

        <path
          v-for="(path, index) in recipe.paths"
          :key="`p-${index}`"
          :d="path.d"
          :fill="path.fill"
          :stroke="path.stroke"
          :stroke-width="path.width"
          stroke-linecap="round"
          stroke-linejoin="round"
        />

        <circle
          v-for="(circle, index) in recipe.circles"
          :key="`c-${index}`"
          :cx="circle.x"
          :cy="circle.y"
          :r="circle.r"
          :fill="circle.fill"
          :stroke="circle.stroke"
          :stroke-width="circle.width"
        />

        <rect width="800" height="360" :fill="`url(#${textureId})`" opacity="0.35" />
      </g>
    </svg>

    <figcaption
      class="flex flex-wrap items-center justify-between gap-2 px-6 pb-4 text-xs text-gray-600 dark:text-gray-400"
    >
      <span>{{ recipe.caption }}</span>
      <span class="font-medium" :style="{ color: recipe.inks[2] }">{{ recipe.metric }}</span>
    </figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed, useId } from "vue";

import type { WrappedCardResponse } from "@/api/stats";

import { buildWrappedRecipe } from "@/utils/wrapped-art.utils";

const props = defineProps<{
  card: WrappedCardResponse;
  seed: string;
}>();

const id = useId().replace(/:/g, "");
const textureId = `wrapped-texture-${id}`;
const clipId = `wrapped-clip-${id}`;

const recipe = computed(() => buildWrappedRecipe(props.card, props.seed));
</script>
