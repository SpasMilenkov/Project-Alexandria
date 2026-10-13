<script setup lang="ts">
import type { HomeFocusOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HomeWidgetType } from "@/enums/home-widget";
import { HOME_FOCUS_STYLES } from "@/utils/home-general";

import HomeGeneralStylePreview from "./HomeGeneralStylePreview.vue";
import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeFocusOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeFocusOptions>] }>();
const fields = [
  { key: "focusMinutes", label: "Focus (minutes)", max: 180 },
  { key: "shortBreakMinutes", label: "Short break (minutes)", max: 60 },
  { key: "longBreakMinutes", label: "Long break (minutes)", max: 120 },
  { key: "focusSessions", label: "Sessions per cycle", max: 8 },
] as const;

const changeNumber = (key: (typeof fields)[number]["key"], value: string | number, max: number) => {
  const number = Number(value);
  if (!Number.isFinite(number) || value === "") return;

  emit("change", { [key]: Math.min(max, Math.max(1, Math.round(number))) });
};
</script>

<template>
  <div class="flex flex-col gap-4">
    <UFormField v-if="section !== 'options'" label="Focus timer style">
      <HomeStylePicker
        label="Focus timer style"
        :styles="HOME_FOCUS_STYLES"
        :selected="options.focusStyle"
        @select="emit('change', { focusStyle: $event })"
      >
        <template #preview="{ value }"
          ><HomeGeneralStylePreview :type="HomeWidgetType.FocusTimer" :variant="value"
        /></template>
      </HomeStylePicker>
    </UFormField>

    <template v-if="section !== 'style'">
      <div class="grid grid-cols-2 gap-4">
        <UFormField
          v-for="field in fields"
          :key="field.key"
          :label="field.label"
          class="flex min-w-0 flex-col justify-between"
        >
          <UInput
            type="number"
            :model-value="options[field.key]"
            :aria-label="field.label"
            :min="1"
            :max="field.max"
            :step="1"
            class="w-full"
            @update:model-value="changeNumber(field.key, $event, field.max)"
          />
        </UFormField>
      </div>
      <p class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
        Changes apply to the next cycle or after Reset. Start each focus session and break when you
        are ready. Your active timer stays in this browser.
      </p>
    </template>
  </div>
</template>
