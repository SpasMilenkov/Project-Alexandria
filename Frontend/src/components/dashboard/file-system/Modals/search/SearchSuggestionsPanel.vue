<template>
  <!-- Mousedown keeps focus in the search input so picking an option never closes the list first. -->
  <div
    :id="id"
    role="listbox"
    :aria-label="title"
    class="overflow-y-auto bg-default p-1"
    @mousedown.prevent
    @keydown.esc="onEscape"
  >
    <p class="px-2 pb-1 pt-1.5 text-xs font-medium text-gray-600 dark:text-gray-400">
      {{ title }}
    </p>

    <div v-if="calendarOpen" class="space-y-2 p-2">
      <UCalendar v-model="range" range :max-value="maxDate" />
      <div class="flex justify-end gap-2">
        <UButton size="sm" color="neutral" variant="outline" @click="emit('cancelCalendar')">
          Cancel
        </UButton>
        <UButton size="sm" color="primary" :disabled="!canApply" @click="emit('applyCalendar')">
          Apply
        </UButton>
      </div>
    </div>

    <template v-else>
      <button
        v-for="(item, index) in items"
        :id="`${optionIdPrefix}-${index}`"
        :key="item.id"
        type="button"
        role="option"
        :aria-selected="index === highlighted"
        class="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm"
        :class="index === highlighted ? 'bg-elevated' : ''"
        @mouseenter="highlighted = index"
        @click="item.apply()"
      >
        <UIcon :name="item.icon" class="w-4 h-4 shrink-0 text-gray-500 dark:text-gray-500" />
        <span class="truncate font-medium text-gray-900 dark:text-gray-100">
          {{ item.label }}
        </span>
        <span
          v-if="item.hint"
          class="ml-auto shrink-0 pl-2 text-xs text-gray-500 dark:text-gray-500"
        >
          {{ item.hint }}
        </span>
      </button>

      <p v-if="items.length === 0" class="px-2 py-1.5 text-sm text-gray-600 dark:text-gray-400">
        {{ emptyText }}
      </p>
    </template>
  </div>
</template>

<script setup lang="ts">
import type { DateValue } from "@internationalized/date";

import type { CalendarRange } from "@/types/calendar-range";
import type { SearchSuggestion } from "@/types/search-suggestion";

const highlighted = defineModel<number>("highlighted", { required: true });
const range = defineModel<CalendarRange | null>("range", { required: true });

const { calendarOpen } = defineProps<{
  calendarOpen: boolean;
  canApply: boolean;
  emptyText: string;
  id: string;
  items: SearchSuggestion[];
  maxDate: DateValue;
  optionIdPrefix: string;
  title: string;
}>();

const emit = defineEmits<{
  applyCalendar: [];
  cancelCalendar: [];
}>();

// Keyboard focus can sit inside the calendar, away from the search input's own handler.
const onEscape = (event: KeyboardEvent) => {
  if (!calendarOpen) {
    return;
  }
  event.preventDefault();
  emit("cancelCalendar");
};
</script>
