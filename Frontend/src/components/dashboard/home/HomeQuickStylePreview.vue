<script setup lang="ts">
import { HomeQuickAccessStyle, HomeQuickUploadStyle } from "@/enums/home-widget";

defineProps<{ accessStyle?: HomeQuickAccessStyle; uploadStyle?: HomeQuickUploadStyle }>();

const folderPath = "M3 9V6h9l4 4h17v20H3z";
</script>

<template>
  <span
    class="flex size-24 shrink-0 overflow-hidden rounded-lg bg-gray-100 p-2 text-gray-600 dark:bg-gray-800 dark:text-gray-400"
    aria-hidden="true"
  >
    <svg viewBox="0 0 80 80" class="h-full w-full" fill="none" stroke="currentColor">
      <template v-if="accessStyle === HomeQuickAccessStyle.List">
        <g v-for="index in 3" :key="index" :transform="`translate(0 ${(index - 1) * 26})`">
          <path :d="folderPath" transform="translate(1 2) scale(.55)" />
          <path d="M28 12H73M28 18H54" stroke-width="2" stroke-linecap="round" />
        </g>
      </template>

      <template v-else-if="accessStyle === HomeQuickAccessStyle.Chips">
        <g
          v-for="(width, index) in [65, 52, 70]"
          :key="index"
          :transform="`translate(3 ${5 + index * 25})`"
        >
          <rect :width="width" height="19" rx="9" />
          <path :d="folderPath" transform="translate(5 3) scale(.4)" />
          <path :d="`M24 10H${width - 8}`" stroke-width="2" stroke-linecap="round" />
        </g>
      </template>

      <template v-else-if="accessStyle !== undefined">
        <g
          v-for="(point, index) in [
            [0, 0],
            [42, 0],
            [0, 42],
            [42, 42],
          ]"
          :key="index"
          :transform="`translate(${point[0]} ${point[1]})`"
        >
          <rect width="36" height="36" rx="5" stroke-opacity=".5" />
          <path :d="folderPath" transform="translate(6 3) scale(.65)" />
          <path d="M6 31H27" stroke-width="2" stroke-linecap="round" />
        </g>
      </template>

      <template v-else-if="uploadStyle === HomeQuickUploadStyle.Actions">
        <rect x="5" y="16" width="70" height="24" rx="5" />
        <path d="M40 34V22m-5 5 5-5 5 5" stroke-width="2" />
        <rect x="5" y="48" width="32" height="18" rx="4" />
        <rect x="43" y="48" width="32" height="18" rx="4" />
      </template>

      <template v-else>
        <circle v-if="uploadStyle === HomeQuickUploadStyle.Pulse" cx="40" cy="40" r="30" />
        <rect v-else x="4" y="5" width="72" height="70" rx="9" stroke-dasharray="4 3" />
        <path
          d="M40 50V27m-9 9 9-9 9 9M26 56h28"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
      </template>
    </svg>
  </span>
</template>
