<script setup lang="ts">
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import HomeWidgetContent from "@/components/dashboard/home/HomeWidgetContent.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { HomeWidgetType } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { homeDragFromControl } from "@/utils/home-grid-input";
import { isHomeClockFaceOnly } from "@/utils/home-widget-options";

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
const home = useHomeStore();
const showTitle = computed(() => widget.showTitle ?? home.showWidgetTitles);
const playerCard = computed(() => widget.type === HomeWidgetType.Player);
const coverCard = computed(
  () => widget.type === HomeWidgetType.DailyCover || widget.type === HomeWidgetType.LatestRecap,
);

const compact = computed(() => {
  if (isHomeClockFaceOnly(widget)) return true;

  return (
    widget.height <= 2 &&
    (widget.type === HomeWidgetType.Greeting ||
      widget.type === HomeWidgetType.Storage ||
      widget.type === HomeWidgetType.LibraryShortcuts ||
      widget.type === HomeWidgetType.RecentFiles ||
      widget.type === HomeWidgetType.QuickAccess ||
      widget.type === HomeWidgetType.QuickUpload ||
      widget.type === HomeWidgetType.Player ||
      widget.type === HomeWidgetType.SystemStatus ||
      widget.type === HomeWidgetType.TopSongs ||
      widget.type === HomeWidgetType.RecentActivity ||
      widget.type === HomeWidgetType.TranscodingJobs ||
      widget.type === HomeWidgetType.LatestRecap ||
      widget.type === HomeWidgetType.Collections ||
      widget.type === HomeWidgetType.Countdown ||
      widget.type === HomeWidgetType.FocusTimer ||
      widget.type === HomeWidgetType.DailyCover)
  );
});

const pointerDown = (event: PointerEvent) => {
  if (event.pointerType === "touch" && !selected) return;

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
    :data-title-visible="showTitle"
    :style="{ '--player-header-space': showTitle ? '28px' : '0px' }"
    :tabindex="editing ? 0 : undefined"
    :aria-describedby="editing ? 'home-placement-help' : undefined"
    class="home-widget frosted-glass glass-surface flex min-h-0 min-w-0 flex-col overflow-y-auto rounded-2xl border border-gray-200/70 dark:border-gray-700/70"
    :class="{
      'p-0': coverCard,
      'p-2': !coverCard && (compact || playerCard),
      'p-4': !coverCard && !compact && !playerCard,
      'outline-1 outline-dashed outline-offset-2 outline-gray-400 dark:outline-gray-600': editing,
      'cursor-grab select-none active:cursor-grabbing': editing && !disabled,
      'touch-none': editing && selected && !disabled,
      'touch-pan-y': editing && !selected,
      'ring-2 ring-primary': editing && selected,
    }"
    @pointerdown="pointerDown"
    @click.capture="click"
    @focus="emit('select')"
    @keydown="emit('key', $event)"
    @dragstart.prevent
  >
    <header
      v-if="showTitle"
      class="flex min-w-0 items-center gap-2"
      :class="{
        'p-2': coverCard && compact,
        'p-4': coverCard && !compact,
        'mb-2': !coverCard && (compact || playerCard),
        'mb-4': !coverCard && !compact && !playerCard,
      }"
    >
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
