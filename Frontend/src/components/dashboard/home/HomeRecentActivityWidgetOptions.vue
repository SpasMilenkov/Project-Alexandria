<script setup lang="ts">
import type { HomeActivityOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HomeActivityStyle } from "@/enums/home-widget";
import { HOME_ACTIVITY_STYLES } from "@/utils/home-status-activity";

import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeActivityOptions;
  section?: HomeOptionsSection;
}>();

const emit = defineEmits<{ change: [options: Partial<HomeActivityOptions>] }>();
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="Recent activity style"
    description="Choose how to arrange your recent events."
  >
    <HomeStylePicker
      label="Recent activity style"
      :styles="HOME_ACTIVITY_STYLES"
      :selected="options.activityStyle"
      @select="emit('change', { activityStyle: $event })"
    >
      <template #preview="{ value }">
        <span
          class="activity-preview flex h-16 w-20 shrink-0 flex-col justify-center gap-2 overflow-hidden rounded-lg border border-gray-200/70 p-2 dark:border-gray-700/70"
          aria-hidden="true"
        >
          <span
            v-if="value === HomeActivityStyle.ByDay"
            class="h-1 w-8 rounded bg-gray-500 dark:bg-gray-400"
          />

          <span v-for="index in 2" :key="index" class="flex items-center gap-2">
            <span
              class="size-2 shrink-0 rounded-full"
              :class="index === 1 ? 'bg-emerald-500' : 'bg-amber-500'"
            />
            <span class="h-1 flex-1 rounded bg-gray-500 dark:bg-gray-400" />
            <span
              v-if="value === HomeActivityStyle.Feed"
              class="h-1 w-2 rounded bg-gray-400 dark:bg-gray-500"
            />
          </span>
        </span>
      </template>
    </HomeStylePicker>
  </UFormField>
</template>
