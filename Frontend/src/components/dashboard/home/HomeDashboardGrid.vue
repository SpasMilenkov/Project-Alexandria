<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { HomeWidgetType } from "@/enums/home-widget";
import type { HomeLayout, HomeWidget } from "@/types/home";

import HomeWidgetCard from "@/components/dashboard/home/HomeWidgetCard.vue";
import HomeWidgetResizeHandles from "@/components/dashboard/home/HomeWidgetResizeHandles.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { useHomeGridDrag } from "@/composables/useHomeGridDrag";
import { useHomeGridMotion } from "@/composables/useHomeGridMotion";
import {
  HOME_GRID_MAX_ROWS,
  homeSpatialOrder,
  homeWidgetFootprint,
  previewHomePlacement,
  projectHomeLayout,
} from "@/utils/home-grid";
import {
  type HomeResizeEdge,
  homeArrowOffset,
  homeDragFromControl,
  homeResizeByCells,
} from "@/utils/home-grid-input";
import { createHomeWidget } from "@/utils/home-layout";

const {
  layout,
  editing,
  disabled,
  selectedId,
  mobile = false,
} = defineProps<{
  layout: HomeLayout;
  editing: boolean;
  disabled: boolean;
  selectedId: string | null;
  mobile?: boolean;
}>();

const emit = defineEmits<{
  select: [instanceId: string];
  place: [widget: HomeWidget];
  nudge: [instanceId: string, x: number, y: number];
  announce: [message: string];
  dragging: [active: boolean];
}>();

const board = ref<HTMLElement | null>(null);

const renderedLayout = computed(() => {
  if (mobile && layout.columns === 12) return projectHomeLayout(layout, 4);

  return layout;
});

const widgets = computed(() => [...renderedLayout.value.widgets].sort(homeSpatialOrder));

const drag = useHomeGridDrag({
  board: () => board.value,
  layout: () => layout,
  enabled: () => editing && !disabled,
  select: (id) => emit("select", id),
  place: (widget) => emit("place", widget),
  announce: (message) => emit("announce", message),
});

const { active, candidate, preview, resizing } = drag;

const motion = useHomeGridMotion({
  board: () => board.value,
  enabled: () => editing && !disabled,
  context: () => `${mobile}:${renderedLayout.value.columns}`,
  active: () => active.value,
});

const previewById = computed(
  () => new Map(preview.value?.widgets.map((widget) => [widget.instanceId, widget])),
);

const rows = computed(() => {
  let extent = Math.max(0, ...widgets.value.map((widget) => widget.y + widget.height));

  if (preview.value)
    extent = Math.max(extent, ...preview.value.widgets.map((widget) => widget.y + widget.height));
  if (candidate.value) extent = Math.max(extent, candidate.value.y + candidate.value.height);
  if (editing) extent += 6;

  return Math.min(HOME_GRID_MAX_ROWS, Math.max(editing ? 8 : 0, extent));
});

const slotStyle = (widget: HomeWidget) => ({
  gridColumn: `${widget.x + 1} / span ${widget.width}`,
  gridRow: `${widget.y + 1} / span ${widget.height}`,
});

const motionPosition = (widget: HomeWidget) =>
  `${widget.x}:${widget.y}:${widget.width}:${widget.height}`;

const renderedPosition = (widget: HomeWidget) => {
  if (candidate.value?.instanceId === widget.instanceId) {
    if (resizing.value && preview.value) return candidate.value;

    return widget;
  }

  return previewById.value.get(widget.instanceId) ?? widget;
};

const keyDown = (widget: HomeWidget, event: KeyboardEvent) => {
  if (
    !editing ||
    disabled ||
    active.value ||
    homeDragFromControl(event.target) ||
    event.altKey ||
    event.ctrlKey ||
    event.metaKey
  )
    return;

  const offset = homeArrowOffset(event.key);

  if (offset) {
    event.preventDefault();
    emit("nudge", widget.instanceId, offset.x, offset.y);
  } else if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    emit("select", widget.instanceId);
  }
};

const resizeByKey = (widget: HomeWidget, edge: HomeResizeEdge, x: number, y: number) => {
  if (!editing || disabled || active.value) return;

  const resized = homeResizeByCells(widget, layout.columns, edge, x, y);

  if (resized.width === widget.width && resized.height === widget.height) return;

  if (previewHomePlacement(layout, resized)) {
    emit("place", resized);
    emit("announce", `Widget resized to ${resized.width} columns by ${resized.height} rows.`);
  } else {
    emit("announce", "That size does not fit. Your layout has not changed.");
  }
};

