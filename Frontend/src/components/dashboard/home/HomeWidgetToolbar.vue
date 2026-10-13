<script setup lang="ts">
import { computed } from "vue";

import type { HomeLayout, HomeWidget, HomeWidgetOptions as WidgetOptions } from "@/types/home";

import HomeWidgetOptions from "@/components/dashboard/home/HomeWidgetOptions.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { HOME_GRID_GAP, HOME_GRID_ROW_HEIGHT } from "@/utils/home-grid";
import { homeWidgetHasStyles, homeWidgetStyleLabel } from "@/utils/home-widget-editor";
import { glassOverlayContent } from "@/utils/modalUi";

const { widget, columns } = defineProps<{
  widget: HomeWidget;
  columns: HomeLayout["columns"];
  canDuplicate: boolean;
}>();

const emit = defineEmits<{
  duplicate: [];
  remove: [];
  options: [options: Partial<WidgetOptions>];
  move: [event: PointerEvent];
}>();

const definition = computed(() => HOME_WIDGET_REGISTRY[widget.type]);
const position = computed(() => ({
  left: `clamp(8px, calc(${widget.x} * (100% + 1rem) / ${columns}), max(8px, calc(100% - 18rem)))`,
  top: `${widget.y * (HOME_GRID_ROW_HEIGHT + HOME_GRID_GAP) + (widget.y === 0 ? 8 : -44)}px`,
}));
</script>

<template>
  <div
    role="toolbar"
    aria-label="Selected widget actions"
    class="home-widget-toolbar frosted-glass glass-surface-strong absolute z-20 flex max-w-[calc(100%-1rem)] items-center gap-1 rounded-lg border border-gray-200/70 p-1 shadow-lg dark:border-gray-700/70"
    :style="position"
    @pointerdown.stop
  >
    <UButton
      color="neutral"
      variant="ghost"
      icon="i-lucide-grip-vertical"
      aria-label="Move selected widget"
      title="Drag to move; arrow keys on the card move one cell"
      class="shrink-0 touch-none cursor-grab justify-center active:cursor-grabbing"
      @pointerdown="emit('move', $event)"
    />

    <UPopover v-if="homeWidgetHasStyles(widget.type)" :ui="{ content: glassOverlayContent }">
      <UButton
        color="neutral"
        variant="ghost"
        trailing-icon="i-lucide-chevron-down"
        aria-label="Quick widget style"
        class="min-w-0 max-w-40"
        :ui="{ label: 'truncate' }"
        :label="`${definition.label} · ${homeWidgetStyleLabel(widget)}`"
      />

      <template #content>
        <div
          class="max-h-[calc(100dvh-2rem)] w-72 max-w-[calc(100vw-2rem)] overflow-y-auto p-4"
          aria-label="Quick style picker"
        >
          <HomeWidgetOptions :widget="widget" section="style" @change="emit('options', $event)" />
        </div>
      </template>
    </UPopover>
    <span v-else class="min-w-0 max-w-40 truncate px-2 text-sm text-gray-900 dark:text-gray-100">
      {{ definition.label }}
    </span>

    <UButton
      color="neutral"
      variant="ghost"
      icon="i-lucide-copy"
      aria-label="Duplicate selected widget"
      title="Duplicate (Ctrl/Cmd+D)"
      :disabled="!canDuplicate"
      class="shrink-0 justify-center"
      @click="emit('duplicate')"
    />

    <UButton
      color="error"
      variant="ghost"
      icon="i-lucide-trash-2"
      aria-label="Remove selected widget"
      title="Remove (Delete)"
      class="shrink-0 justify-center"
      @click="emit('remove')"
    />
  </div>
</template>
