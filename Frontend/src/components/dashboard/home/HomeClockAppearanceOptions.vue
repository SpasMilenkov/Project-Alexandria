<script setup lang="ts">
import type { HomeClockOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import HomeClockPositionPicker from "@/components/dashboard/home/HomeClockPositionPicker.vue";
import HomeClockStylePreview from "@/components/dashboard/home/HomeClockStylePreview.vue";
import { HomeClockHourFormat, HomeClockStyle } from "@/enums/home-widget";
import { HOME_CLOCK_HOUR_FORMATS, HOME_CLOCK_STYLES } from "@/utils/home-widget-options";

import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeClockOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeClockOptions>] }>();

const change = (patch: Partial<HomeClockOptions>) => emit("change", patch);
</script>

<template>
  <div class="flex flex-col gap-4">
    <UFormField
      v-if="section !== 'options'"
      label="Clock style"
      description="Keep this look as you resize your clock."
    >
      <HomeStylePicker
        label="Clock style"
        :styles="HOME_CLOCK_STYLES"
        :selected="options.clockStyle"
        @select="change({ clockStyle: $event })"
      >
        <template #preview="{ value }">
          <HomeClockStylePreview :clock-style="value" />
        </template>
      </HomeStylePicker>
    </UFormField>

    <UFormField
      v-if="section !== 'style'"
      label="Hour format"
      description="Device default follows this device’s locale."
    >
      <div class="grid grid-cols-3 gap-2" role="group" aria-label="Hour format">
        <button
          v-for="format in HOME_CLOCK_HOUR_FORMATS"
          :key="format.value"
          type="button"
          :aria-label="format.label"
          :aria-pressed="options.hourFormat === format.value"
          class="rounded-lg border border-gray-200/70 px-2 py-3 text-center text-xs font-medium text-gray-900 hover:bg-gray-100/70 focus-visible:outline-2 focus-visible:outline-primary dark:border-gray-700/70 dark:text-gray-100 dark:hover:bg-gray-800/70"
          :class="{ 'ring-1 ring-primary border-primary': options.hourFormat === format.value }"
          @click="change({ hourFormat: format.value })"
        >
          <span class="block">{{ format.label }}</span>
          <span
            v-if="format.value === HomeClockHourFormat.TwelveHour"
            class="mt-1 block text-gray-600 dark:text-gray-400"
            >AM / PM</span
          >
        </button>
      </div>
    </UFormField>

    <UFormField
      v-if="section !== 'style' && options.clockStyle !== HomeClockStyle.Analog"
      label="Show seconds"
      description="Include seconds in the digital time."
    >
      <USwitch
        :model-value="options.showSeconds"
        aria-label="Show seconds"
        @update:model-value="change({ showSeconds: $event })"
      />
    </UFormField>

    <UFormField
      v-if="section !== 'style'"
      label="Clock position"
      description="Pick a point inside the widget."
    >
      <HomeClockPositionPicker
        target="clock"
        :horizontal="options.horizontalAlignment"
        :vertical="options.verticalAlignment"
        @change="
          (horizontal, vertical) =>
            change({ horizontalAlignment: horizontal, verticalAlignment: vertical })
        "
      />
    </UFormField>

    <UFormField
      v-if="section !== 'style'"
      label="Text position"
      description="Place the date and time zone independently of the clock."
    >
      <HomeClockPositionPicker
        target="text"
        :horizontal="options.textAlignment"
        :vertical="options.textVerticalAlignment"
        @change="
          (horizontal, vertical) =>
            change({ textAlignment: horizontal, textVerticalAlignment: vertical })
        "
      />
    </UFormField>
  </div>
</template>
