<script setup lang="ts">
import type { HomeOptionsSection } from "@/features/home/editor/styles";
import type { HomeDailyCoverOptions } from "@/types/home";

import { HomeWidgetType } from "@/enums/home-widget";
import { HOME_DAILY_COVER_STYLES } from "@/features/home/widgets/general";

import HomeStylePicker from "@/components/dashboard/home/options/HomeStylePicker.vue";
import HomeGeneralStylePreview from "@/components/dashboard/home/options/style-previews/HomeGeneralStylePreview.vue";

const { options, section = "all" } = defineProps<{
  options: HomeDailyCoverOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeDailyCoverOptions>] }>();
</script>

<template>
  <div class="flex flex-col gap-4">
    <UFormField v-if="section !== 'options'" label="Daily cover style">
      <HomeStylePicker
        label="Daily cover style"
        :styles="HOME_DAILY_COVER_STYLES"
        :selected="options.dailyCoverStyle"
        @select="emit('change', { dailyCoverStyle: $event })"
      >
        <template #preview="{ value }"
          ><HomeGeneralStylePreview :type="HomeWidgetType.DailyCover" :variant="value"
        /></template>
      </HomeStylePicker>
    </UFormField>
    <p v-if="section !== 'style'" class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
      A new cover every day, generated from your local calendar date. The same date and style give
      you the same artwork on every device.
    </p>
  </div>
</template>