const startCatalogueDrag = (type: HomeWidgetType, event: PointerEvent) => {
  if (layout.widgets.length >= 32) return;

  motion.cancel();

  const widget = createHomeWidget(type, crypto.randomUUID());
  Object.assign(widget, homeWidgetFootprint(widget.size, layout.columns));
  widget.options = { ...HOME_WIDGET_REGISTRY[type].defaultOptions };

  drag.start(widget, event, true);
};

watch(active, (value) => emit("dragging", value), { flush: "sync" });

watch(() => mobile, drag.cancel);

defineExpose({ startCatalogueDrag, cancel: drag.cancel });
</script>

<template>
  <div
    ref="board"
    class="home-grid relative isolate"
    aria-label="Home widgets"
    :data-grid-columns="renderedLayout.columns"
    :data-editing="editing"
    :data-dragging="active"
    :data-resizing="active && resizing"
    @pointerdown.capture="motion.cancel"
    :style="{
      gridTemplateColumns: `repeat(${renderedLayout.columns}, minmax(0, 1fr))`,
      gridTemplateRows: `repeat(${rows}, 4rem)`,
      '--home-columns': renderedLayout.columns,
    }"
  >
    <div
      v-if="editing"
      class="grid-guidance pointer-events-none absolute inset-0 z-0 rounded-2xl"
      aria-hidden="true"
    />

    <div
      v-for="widget in widgets"
      :key="widget.instanceId"
      :data-home-motion="widget.instanceId"
      :data-motion-position="motionPosition(renderedPosition(widget))"
      :data-motion-hidden="active && !resizing && candidate?.instanceId === widget.instanceId"
      class="relative z-[1] min-h-0 min-w-0 rounded-2xl"
      :class="{ invisible: active && !resizing && candidate?.instanceId === widget.instanceId }"
      :style="slotStyle(renderedPosition(widget))"
    >
      <HomeWidgetCard
        :widget="renderedPosition(widget)"
        :editing="editing"
        :disabled="disabled"
        :selected="selectedId === widget.instanceId"
        class="h-full"
        @select="emit('select', widget.instanceId)"
        @pointer="drag.start(widget, $event)"
        @key="keyDown(widget, $event)"
      />

      <HomeWidgetResizeHandles
        v-if="editing && !disabled && selectedId === widget.instanceId"
        :label="HOME_WIDGET_REGISTRY[widget.type].label"
        :help-id="`${widget.instanceId}-resize-help`"
        @start="(edge, event) => drag.startResize(widget, edge, event)"
        @resize="(edge, x, y) => resizeByKey(widget, edge, x, y)"
      />

      <p v-if="editing" :id="`${widget.instanceId}-resize-help`" class="sr-only">
        Drag an edge or corner to resize. Arrow keys on a resize handle change the size one cell.
        Escape cancels the active gesture.
      </p>
    </div>

    <div
      v-if="active && candidate"
      data-placement-ghost
      data-home-motion="placement"
      :data-motion-widget="candidate.instanceId"
      :data-motion-position="motionPosition(candidate)"
      :data-valid="Boolean(preview)"
      :data-column="candidate.x"
      :data-row="candidate.y"
      :data-width="candidate.width"
      :data-height="candidate.height"
      class="pointer-events-none z-10 flex items-center justify-center rounded-2xl border-2 border-dashed p-4 text-center"
      :class="[
        preview ? 'border-primary bg-primary/10' : 'border-error bg-error/10',
        { 'items-end': resizing },
      ]"
      :style="slotStyle(candidate)"
      role="status"
    >
      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
        <template v-if="!preview">Cannot place here</template>
        <template v-else-if="resizing">
          {{ candidate.width }} × {{ candidate.height }}
          <span class="sr-only">columns by rows</span>
        </template>
        <template v-else>{{ HOME_WIDGET_REGISTRY[candidate.type].label }}</template>
      </p>
    </div>
  </div>
</template>

<style scoped>
.home-grid {
  display: grid;
  grid-auto-rows: 4rem;
  gap: 1rem;
  align-items: stretch;
}

.grid-guidance {
  background-image:
    linear-gradient(to right, var(--color-gray-300) 1px, transparent 1px),
    linear-gradient(to bottom, var(--color-gray-300) 1px, transparent 1px);
  background-size: calc((100% + 1rem) / var(--home-columns)) 5rem;
  opacity: 0.35;
}

.grid-guidance:where(.dark *) {
  background-image:
    linear-gradient(to right, var(--color-gray-600) 1px, transparent 1px),
    linear-gradient(to bottom, var(--color-gray-600) 1px, transparent 1px);
}
</style>
