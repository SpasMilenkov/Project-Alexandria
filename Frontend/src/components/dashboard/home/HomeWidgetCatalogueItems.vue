<script setup lang="ts">
import type { HomeWidgetType } from "@/enums/home-widget";

import { homeWidgetDefinitions } from "@/components/dashboard/home/registry";
import { homeDragFromControl } from "@/utils/home-grid-input";

const {
  disabled,
  draggable,
  disabledReason = "",
} = defineProps<{
  disabled: boolean;
  draggable: boolean;
  disabledReason?: string;
}>();

const emit = defineEmits<{
  add: [type: HomeWidgetType];
  pointerDrag: [type: HomeWidgetType, event: PointerEvent];
}>();

const startDrag = (type: HomeWidgetType, event: PointerEvent) => {
  if (!disabled && draggable && !homeDragFromControl(event.target))
    emit("pointerDrag", type, event);
};
</script>

<template>
  <div class="flex w-full flex-col gap-4">
    <div
      v-for="definition in homeWidgetDefinitions"
      :key="definition.type"
      :data-catalogue-widget="definition.type"
      :data-draggable="draggable && !disabled"
      class="flex items-center gap-4 rounded-xl border border-gray-200/70 p-4 dark:border-gray-700/70"
      :class="{
        'cursor-grab touch-none select-none active:cursor-grabbing': draggable && !disabled,
      }"
      @pointerdown="startDrag(definition.type, $event)"
      @dragstart.prevent
    >
      <UIcon :name="definition.icon" class="size-5 shrink-0 text-gray-600 dark:text-gray-400" />

      <div class="min-w-0 flex-1">
        <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">{{ definition.label }}</p>
        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">{{ definition.description }}</p>
      </div>

      <UButton
        color="neutral"
        variant="outline"
        icon="i-heroicons-plus"
        :disabled="disabled"
        :aria-label="`Add ${definition.label}`"
        @click="emit('add', definition.type)"
        >Add</UButton
      >
    </div>

    <p v-if="disabled" class="text-sm text-gray-600 dark:text-gray-400">
      {{ disabledReason || "This layout has reached its widget limit." }}
    </p>
  </div>
</template>
