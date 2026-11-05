<template>
  <UPopover v-model:open="isOpen" :content="{ align: 'start' }">
    <UButton
      color="neutral"
      variant="outline"
      :disabled="disabled"
      :aria-label="`${label}: ${summary}`"
      class="h-10 w-full justify-between gap-2 rounded-xl px-3"
    >
      <span class="flex min-w-0 items-center gap-2">
        <UIcon :name="icon" class="size-4 shrink-0 text-gray-500 dark:text-gray-500" />

        <span class="shrink-0 text-sm text-gray-600 dark:text-gray-400">{{ label }}</span>

        <span class="truncate text-sm font-medium" :class="summaryClass">{{ summary }}</span>
      </span>

      <UIcon
        name="i-lucide-chevron-down"
        class="size-4 shrink-0 text-gray-500 transition-transform duration-200 dark:text-gray-500"
        :class="{ 'rotate-180': isOpen }"
      />
    </UButton>

    <template #content>
      <div class="flex flex-col sm:flex-row">
        <div
          role="group"
          :aria-label="`${label} presets`"
          class="flex flex-wrap gap-1 border-b border-gray-200/70 p-2 dark:border-gray-700/70 sm:w-36 sm:flex-col sm:flex-nowrap sm:border-r sm:border-b-0"
        >
          <UButton
            v-for="preset in PRESETS"
            :key="preset.label"
            :label="preset.label"
            size="sm"
            :color="presetColor(preset)"
            :variant="presetVariant(preset)"
            :aria-pressed="isPresetActive(preset)"
            class="rounded-lg"
            @click="applyPreset(preset)"
          />
        </div>

        <div class="p-2">
          <UCalendar
            v-model="draft"
            range
            :max-value="todayDate"
            :aria-label="`${label} date range`"
          />

          <div
            class="flex items-center justify-between gap-2 border-t border-gray-200/70 px-1 pt-2 dark:border-gray-700/70"
          >
            <p class="text-xs text-gray-600 dark:text-gray-400">
              {{ draftHint }}
            </p>

            <UButton
              v-if="hasValue || draft.start"
              label="Clear"
              size="xs"
              color="neutral"
              variant="ghost"
              @click="clear"
            />
          </div>
        </div>
      </div>
    </template>
  </UPopover>
</template>

<script setup lang="ts">
import {
  type CalendarDate,
  type DateValue,
  getLocalTimeZone,
  toCalendarDate,
  today,
} from "@internationalized/date";
import { computed, ref, shallowRef, watch } from "vue";

export interface DateRangeValue {
  start?: CalendarDate;
  end?: CalendarDate;
}

// Mirrors the calendar's range shape, where both ends exist but may be unset mid-pick.
interface CalendarRange {
  start: DateValue | undefined;
  end: DateValue | undefined;
}

interface Preset {
  label: string;
  range: () => Required<DateRangeValue>;
}

const { disabled } = defineProps<{
  label: string;
  icon: string;
  disabled?: boolean;
}>();

const model = defineModel<DateRangeValue>({ required: true });

const todayDate = today(getLocalTimeZone());

const lastDays = (days: number) => () => ({
  end: todayDate,
  start: todayDate.subtract({ days: days - 1 }),
});

const PRESETS: Preset[] = [
  { label: "Today", range: lastDays(1) },
  { label: "Last 7 days", range: lastDays(7) },
  { label: "Last 30 days", range: lastDays(30) },
  { label: "Last 90 days", range: lastDays(90) },
  {
    label: "This year",
    range: () => ({
      end: todayDate,
      start: todayDate.set({ day: 1, month: 1 }),
    }),
  },
];

const formatter = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  month: "short",
  year: "numeric",
});

const isOpen = ref(false);
const emptyRange = (): CalendarRange => ({ end: undefined, start: undefined });

const draft = shallowRef<CalendarRange>(emptyRange());

const hasValue = computed(() => Boolean(model.value.start && model.value.end));

const toJsDate = (value: DateValue) => value.toDate(getLocalTimeZone());

const isSameDay = (a?: DateValue, b?: DateValue) => {
  if (!a || !b) {
    return false;
  }
  return a.compare(b) === 0;
};

const summary = computed(() => {
  const { start, end } = model.value;

  if (!start || !end) {
    return "Any time";
  }

  const matchingPreset = PRESETS.find((preset) => isPresetActive(preset));
  if (matchingPreset) {
    return matchingPreset.label;
  }

  if (isSameDay(start, end)) {
    return formatter.format(toJsDate(start));
  }

  return formatter.formatRange(toJsDate(start), toJsDate(end));
});

const summaryClass = computed(() => {
  if (hasValue.value) {
    return "text-gray-900 dark:text-gray-100";
  }
  return "text-gray-500 dark:text-gray-500";
});

const draftHint = computed(() => {
  if (draft.value.start && !draft.value.end) {
    return "Pick an end date, or close for a single day";
  }
  return "Pick a start and end date";
});

const isPresetActive = (preset: Preset) => {
  const range = preset.range();
  return isSameDay(model.value.start, range.start) && isSameDay(model.value.end, range.end);
};

const presetColor = (preset: Preset) => {
  if (isPresetActive(preset)) {
    return "primary";
  }
  return "neutral";
};

const presetVariant = (preset: Preset) => {
  if (isPresetActive(preset)) {
    return "soft";
  }
  return "ghost";
};

const commit = (start: DateValue, end: DateValue) => {
  model.value = { end: toCalendarDate(end), start: toCalendarDate(start) };
};

const applyPreset = (preset: Preset) => {
  const range = preset.range();
  commit(range.start, range.end);
  isOpen.value = false;
};

const clear = () => {
  draft.value = emptyRange();
  model.value = {};
  isOpen.value = false;
};

watch(isOpen, (open) => {
  if (open) {
    draft.value = { end: model.value.end, start: model.value.start };
    return;
  }

  // A lone start date is a deliberate single-day pick, not an abandoned range.
  const { start, end } = draft.value;
  if (start && !end) {
    commit(start, start);
  }
});

watch(draft, (range) => {
  if (!range.start || !range.end) {
    return;
  }

  if (isSameDay(range.start, model.value.start) && isSameDay(range.end, model.value.end)) {
    return;
  }

  commit(range.start, range.end);
  isOpen.value = false;
});

watch(
  () => disabled,
  (isDisabled) => {
    if (isDisabled) {
      isOpen.value = false;
    }
  },
);
</script>
