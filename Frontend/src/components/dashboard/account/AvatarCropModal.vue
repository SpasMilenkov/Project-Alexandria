<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import {
  AVATAR_MAX_ZOOM,
  AVATAR_MIN_ZOOM,
  type CropFrame,
  type CropState,
  displayScale,
  gifFits,
  isGifFile,
  panBy,
  renderAvatar,
  zoomTo,
} from "@/features/account/avatar-crop";
import { useUploadAvatar } from "@/mutations/user";
import { glassModalContent } from "@/utils/modal-ui";

const VIEWPORT = 256;
const KEYBOARD_STEP = 12;
const ZOOM_STEP = 0.1;

const { file } = defineProps<{ file: File | null }>();

const emit = defineEmits<{ close: [] }>();

const appToast = useAppToast("account");
const { mutateAsync: uploadAvatar, isLoading: isSaving } = useUploadAvatar();

const imageUrl = ref<string | null>(null);
const imageElement = ref<HTMLImageElement | null>(null);
const frame = ref<CropFrame | null>(null);
const crop = ref<CropState>({ offsetX: 0, offsetY: 0, zoom: 1 });

// A canvas re-encode keeps only the first frame, so GIFs are previewed and uploaded untouched.
const isGif = computed(() => isGifFile(file));
const gifTooLarge = ref(false);
const canCrop = computed(() => frame.value !== null && !isGif.value);

const copy = computed(() => {
  if (isGif.value) {
    return {
      description: "GIFs keep their animation, so they're centred in the circle instead of cropped.",
      title: "Preview your picture",
    };
  }

  return {
    description: "Drag to move it and zoom to fit. The circle is what everyone sees.",
    title: "Position your picture",
  };
});

const revokeImage = () => {
  if (imageUrl.value) URL.revokeObjectURL(imageUrl.value);

  imageUrl.value = null;
  imageElement.value = null;
  frame.value = null;
  gifTooLarge.value = false;
};

watch(
  () => file,
  (next) => {
    revokeImage();
    crop.value = { offsetX: 0, offsetY: 0, zoom: 1 };

    if (next) imageUrl.value = URL.createObjectURL(next);
  },
  { immediate: true },
);

onBeforeUnmount(revokeImage);

const handleImageLoad = (event: Event) => {
  const image = event.target as HTMLImageElement;

  imageElement.value = image;
  gifTooLarge.value = isGif.value && !gifFits(image.naturalWidth, image.naturalHeight);
  frame.value = {
    naturalHeight: image.naturalHeight,
    naturalWidth: image.naturalWidth,
    viewport: VIEWPORT,
  };
};

const handleImageError = () => {
  appToast.error("Couldn't open that picture", "Try a JPEG, PNG or WebP image.");
  emit("close");
};

const imageStyle = computed(() => {
  if (!frame.value) return { opacity: 0 };
  if (isGif.value) return undefined;

  const scale = displayScale(frame.value, crop.value.zoom);

  return {
    height: `${frame.value.naturalHeight * scale}px`,
    transform: `translate(-50%, -50%) translate(${crop.value.offsetX}px, ${crop.value.offsetY}px)`,
    width: `${frame.value.naturalWidth * scale}px`,
  };
});

const imageClass = computed(() => {
  if (isGif.value) return "inset-0 size-full object-cover";

  return "top-1/2 left-1/2 max-w-none";
});

const viewportLabel = computed(() => {
  if (isGif.value) return "Preview of your animated picture";

  return "Picture crop area. Use the arrow keys to move and plus or minus to zoom.";
});

const zoom = computed({
  get: () => crop.value.zoom,
  set: (value: number) => {
    if (!frame.value) return;

    crop.value = zoomTo(frame.value, crop.value, value);
  },
});

const nudgeZoom = (delta: number) => {
  zoom.value = crop.value.zoom + delta;
};

// Pointer capture keeps the drag alive when the pointer leaves the viewport.
let dragOrigin: { x: number; y: number } | null = null;

