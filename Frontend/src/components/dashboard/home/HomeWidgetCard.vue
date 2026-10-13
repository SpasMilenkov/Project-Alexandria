<script setup lang="ts">
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import HomeWidgetContent from "@/components/dashboard/home/HomeWidgetContent.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { homeDragFromControl } from "@/utils/home-grid-input";

const { widget, editing, disabled, selected } = defineProps<{
  widget: HomeWidget;
  editing: boolean;
  disabled: boolean;
  selected: boolean;
}>();

const emit = defineEmits<{
  select: [];
  pointer: [event: PointerEvent];
  key: [event: KeyboardEvent];
}>();

const definition = computed(() => HOME_WIDGET_REGISTRY[widget.type]);

const pointerDown = (event: PointerEvent) => {
  if (editing && !disabled) emit("pointer", event);
};

const click = (event: MouseEvent) => {
  if (!editing || homeDragFromControl(event.target)) return;

  event.preventDefault();
  emit("select");
};
</script>

<template>
  <section
    :aria-label="definition.label"
    :data-widget-id="widget.instanceId"
    :data-selected="selected"
    :tabindex="editing ? 0 : undefined"
    :aria-describedby="editing ? 'home-placement-help' : undefined"
    class="home-widget frosted-glass glass-surface flex min-h-0 min-w-0 flex-col overflow-y-auto rounded-2xl border border-gray-200/70 p-4 dark:border-gray-700/70"
    :class="{
      'outline-1 outline-dashed outline-offset-2 outline-gray-400 dark:outline-gray-600': editing,
      'cursor-grab touch-none select-none active:cursor-grabbing': editing && !disabled,
      'ring-2 ring-primary': editing && selected,
    }"
    @pointerdown="pointerDown"
    @click.capture="click"
    @focus="emit('select')"
    @keydown="emit('key', $event)"
    @dragstart.prevent
  >
    <header class="mb-4 flex min-w-0 items-center gap-2">
      <UIcon :name="definition.icon" class="size-5 shrink-0 text-gray-600 dark:text-gray-400" />
      <h2 class="min-w-0 flex-1 truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
        {{ definition.label }}
      </h2>
    </header>

    <HomeWidgetContent :component="definition.component" :widget="widget" />
  </section>
</template>

<style scoped>
.home-widget {
  container-name: home-widget;
  container-type: size;
}
</style>
