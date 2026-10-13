<script setup lang="ts">
import type { HomeStatusOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HomeStatusStyle } from "@/enums/home-widget";
import { HOME_STATUS_STYLES } from "@/utils/home-status-activity";

import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeStatusOptions;
  section?: HomeOptionsSection;
}>();

const emit = defineEmits<{ change: [options: Partial<HomeStatusOptions>] }>();
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="System status style"
    description="Keep this look as you resize the widget."
  >
    <HomeStylePicker
      label="System status style"
      :styles="HOME_STATUS_STYLES"
      :selected="options.statusStyle"
      @select="emit('change', { statusStyle: $event })"
    >
      <template #preview="{ value }">
        <span
          class="status-preview flex h-16 w-20 shrink-0 flex-col justify-center gap-2 overflow-hidden rounded-lg border border-gray-200/70 p-2 dark:border-gray-700/70"
          aria-hidden="true"
        >
          <span v-if="value === HomeStatusStyle.Beacon" class="flex items-center gap-2">
            <UIcon
              name="i-lucide-circle-check"
              class="size-6 text-emerald-700 dark:text-emerald-400"
            />
            <span class="h-2 flex-1 rounded bg-gray-500 dark:bg-gray-400" />
          </span>

          <template v-else-if="value === HomeStatusStyle.Services">
            <span v-for="index in 3" :key="index" class="flex items-center gap-2">
              <span class="size-1.5 rounded-full bg-emerald-500" />
              <span class="h-1 flex-1 rounded bg-gray-500 dark:bg-gray-400" />
            </span>
          </template>

          <template v-else>
            <span v-for="index in 2" :key="index" class="flex gap-0.5">
              <span
                v-for="day in 8"
                :key="day"
                class="h-3 min-w-0 flex-1 rounded-sm"
                :class="index === 1 && day === 6 ? 'bg-amber-500' : 'bg-emerald-500'"
              />
            </span>
          </template>
        </span>
      </template>
    </HomeStylePicker>
  </UFormField>
</template>
