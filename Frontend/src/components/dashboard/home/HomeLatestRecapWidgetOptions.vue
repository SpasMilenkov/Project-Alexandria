<script setup lang="ts">
import type { HomeRecapOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import WrappedPalettePicker from "@/components/streaming/wrapped/WrappedPalettePicker.vue";
import { HomeRecapPeriod, HomeRecapStyle } from "@/enums/home-widget";
import { HOME_RECAP_STYLES } from "@/utils/home-media";

import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeRecapOptions;
  section?: HomeOptionsSection;
}>();

const emit = defineEmits<{ change: [options: Partial<HomeRecapOptions>] }>();

const periods = [
  { label: "This month so far", value: HomeRecapPeriod.CurrentMonth },
  { label: "Last month", value: HomeRecapPeriod.PreviousMonth },
];
</script>

<template>
  <div class="flex flex-col gap-4">
    <UFormField
      v-if="section !== 'options'"
      label="Recap style"
      description="A preview of your actual listening recap."
    >
      <HomeStylePicker
        label="Recap style"
        :styles="HOME_RECAP_STYLES"
        :selected="options.recapStyle"
        @select="emit('change', { recapStyle: $event })"
      >
        <template #preview="{ value }">
          <span
            class="flex h-16 w-20 shrink-0 flex-col justify-center gap-2 overflow-hidden rounded-lg border border-gray-200/70 p-2 dark:border-gray-700/70"
            :class="{ 'bg-violet-500/10': value === HomeRecapStyle.Poster }"
            aria-hidden="true"
          >
            <span v-if="value === HomeRecapStyle.Stories" class="flex h-full gap-2">
              <span v-for="index in 2" :key="index" class="flex-1 rounded bg-violet-500/20" />
            </span>
            <template v-else>
              <span class="h-3 w-10 rounded bg-gray-500 dark:bg-gray-400" />
              <span class="h-1 w-full rounded bg-gray-400 dark:bg-gray-500" />
              <span class="h-1 w-8 rounded bg-gray-400 dark:bg-gray-500" />
            </template>
          </span>
        </template>
      </HomeStylePicker>
    </UFormField>

    <UFormField
      v-if="section !== 'options'"
      label="Recap colors"
      description="Preview your mix on this widget. Press Done to save it."
    >
      <WrappedPalettePicker
        :model-value="options.recapPalette"
        :default-colors="['#8a3b32', '#4c1d95', '#6554a4']"
        label="Recap"
        description="Pick three colors for your recap gradient."
        @update:model-value="emit('change', { recapPalette: $event })"
      />
    </UFormField>

    <UFormField
      v-if="section !== 'style'"
      label="Month"
      description="Closed months are labeled Final; their data can still refresh."
    >
      <div class="flex flex-wrap gap-2" role="group" aria-label="Recap month">
        <UButton
          v-for="period in periods"
          :key="period.value"
          :color="options.recapPeriod === period.value ? 'primary' : 'neutral'"
          :variant="options.recapPeriod === period.value ? 'solid' : 'outline'"
          :aria-pressed="options.recapPeriod === period.value"
          @click="emit('change', { recapPeriod: period.value })"
        >
          {{ period.label }}
        </UButton>
      </div>
    </UFormField>
  </div>
</template>