const handlePointerDown = (event: PointerEvent) => {
  if (!canCrop.value) return;

  (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  dragOrigin = { x: event.clientX, y: event.clientY };
};

const handlePointerMove = (event: PointerEvent) => {
  if (!dragOrigin || !frame.value || !canCrop.value) return;

  crop.value = panBy(
    frame.value,
    crop.value,
    event.clientX - dragOrigin.x,
    event.clientY - dragOrigin.y,
  );
  dragOrigin = { x: event.clientX, y: event.clientY };
};

const handlePointerUp = () => {
  dragOrigin = null;
};

const handleWheel = (event: WheelEvent) => {
  if (!canCrop.value) return;

  nudgeZoom(event.deltaY < 0 ? ZOOM_STEP : -ZOOM_STEP);
};

// Arrows move the picture itself, the same way a drag does.
const KEY_PANS: Record<string, [number, number]> = {
  ArrowDown: [0, KEYBOARD_STEP],
  ArrowLeft: [-KEYBOARD_STEP, 0],
  ArrowRight: [KEYBOARD_STEP, 0],
  ArrowUp: [0, -KEYBOARD_STEP],
};

const handleKeydown = (event: KeyboardEvent) => {
  if (!frame.value || !canCrop.value) return;

  if (event.key === "+" || event.key === "=") {
    event.preventDefault();
    nudgeZoom(ZOOM_STEP);
    return;
  }

  if (event.key === "-") {
    event.preventDefault();
    nudgeZoom(-ZOOM_STEP);
    return;
  }

  const pan = KEY_PANS[event.key];
  if (!pan) return;

  event.preventDefault();
  crop.value = panBy(frame.value, crop.value, pan[0], pan[1]);
};

const prepareUpload = (): Promise<Blob | null> => {
  if (!frame.value || !imageElement.value || !file) return Promise.resolve(null);
  if (isGif.value) return Promise.resolve(file);

  return renderAvatar(imageElement.value, frame.value, crop.value);
};

const save = async () => {
  if (isSaving.value || gifTooLarge.value) return;

  try {
    const blob = await prepareUpload();
    if (!blob) return;

    await uploadAvatar(blob);

    appToast.success("Profile picture updated");
    emit("close");
  } catch (err) {
    appToast.error("Couldn't save your picture", err);
  }
};

const dismiss = () => {
  if (isSaving.value) return;

  emit("close");
};
</script>

<template>
  <UModal
    :open="file !== null"
    :title="copy.title"
    :description="copy.description"
    :close="{ onClick: dismiss }"
    :dismissible="!isSaving"
    :ui="{ content: `${glassModalContent} sm:max-w-md` }"
    @update:open="!$event && dismiss()"
  >
    <template #body>
      <div class="flex flex-col items-center gap-6">
        <div
          class="relative touch-none overflow-hidden rounded-2xl bg-gray-500/10 select-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          :class="{ 'cursor-grab active:cursor-grabbing': !isGif }"
          :style="{ height: `${VIEWPORT}px`, width: `${VIEWPORT}px` }"
          :role="isGif ? 'img' : 'application'"
          :aria-label="viewportLabel"
          :tabindex="isGif ? -1 : 0"
          @pointerdown="handlePointerDown"
          @pointermove="handlePointerMove"
          @pointerup="handlePointerUp"
          @pointercancel="handlePointerUp"
          @wheel.prevent="handleWheel"
          @keydown="handleKeydown"
        >
          <img
            v-if="imageUrl"
            :src="imageUrl"
            alt=""
            draggable="false"
            class="pointer-events-none absolute"
            :class="imageClass"
            :style="imageStyle"
            @load="handleImageLoad"
            @error="handleImageError"
          />

          <span
            class="pointer-events-none absolute inset-0 rounded-full shadow-[0_0_0_9999px_rgb(0_0_0/0.45)] ring-2 ring-white/80"
            aria-hidden="true"
          />
        </div>

        <UAlert
          v-if="gifTooLarge"
          color="warning"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="This GIF is too large"
          description="Animated pictures can be at most 1024 by 1024 pixels. Resize it and try again."
          class="w-full"
        />

        <div v-if="!isGif" class="flex w-full max-w-64 items-center gap-3">
          <UButton
            icon="i-lucide-zoom-out"
            color="neutral"
            variant="ghost"
            size="sm"
            square
            class="justify-center"
            aria-label="Zoom out"
            :disabled="!frame || zoom <= AVATAR_MIN_ZOOM"
            @click="nudgeZoom(-ZOOM_STEP)"
          />

          <USlider
            v-model="zoom"
            :min="AVATAR_MIN_ZOOM"
            :max="AVATAR_MAX_ZOOM"
            :step="0.01"
            :disabled="!frame"
            aria-label="Zoom"
            class="flex-1"
          />

          <UButton
            icon="i-lucide-zoom-in"
            color="neutral"
            variant="ghost"
            size="sm"
            square
            class="justify-center"
            aria-label="Zoom in"
            :disabled="!frame || zoom >= AVATAR_MAX_ZOOM"
            @click="nudgeZoom(ZOOM_STEP)"
          />
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="outline"
          :disabled="isSaving"
          @click="dismiss"
        />

        <UButton
          label="Save picture"
          color="primary"
          icon="i-lucide-check"
          :loading="isSaving"
          :disabled="!frame || gifTooLarge"
          @click="save"
        />
      </div>
    </template>
  </UModal>
</template>
