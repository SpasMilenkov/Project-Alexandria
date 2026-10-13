<script setup lang="ts">
import { type DateValue, parseDate } from "@internationalized/date";
import { computed } from "vue";

import type { HomeCountdownOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HomeWidgetType } from "@/enums/home-widget";
import { HOME_COUNTDOWN_STYLES } from "@/utils/home-general";

import HomeGeneralStylePreview from "./HomeGeneralStylePreview.vue";
import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeCountdownOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeCountdownOptions>] }>();

const minDate = parseDate("0001-01-01");
const maxDate = parseDate("9999-12-31");

const targetDate = computed<DateValue | undefined>({
  get: () => {
    if (!options.countdownTarget) return undefined;

    return parseDate(options.countdownTarget);
  },
  set: (value) => emit("change", { countdownTarget: value?.toString() ?? null }),
});
</script>

<template>
  <div class="flex flex-col gap-4">
    <UFormField v-if="section !== 'options'" label="Countdown style">
      <HomeStylePicker
        label="Countdown style"
        :styles="HOME_COUNTDOWN_STYLES"
        :selected="options.countdownStyle"
        @select="emit('change', { countdownStyle: $event })"
      >
        <template #preview="{ value }"
          ><HomeGeneralStylePreview :type="HomeWidgetType.Countdown" :variant="value"
        /></template>
      </HomeStylePicker>
    </UFormField>

    <template v-if="section !== 'style'">
      <UFormField label="Event name" description="A short label for the day you are waiting for.">
        <UInput
          :model-value="options.countdownLabel"
          aria-label="Event name"
          :maxlength="120"
          class="w-full"
          @update:model-value="emit('change', { countdownLabel: String($event) })"
        />
      </UFormField>

      <UFormField
        label="Target date"
        description="Uses your local calendar. Shows Today for the whole day."
      >
        <UInputDate
          v-model="targetDate"
          aria-label="Target date"
          :min-value="minDate"
          :max-value="maxDate"
          class="w-full"
        >
          <template #trailing>
            <UPopover>
              <UButton
                color="neutral"
                variant="link"
                size="sm"
                icon="i-lucide-calendar"
                aria-label="Choose target date"
                class="justify-center px-0"
              />

              <template #content>
                <UCalendar v-model="targetDate" :min-value="minDate" :max-value="maxDate" />
              </template>
            </UPopover>
          </template>
        </UInputDate>
      </UFormField>
    </template>
  </div>
</template>
