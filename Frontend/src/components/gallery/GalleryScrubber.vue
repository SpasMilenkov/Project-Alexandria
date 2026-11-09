<script setup lang="ts">
import { useElementSize, useMediaQuery, useTimeoutFn } from "@vueuse/core";
import { computed, ref, useTemplateRef, watch } from "vue";

import { type TimelineLayout, monthDots, sectionAt, yearMarks } from "@/features/gallery/timeline";

const { layout, scrollTop, viewportHeight } = defineProps<{
  layout: TimelineLayout;
  scrollTop: number;
  viewportHeight: number;
}>();

const emit = defineEmits<{ seek: [offset: number]; scrollBy: [delta: number] }>();

// The scrubber floats over the grid's right edge, so it never changes the scroller's width.
// At rest it is an invisible strip over the grid's right padding; hovering, dragging or
// focusing it shows the rail and widens the hit area so the year labels beside it can be
// read and clicked.
const HIT_COLLAPSED_PX = 24;
const HIT_EXPANDED_PX = 80;

// Each label pill is about 26px tall, so this keeps neighbours from touching.
const MIN_LABEL_GAP_PX = 32;
const MIN_DOT_GAP_PX = 6;
const KEY_STEP_FRACTION = 0.02;

// Marks near the pointer grow slightly, fading out over this distance, to show which part of
// the timeline the pointer is reading without moving anything around.
const ZOOM_RADIUS_PX = 72;
const LABEL_ZOOM = 0.2;
const DOT_ZOOM = 0.9;

const track = useTemplateRef<HTMLElement>("track");
const { height: trackHeight } = useElementSize(track);

const dragging = ref(false);
const hovering = ref(false);
const focused = ref(false);
const pointerFraction = ref<number | null>(null);

const expanded = computed(() => hovering.value || dragging.value || focused.value);

// Touch screens have no hover to reveal the scrubber, so it shows while the grid scrolls and
// fades once scrolling stops, the way phone galleries let people find it. Only the visuals
// show: the hit area stays the narrow edge strip, so a flick near the edge still scrolls.
const SCROLL_REVEAL_MS = 1200;
const touchOnly = useMediaQuery("(hover: none)");
const scrolling = ref(false);
const { start: hideAfterScroll } = useTimeoutFn(
  () => {
    scrolling.value = false;
  },
  SCROLL_REVEAL_MS,
  { immediate: false },
);

watch(
  () => scrollTop,
  () => {
    if (!touchOnly.value) return;

    scrolling.value = true;
    hideAfterScroll();
  },
);

const visibility = computed(() =>
  expanded.value || scrolling.value ? "opacity-100" : "opacity-0",
);

const scrollRange = computed(() => Math.max(1, layout.totalHeight - viewportHeight));
const scrollFraction = computed(() => Math.min(1, Math.max(0, scrollTop / scrollRange.value)));

const marks = computed(() =>
  yearMarks(layout, scrollRange.value, trackHeight.value, MIN_LABEL_GAP_PX),
);

const dots = computed(() =>
  monthDots(layout, scrollRange.value, trackHeight.value, MIN_DOT_GAP_PX),
);

const toPx = (fraction: number) => `${fraction * trackHeight.value}px`;

// Smoothstep falloff: full size at the pointer, easing to nothing at the radius edge.
const closeness = (fraction: number) => {
  if (pointerFraction.value === null) return 0;

  const distance = Math.abs(fraction - pointerFraction.value) * trackHeight.value;
  const linear = Math.max(0, 1 - distance / ZOOM_RADIUS_PX);

  return linear * linear * (3 - 2 * linear);
};

const labelStyle = (fraction: number) => ({
  top: toPx(fraction),
  transform: `translateY(-50%) scale(${1 + LABEL_ZOOM * closeness(fraction)})`,
});

const dotStyle = (fraction: number) => ({
  top: toPx(fraction),
  transform: `translateY(-50%) scale(${1 + DOT_ZOOM * closeness(fraction)})`,
});

const isNearPointer = (fraction: number) => closeness(fraction) > 0.5;

const labelAt = (fraction: number) =>
  sectionAt(layout.sections, fraction * scrollRange.value)?.label ?? "";

const currentLabel = computed(() => labelAt(scrollFraction.value));
const pointerLabel = computed(() =>
  pointerFraction.value === null ? "" : labelAt(pointerFraction.value),
);

const fractionAt = (event: PointerEvent) => {
  const rect = track.value?.getBoundingClientRect();
  if (!rect || rect.height === 0) return 0;

  return Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height));
};

const seekToFraction = (fraction: number) => {
  emit("seek", fraction * scrollRange.value);
};

const onPointerEnter = (event: PointerEvent) => {
  if (event.pointerType === "mouse") hovering.value = true;
};

const onPointerDown = (event: PointerEvent) => {
  if (event.button !== 0) return;

  dragging.value = true;
  (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);

  pointerFraction.value = fractionAt(event);
  seekToFraction(pointerFraction.value);
};

const onPointerMove = (event: PointerEvent) => {
  if (!expanded.value) return;

  pointerFraction.value = fractionAt(event);

  if (dragging.value) seekToFraction(pointerFraction.value);
};

const onPointerUp = (event: PointerEvent) => {
  dragging.value = false;
  (event.currentTarget as HTMLElement).releasePointerCapture(event.pointerId);

  if (event.pointerType !== "mouse") pointerFraction.value = null;
};

