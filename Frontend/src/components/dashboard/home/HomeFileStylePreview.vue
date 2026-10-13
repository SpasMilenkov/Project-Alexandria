<script setup lang="ts">
import type { StorageBreakdown } from "@/api/status";

import HomeStorageDisplay from "@/components/dashboard/home/widgets/HomeStorageDisplay.vue";
import { HomeRecentFilesStyle, HomeStorageStyle } from "@/enums/home-widget";

defineProps<{ storageStyle?: HomeStorageStyle; recentStyle?: HomeRecentFilesStyle }>();

const sample: StorageBreakdown = {
  sizeByType: { "audio/mpeg": 28e9, "video/mp4": 32e9, "application/pdf": 8e9 },
  filesSize: 68e9,
  previewsSize: 2e9,
  previewsCount: 4,
  transcodedSize: 6e9,
  representationsCount: 2,
  usedBytes: 76e9,
  quotaBytes: 100e9,
  trashSize: 0,
  oldFiles: [],
};
</script>

<template>
  <span
    class="flex size-24 shrink-0 overflow-hidden rounded-lg bg-gray-100 p-2 dark:bg-gray-800"
    aria-hidden="true"
  >
    <HomeStorageDisplay
      v-if="storageStyle !== undefined"
      :data="sample"
      :storage-style="storageStyle"
      :animated="false"
    />

    <svg v-else viewBox="0 0 80 80" class="h-full w-full" fill="none">
      <template v-if="recentStyle === HomeRecentFilesStyle.Thumbnails">
        <g
          v-for="(point, index) in [
            [2, 6],
            [44, 6],
            [2, 44],
            [44, 44],
          ]"
          :key="index"
          :transform="`translate(${point[0]} ${point[1]})`"
        >
          <rect width="32" height="24" rx="4" fill="#7A9E87" />
          <path d="M4 20L12 12L20 18L26 10" stroke="white" stroke-width="2" />
          <path d="M2 30H27" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
        </g>
      </template>

      <template v-else-if="recentStyle === HomeRecentFilesStyle.Deck">
        <rect x="15" y="20" width="52" height="55" rx="6" stroke="currentColor" />
        <rect
          x="10"
          y="14"
          width="60"
          height="55"
          rx="6"
          class="preview-surface"
          stroke="currentColor"
        />
        <rect x="5" y="8" width="70" height="55" rx="6" fill="#7A9E87" />
        <path d="M14 43L28 25L42 35L60 20" stroke="white" stroke-width="2" />
        <path d="M15 52H60" stroke="white" stroke-width="2" />
      </template>

      <template v-else-if="recentStyle === HomeRecentFilesStyle.Timeline">
        <path d="M16 6V74" stroke="currentColor" stroke-width="1" />
        <g
          v-for="(color, index) in ['#5B7FA6', '#7A9E87', '#C9A84C']"
          :key="color"
          :transform="`translate(0 ${index * 24})`"
        >
          <circle cx="16" cy="16" r="4" :fill="color" />
          <path
            d="M28 13H72M28 20H52"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        </g>
      </template>

      <template v-else-if="recentStyle === HomeRecentFilesStyle.Table">
        <path d="M4 10H40M58 10H74M4 16H76" stroke="currentColor" />
        <g v-for="index in 4" :key="index" :transform="`translate(0 ${index * 14})`">
          <path
            d="M5 14H43M58 14H74"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        </g>
      </template>

      <template v-else>
        <g
          v-for="(color, index) in ['#5B7FA6', '#7A9E87', '#C9A84C']"
          :key="color"
          :transform="`translate(0 ${index * 24})`"
        >
          <rect x="4" y="8" width="12" height="16" rx="2" :stroke="color" stroke-width="2" />
          <path
            d="M24 12H74M24 20H48"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
          />
        </g>
      </template>
    </svg>
  </span>
</template>

<style scoped>
.preview-surface {
  fill: var(--ui-bg-elevated);
}
</style>
