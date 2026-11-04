<script setup lang="ts">
import {
  HomeCountdownStyle,
  HomeDailyCoverStyle,
  HomeFocusStyle,
  HomeWidgetType,
} from "@/enums/home-widget";

import HomeDailyCoverArt from "./widgets/HomeDailyCoverArt.vue";

defineProps<{ type: HomeWidgetType; variant: number }>();
</script>

<template>
  <span
    class="relative flex h-16 w-20 flex-col items-center justify-center gap-1 overflow-hidden rounded-lg border border-gray-200/70 text-gray-900 dark:border-gray-700/70 dark:text-gray-100"
  >
    <template v-if="type === HomeWidgetType.DailyCover">
      <HomeDailyCoverArt day="2026-10-09" :cover-style="variant" class="absolute inset-0" />

      <span
        v-if="variant === HomeDailyCoverStyle.DatePoster"
        class="absolute inset-x-0 bottom-0 bg-default px-1 text-xs"
        ><b>9</b> Fri</span
      >
    </template>

    <template v-else-if="type === HomeWidgetType.Countdown">
      <span
        v-if="variant === HomeCountdownStyle.Ring"
        class="flex size-12 items-center justify-center rounded-full border-4 border-gray-400 text-xl font-bold dark:border-gray-500"
        >12</span
      >
      <span v-else-if="variant === HomeCountdownStyle.Flip" class="flex gap-1">
        <b
          v-for="digit in ['1', '2']"
          :key="digit"
          class="relative rounded bg-gray-200 px-1 text-2xl dark:bg-gray-800"
          >{{ digit
          }}<span class="absolute inset-x-0 top-1/2 border-t border-gray-400 dark:border-gray-500"
        /></b>
      </span>
      <template v-else>
        <b class="text-2xl">12<span class="ml-1 text-xs font-normal">days</span></b>
        <span v-if="variant === HomeCountdownStyle.DayDots" class="grid grid-cols-10 gap-0.5">
          <span
            v-for="dot in 30"
            :key="dot"
            class="size-1 rounded-full"
            :class="dot < 19 ? 'bg-gray-500 dark:bg-gray-400' : 'bg-gray-200 dark:bg-gray-800'"
          />
        </span>
      </template>
    </template>

    <template v-else>
      <span
        v-if="variant === HomeFocusStyle.Ring"
        class="flex size-12 items-center justify-center rounded-full border-4 border-gray-400 text-xs font-bold dark:border-gray-500"
        >25:00</span
      >
      <template v-else>
        <b class="text-xl">25:00</b>
        <span v-if="variant === HomeFocusStyle.Cycle" class="flex w-16 gap-0.5">
          <span
            v-for="block in 8"
            :key="block"
            class="h-2 rounded-sm bg-gray-400 dark:bg-gray-500"
            :style="{ flex: block % 2 ? 5 : 1 }"
          />
        </span>
        <span v-else class="h-1 w-14 rounded bg-gray-400 dark:bg-gray-500" />
      </template>
    </template>
  </span>
</template>