const onPointerLeave = () => {
  hovering.value = false;

  if (!dragging.value) pointerFraction.value = null;
};

// The rail sits beside the scroller rather than inside it, so wheel and touchpad scrolling over
// it would otherwise go nowhere. Forwarding keeps the grid scrolling while the pointer is here.
// Ctrl+wheel is a pinch zoom and stays with the browser.
const LINE_HEIGHT_PX = 16;

const onWheel = (event: WheelEvent) => {
  if (event.ctrlKey || event.deltaY === 0) return;

  event.preventDefault();

  let delta = event.deltaY;
  if (event.deltaMode === WheelEvent.DOM_DELTA_LINE) delta *= LINE_HEIGHT_PX;
  else if (event.deltaMode === WheelEvent.DOM_DELTA_PAGE) delta *= viewportHeight;

  emit("scrollBy", delta);
};

const KEY_TARGETS: Record<string, (fraction: number) => number> = {
  ArrowDown: (fraction) => fraction + KEY_STEP_FRACTION,
  ArrowUp: (fraction) => fraction - KEY_STEP_FRACTION,
  End: () => 1,
  Home: () => 0,
  PageDown: (fraction) => fraction + KEY_STEP_FRACTION * 5,
  PageUp: (fraction) => fraction - KEY_STEP_FRACTION * 5,
};

const onKeydown = (event: KeyboardEvent) => {
  const target = KEY_TARGETS[event.key];
  if (!target) return;

  event.preventDefault();
  seekToFraction(Math.min(1, Math.max(0, target(scrollFraction.value))));
};
</script>

<template>
  <div
    role="scrollbar"
    tabindex="0"
    aria-orientation="vertical"
    aria-label="Timeline"
    :aria-valuenow="Math.round(scrollFraction * 100)"
    aria-valuemin="0"
    aria-valuemax="100"
    :aria-valuetext="currentLabel"
    class="group absolute inset-y-0 right-0 z-10 cursor-row-resize touch-none outline-none select-none"
    :style="{ width: `${expanded ? HIT_EXPANDED_PX : HIT_COLLAPSED_PX}px` }"
    @pointerenter="onPointerEnter"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerUp"
    @pointerleave="onPointerLeave"
    @wheel="onWheel"
    @focus="focused = true"
    @blur="focused = false"
    @keydown="onKeydown"
  >
    <!-- Each piece fades on its own rather than through a shared wrapper: an ancestor below full
         opacity cuts the glass off from the photos behind it, so the blur would only appear
         once the fade finished. The slim glass rail keeps the dots readable over a custom
         background image; the year labels get their own glass so nothing wide covers photos. -->
    <div
      class="absolute inset-y-0 right-1 w-3.5 rounded-xl border border-gray-200/70 shadow-sm frosted-glass glass-surface transition-opacity duration-150 ease-out group-focus-visible:outline-2 group-focus-visible:outline-primary motion-reduce:transition-none dark:border-gray-700/70"
      :class="visibility"
    />

    <!-- Spans the hit area and is right-aligned with the rail, so every mark is placed from the
         rail's right edge: dots and the ring sit 7px in, labels just left of the rail. -->
    <div ref="track" class="pointer-events-none absolute inset-y-3 right-1 left-0">
      <span
        v-for="(dot, index) in dots"
        :key="index"
        class="absolute right-[5px] size-1 rounded-xl bg-gray-500 transition-[opacity,transform] duration-150 ease-out motion-reduce:transition-none dark:bg-gray-400"
        :class="visibility"
        :style="dotStyle(dot)"
      />

      <span
        v-for="mark in marks"
        :key="mark.year"
        class="absolute right-5 origin-right rounded-lg border border-gray-200/70 px-2 py-0.5 text-sm font-semibold tabular-nums shadow-sm frosted-glass glass-surface-strong transition-[opacity,transform,color] duration-150 ease-out motion-reduce:transition-none dark:border-gray-700/70"
        :class="[
          visibility,
          isNearPointer(mark.fraction)
            ? 'text-gray-900 dark:text-gray-100'
            : 'text-gray-600 dark:text-gray-400',
        ]"
        :style="labelStyle(mark.fraction)"
      >
        {{ mark.year }}
      </span>

      <!-- Where the viewport is, centered on the dot column. -->
      <span
        class="absolute right-[2px] size-2.5 -translate-y-1/2 rounded-xl border-2 border-gray-900 transition-opacity duration-150 ease-out motion-reduce:transition-none dark:border-gray-100"
        :class="visibility"
        :style="{ top: toPx(scrollFraction) }"
      />

      <!-- Where a click or drag would land, named above the line. -->
      <template v-if="pointerFraction !== null">
        <span
          class="absolute inset-x-0 h-0.5 -translate-y-1/2 rounded-xl bg-primary shadow-sm"
          :style="{ top: toPx(pointerFraction) }"
        />

        <span
          v-if="pointerLabel"
          class="absolute right-0 z-10 -translate-y-[calc(100%+6px)] rounded-lg border border-gray-200/70 px-2.5 py-1 text-sm font-semibold whitespace-nowrap text-gray-900 shadow-sm frosted-glass glass-surface-strong dark:border-gray-700/70 dark:text-gray-100"
          :style="{ top: toPx(pointerFraction) }"
        >
          {{ pointerLabel }}
        </span>
      </template>
    </div>
  </div>
</template>
