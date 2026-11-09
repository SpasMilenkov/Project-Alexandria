<script setup lang="ts">
import { useQuery, useQueryCache } from "@pinia/colada";
import { computed, ref, useTemplateRef, watch } from "vue";

import type { GalleryPhoto } from "@/api/gallery";

import { type PreviewResultDto, fileApi } from "@/api/file";
import { useImageZoom } from "@/composables/useImageZoom";
import { browserCanShowOriginal, prefersOriginal } from "@/features/gallery/viewer";
import { FILES_QUERY_KEYS, getPreviewByVersion, getVersionDownloadUrl } from "@/queries/files";

const { photo, hasPrevious, hasNext } = defineProps<{
  photo: GalleryPhoto;
  hasPrevious: boolean;
  hasNext: boolean;
}>();

const emit = defineEmits<{ turn: [direction: 1 | -1]; close: []; zoomed: [zoomed: boolean] }>();

const queryCache = useQueryCache();

// The grid already holds this exact URL, so the photo is on screen the moment the viewer opens
// and the sharper image fades in over it.
const thumbnailUrl = computed(() =>
  fileApi.getThumbnailUrlForVersion(photo.fileId, photo.versionId),
);

const skipsPreview = computed(() => prefersOriginal(photo.mimeType));

const preview = useQuery(() => ({
  ...getPreviewByVersion({
    cached: queryCache.getQueryData<PreviewResultDto>(
      FILES_QUERY_KEYS.previewByVersion(photo.fileId, photo.versionId),
    ),
    fileId: photo.fileId,
    mimeType: photo.mimeType,
    versionId: photo.versionId,
  }),
  enabled: !skipsPreview.value,
}));

const previewUrl = computed(() => preview.data.value?.previewUrl ?? null);

// The preview is rendered by a worker and answers "not yet" until it exists. A format the
// browser can draw shows the original meanwhile instead of a stretched thumbnail.
const previewMissing = computed(
  () =>
    preview.status.value === "error" || (preview.status.value === "success" && !previewUrl.value),
);

const wantsOriginal = computed(
  () => skipsPreview.value || (previewMissing.value && browserCanShowOriginal(photo.mimeType)),
);

const view = useTemplateRef<HTMLElement>("view");
const box = useTemplateRef<HTMLElement>("box");
const frame = useTemplateRef<HTMLElement>("frame");

const zoom = useImageZoom(
  { box, frame, view },
  {
    canTurn: (direction) => (direction === 1 ? hasNext : hasPrevious),
    onClose: () => emit("close"),
    onTurn: (direction) => emit("turn", direction),
  },
);

watch(zoom.zoomed, (zoomed) => emit("zoomed", zoomed));

// The preview tops out at 1920x1080, which turns soft once zoomed. Zooming in fetches the
// original once and lays it over the preview, as long as the browser can draw it.
const detailRequested = ref(false);

watch(zoom.zoomed, (zoomed) => {
  if (zoomed && browserCanShowOriginal(photo.mimeType)) detailRequested.value = true;
});

const original = useQuery(() => ({
  ...getVersionDownloadUrl(photo.versionId),
  enabled: wantsOriginal.value || detailRequested.value,
}));

const originalUrl = computed(() => original.data.value?.presignedUrl ?? null);
const detailUrl = computed(() =>
  detailRequested.value && !wantsOriginal.value ? originalUrl.value : null,
);
const detailLoaded = ref(false);

const fullUrl = computed(() => {
  if (wantsOriginal.value) return originalUrl.value;

  return previewUrl.value;
});

const stillPreparing = computed(
  () => previewMissing.value && !browserCanShowOriginal(photo.mimeType),
);

const fullLoaded = ref(false);
const naturalAspect = ref<number | null>(null);

const onFullLoad = (event: Event) => {
  const image = event.target as HTMLImageElement;

  if (image.naturalWidth > 0 && image.naturalHeight > 0) {
    naturalAspect.value = image.naturalWidth / image.naturalHeight;
  }

  fullLoaded.value = true;
};

// The loaded image is the truth for its shape: stored dimensions can predate an orientation
// fix, and unknown ones fall back to square until the image says otherwise.
const aspect = computed(() => {
  if (naturalAspect.value) return naturalAspect.value;
  if (photo.width && photo.height) return photo.width / photo.height;

  return 1;
});

// Sized from the stage's own box, so the frame fits either way round without measuring.
const frameStyle = computed(() => ({
  aspectRatio: String(aspect.value),
  transform: zoom.transform.value,
  width: `min(100cqw, calc(100cqh * ${aspect.value}))`,
}));

const cursor = computed(() => {
  if (!zoom.zoomed.value) return "pointer-fine:cursor-zoom-in";
  if (zoom.panning.value) return "cursor-grabbing";

  return "cursor-grab";
});

// Eased zooms decelerate hard and settle softly, which reads as the photo being placed rather
// than sliding. Wheel notches use a shorter version so quick scrolling never lags behind.
const EASINGS = {
  direct: "",
  quick: "transition-transform duration-150 ease-[cubic-bezier(0.2,0,0,1)]",
  smooth: "transition-transform duration-300 ease-[cubic-bezier(0.2,0,0,1)]",
} as const;

const frameMotion = computed(() => EASINGS[zoom.easing.value]);

defineExpose({
  reset: zoom.reset,
  settle: zoom.settle,
  toggle: zoom.toggle,
  zoomBy: zoom.zoomBy,
});
</script>

<template>
  <!-- Takes every gesture itself, so neither the browser's own pinch nor page scrolling
       competes with zooming and panning the photo. -->
  <div
    ref="view"
    class="absolute inset-0 touch-none overflow-hidden select-none"
    :class="cursor"
    @pointerdown="zoom.onPointerDown"
    @pointermove="zoom.onPointerMove"
    @pointerup="zoom.onPointerUp"
    @pointercancel="zoom.onPointerUp"
    @wheel="zoom.onWheel"
  >
    <div
      ref="box"
      class="flex size-full items-center justify-center px-2 pt-16 pb-4 [container-type:size] sm:px-20 sm:pb-8"
    >
      <div
        ref="frame"
        class="relative shrink-0 overflow-hidden motion-reduce:transition-none"
        :class="frameMotion"
        :style="frameStyle"
      >
        <!-- Thumbnails are square with transparent padding; cover inside a frame shaped like
             the photo crops exactly to the photo, the same way the grid tile does. -->
        <img
          :src="thumbnailUrl"
          alt=""
          draggable="false"
          class="absolute inset-0 size-full object-cover"
        />

        <img
          v-if="fullUrl"
          :key="fullUrl"
          :src="fullUrl"
          :alt="photo.name"
          draggable="false"
          class="absolute inset-0 size-full object-contain transition-opacity duration-200 ease-out motion-reduce:transition-none"
          :class="fullLoaded ? 'opacity-100' : 'opacity-0'"
          @load="onFullLoad"
        />

        <img
          v-if="detailUrl"
          :src="detailUrl"
          alt=""
          draggable="false"
          class="absolute inset-0 size-full object-contain transition-opacity duration-200 ease-out motion-reduce:transition-none"
          :class="detailLoaded ? 'opacity-100' : 'opacity-0'"
          @load="detailLoaded = true"
        />
      </div>
    </div>

    <p
      v-if="stillPreparing"
      class="pointer-events-none absolute bottom-3 left-1/2 m-0 -translate-x-1/2 rounded-xl bg-white/80 px-3 py-1 text-xs text-gray-700 dark:bg-black/60 dark:text-gray-200"
    >
      The full-size preview is still being prepared
    </p>
  </div>
</template>
