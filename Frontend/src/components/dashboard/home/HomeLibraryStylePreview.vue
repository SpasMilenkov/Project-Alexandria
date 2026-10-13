<script setup lang="ts">
import { HomeLibraryStyle } from "@/enums/home-widget";

defineProps<{ libraryStyle: HomeLibraryStyle }>();
</script>

<template>
  <span
    class="flex size-24 shrink-0 overflow-hidden rounded-lg bg-gray-100 p-2 text-gray-600 dark:bg-gray-800 dark:text-gray-400"
    aria-hidden="true"
  >
    <svg viewBox="0 0 80 80" class="h-full w-full" fill="none" stroke="currentColor">
      <template v-if="libraryStyle === HomeLibraryStyle.Directory">
        <g v-for="index in 4" :key="index" :transform="`translate(0 ${(index - 1) * 20})`">
          <rect x="3" y="4" width="10" height="10" rx="3" />
          <path d="M21 9H60M70 6l3 3-3 3M3 19h73" stroke-linecap="round" />
        </g>
      </template>

      <template v-else-if="libraryStyle === HomeLibraryStyle.Spotlight">
        <rect x="1" y="2" width="40" height="76" rx="6" />
        <circle cx="21" cy="29" r="16" />
        <rect x="12" y="20" width="18" height="18" rx="4" transform="rotate(-14 21 29)" />
        <rect x="12" y="20" width="18" height="18" rx="4" />
        <path d="M17 27h3l2 2h3v5h-8z" stroke-linejoin="round" />
        <path d="M8 56h24M8 63h19M8 70h23" stroke-linecap="round" />
        <g v-for="index in 3" :key="index" :transform="`translate(48 ${2 + (index - 1) * 27})`">
          <rect width="31" height="22" rx="5" />
          <path d="M6 8h7M6 15h19" stroke-linecap="round" />
        </g>
      </template>

      <template v-else-if="libraryStyle === HomeLibraryStyle.Icons">
        <g
          v-for="(point, index) in [
            [3, 17],
            [29, 17],
            [55, 17],
            [3, 43],
            [29, 43],
            [55, 43],
          ]"
          :key="index"
          :transform="`translate(${point[0]} ${point[1]})`"
        >
          <rect width="22" height="22" rx="4" />
          <path d="M6 8h4l2 2h4v6H6z" stroke-linejoin="round" />
        </g>
      </template>

      <template v-else>
        <g
          v-for="(point, index) in [
            [1, 2],
            [43, 2],
            [1, 44],
            [43, 44],
          ]"
          :key="index"
          :transform="`translate(${point[0]} ${point[1]})`"
        >
          <rect width="36" height="34" rx="6" />
          <rect x="6" y="6" width="10" height="10" rx="3" />
          <path d="M6 25h23M26 8h4v4" stroke-linecap="round" />
        </g>
      </template>
    </svg>
  </span>
</template>
