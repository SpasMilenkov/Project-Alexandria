<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, useTemplateRef } from "vue";

import type { GalleryPhoto } from "@/api/gallery";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { type PhotoPick, photoClick } from "@/features/gallery/selection";

const { photo, width, height, selected, selecting } = defineProps<{
  photo: GalleryPhoto;
  width: number;
  height: number;
  selected: boolean;
  // Once anything is selected, a tap picks photos instead of opening them.
  selecting: boolean;
}>();

const emit = defineEmits<{
  open: [photo: GalleryPhoto];
  pick: [photo: GalleryPhoto, how: PhotoPick];
}>();

// Holding a finger on a photo starts selecting, the way phone galleries do. The click that
// follows the lift is swallowed so the photo does not open as well.
const LONG_PRESS_MS = 450;
const LONG_PRESS_SLOP_PX = 10;

const { thumbnailErrored, thumbnailLoaded, thumbnailUrl, onThumbnailError, onThumbnailLoad } =
  useFileThumbnail(() => ({
    fileId: photo.fileId,
    mimeType: photo.mimeType,
    versionId: photo.versionId,
  }));

const image = useTemplateRef<HTMLImageElement>("image");
const fadesIn = ref(false);

// The scroller hands rows to different views whenever the timeline changes, which remounts
// their tiles. An image the browser already holds is shown at once; fading it in again
// would blink every photo on screen each time a month loads.
onMounted(() => {
  const element = image.value;

  if (element?.complete && element.naturalWidth > 0) {
    onThumbnailLoad();
    return;
  }

  fadesIn.value = true;
});

let pressTimer: ReturnType<typeof setTimeout> | null = null;
// Not every browser says on the click itself whether a finger or a mouse made it.
let lastPointerType = "mouse";
let pressOrigin: { x: number; y: number } | null = null;
let swallowClick = false;

const cancelPress = () => {
  if (pressTimer !== null) clearTimeout(pressTimer);
  pressTimer = null;
  pressOrigin = null;
};

onBeforeUnmount(cancelPress);

const onPointerDown = (event: PointerEvent) => {
  lastPointerType = event.pointerType;
  if (event.pointerType === "mouse") return;

  cancelPress();
  pressOrigin = { x: event.clientX, y: event.clientY };
  pressTimer = setTimeout(() => {
    pressTimer = null;
    swallowClick = true;
    emit("pick", photo, "toggle");
  }, LONG_PRESS_MS);
};

// Scrolling the grid moves the finger, which must never count as a press.
const onPointerMove = (event: PointerEvent) => {
  if (!pressOrigin) return;

  const moved = Math.hypot(event.clientX - pressOrigin.x, event.clientY - pressOrigin.y);
  if (moved > LONG_PRESS_SLOP_PX) cancelPress();
};

const onClick = (event: MouseEvent) => {
  if (swallowClick) {
    swallowClick = false;
    return;
  }

  const how = photoClick({
    selecting,
    shift: event.shiftKey,
    toggleKey: event.ctrlKey || event.metaKey,
    touch: lastPointerType !== "mouse",
  });

  if (how === "open") emit("open", photo);
  else emit("pick", photo, how);
};

// A held finger would also open the browser's image menu on top of starting a selection.
const onContextMenu = (event: MouseEvent) => {
  if (pressOrigin || swallowClick) event.preventDefault();
};

// The check only ever adds or removes; it never narrows the selection to one photo.
const onCheck = (event: MouseEvent) => {
  const how = photoClick({
    selecting: true,
    shift: event.shiftKey,
    toggleKey: event.ctrlKey || event.metaKey,
    touch: true,
  });

  emit("pick", photo, how === "open" ? "toggle" : how);
};
</script>

<template>
  <div
    class="group relative shrink-0 overflow-hidden rounded-md"
    :class="{ 'bg-primary/15': selected }"
    :style="{ height: `${height}px`, width: `${width}px` }"
  >
    <!-- Thumbnails are square with transparent padding around the photo. A tile shaped like the
         photo plus object-cover crops exactly to the photo and hides that padding. -->
    <button
      type="button"
      class="relative size-full overflow-hidden rounded-md bg-black/[0.04] transition-transform duration-150 ease-out [-webkit-touch-callout:none] select-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary motion-reduce:transition-none dark:bg-white/[0.06]"
      :class="{ 'scale-[0.88]': selected }"
      :aria-label="photo.name"
      :aria-pressed="selecting ? selected : undefined"
      :title="photo.name"
      @click="onClick"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="cancelPress"
      @pointercancel="cancelPress"
      @contextmenu="onContextMenu"
    >
      <img
        v-if="!thumbnailErrored"
        ref="image"
        :src="thumbnailUrl"
        alt=""
        loading="lazy"
        draggable="false"
        class="size-full object-cover"
        :class="{
          'opacity-0': !thumbnailLoaded,
          'transition-opacity duration-200 ease-out': fadesIn,
        }"
        @load="onThumbnailLoad"
        @error="onThumbnailError"
      />

      <span v-else class="absolute inset-0 flex items-center justify-center">
        <UIcon name="i-lucide-image-off" class="size-5 text-gray-500 dark:text-gray-500" />
      </span>

      <!-- Keeps the check readable over bright photos while the pointer is on the tile. -->
      <span
        class="pointer-events-none absolute inset-x-0 top-0 h-12 bg-linear-to-b from-black/40 to-transparent opacity-0 transition-opacity duration-150 group-hover:opacity-100 motion-reduce:transition-none"
        :class="{ 'opacity-100': selecting && !selected }"
      />
    </button>

    <!-- Hover shows the check on a mouse; on touch screens a long press starts selecting and
         the checks stay visible from then on. -->
    <button
      type="button"
      class="absolute top-1.5 left-1.5 flex size-6 items-center justify-center rounded-xl border-2 transition-opacity duration-150 motion-reduce:transition-none"
      :class="[
        selected
          ? 'border-primary bg-primary text-white opacity-100'
          : 'border-white bg-black/20 text-transparent shadow-sm hover:text-white',
        selecting || selected
          ? 'opacity-100'
          : 'opacity-0 group-hover:opacity-100 focus-visible:opacity-100 pointer-coarse:hidden',
      ]"
      :aria-label="selected ? `Deselect ${photo.name}` : `Select ${photo.name}`"
      :aria-pressed="selected"
      @click="onCheck"
    >
      <UIcon name="i-lucide-check" class="size-3.5" />
    </button>
  </div>
</template>
