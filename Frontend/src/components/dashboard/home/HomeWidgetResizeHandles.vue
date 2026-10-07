<script setup lang="ts">
import { type HomeResizeEdge, homeArrowOffset } from "@/utils/home-grid-input";

defineProps<{ label: string; helpId: string }>();

const emit = defineEmits<{
  start: [edge: HomeResizeEdge, event: PointerEvent];
  resize: [edge: HomeResizeEdge, x: number, y: number];
}>();

const handles: { edge: HomeResizeEdge; label: string; position: string; grip: string }[] = [
  {
    edge: "top",
    label: "top",
    position: "-top-2 right-4 left-4 h-4 cursor-ns-resize",
    grip: "h-1 w-8",
  },
  {
    edge: "right",
    label: "right",
    position: "top-4 -right-2 bottom-4 w-4 cursor-ew-resize",
    grip: "h-8 w-1",
  },
  {
    edge: "bottom",
    label: "bottom",
    position: "right-4 -bottom-2 left-4 h-4 cursor-ns-resize",
    grip: "h-1 w-8",
  },
  {
    edge: "left",
    label: "left",
    position: "top-4 bottom-4 -left-2 w-4 cursor-ew-resize",
    grip: "h-8 w-1",
  },
  {
    edge: "top-left",
    label: "top left",
    position: "-top-4 -left-4 size-8 cursor-nwse-resize",
    grip: "size-3",
  },
  {
    edge: "top-right",
    label: "top right",
    position: "-top-4 -right-4 size-8 cursor-nesw-resize",
    grip: "size-3",
  },
  {
    edge: "bottom-left",
    label: "bottom left",
    position: "-bottom-4 -left-4 size-8 cursor-nesw-resize",
    grip: "size-3",
  },
  {
    edge: "bottom-right",
    label: "bottom right",
    position: "-right-4 -bottom-4 size-8 cursor-nwse-resize",
    grip: "size-3",
  },
];

const keyDown = (edge: HomeResizeEdge, event: KeyboardEvent) => {
  if (event.altKey || event.ctrlKey || event.metaKey) return;

  const offset = homeArrowOffset(event.key);

  if (!offset) return;

  event.preventDefault();
  event.stopPropagation();
  emit("resize", edge, offset.x, offset.y);
};
</script>

<template>
  <button
    v-for="handle in handles"
    :key="handle.edge"
    type="button"
    :data-resize-edge="handle.edge"
    :aria-label="`Resize ${label} from ${handle.label}`"
    :aria-describedby="helpId"
    class="absolute z-20 flex touch-none items-center justify-center rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
    :class="handle.position"
    @pointerdown.stop="emit('start', handle.edge, $event)"
    @keydown="keyDown(handle.edge, $event)"
    @click.stop
  >
    <span class="rounded-full border-2 border-primary bg-default" :class="handle.grip" />
  </button>
</template>
