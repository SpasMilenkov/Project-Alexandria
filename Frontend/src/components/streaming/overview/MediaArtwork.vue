<script setup lang="ts">
import { usePreferredReducedMotion } from "@vueuse/core";
import { computed, onBeforeUnmount, ref, watch } from "vue";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { sampleCoverColor } from "@/features/media/cover-color";
import { generatedCover } from "@/features/media/generated-cover";
import { useSettingsStore } from "@/stores/settings";

const {
  fileId = undefined,
  versionId = undefined,
  mimeType = undefined,
  seed,
  zoom = false,
  floating = false,
  skeleton = false,
} = defineProps<{
  fileId?: string;
  versionId?: string;
  mimeType?: string;
  seed: string;
  zoom?: boolean;
  floating?: boolean;
  skeleton?: boolean;
}>();

const emit = defineEmits<{ color: [hex: string] }>();

const settings = useSettingsStore();
const image = ref<HTMLImageElement | null>(null);
const cover = computed(() => generatedCover(seed));

const thumbnail = useFileThumbnail(() => ({
  fileId: fileId ?? "",
  versionId,
  mimeType,
}));

const showArtwork = computed(
  () =>
    Boolean(fileId) &&
    settings.thumbnailsEnabled &&
    thumbnail.canHaveThumbnail.value &&
    !thumbnail.thumbnailErrored.value,
);

// Covers without artwork retry for minutes before latching an error, so the skeleton
// gives way to the generated cover after the first failed attempt.
const firstAttemptFailed = ref(false);

// The cover is dropped once the artwork is fully opaque, so transparent pixels and
// sub-pixel edges never show it through the loaded image.
const artworkSettled = ref(false);
const reducedMotion = usePreferredReducedMotion();

// A cached image can load before its hidden state is painted, which skips the fade
// and its transitionend, so a timer slightly longer than the fade settles it anyway.
const SETTLE_FALLBACK_MS = 300;
let settleTimer: ReturnType<typeof setTimeout> | null = null;

const clearSettleTimer = () => {
  if (settleTimer === null) return;

  clearTimeout(settleTimer);
  settleTimer = null;
};

onBeforeUnmount(clearSettleTimer);

watch(
  () => [fileId, versionId],
  () => {
    firstAttemptFailed.value = false;
    artworkSettled.value = false;
    clearSettleTimer();
  },
);

const isPending = computed(
  () =>
    skeleton && showArtwork.value && !thumbnail.thumbnailLoaded.value && !firstAttemptFailed.value,
);

const coverStyle = computed(() => {
  if (isPending.value) return undefined;
  if (showArtwork.value && thumbnail.thumbnailLoaded.value && artworkSettled.value)
    return undefined;
  return { background: cover.value.background };
});

const onError = () => {
  firstAttemptFailed.value = true;
  artworkSettled.value = false;
  clearSettleTimer();
  emit("color", cover.value.base);
  thumbnail.onThumbnailError();
};

const onTransitionEnd = (event: TransitionEvent) => {
  if (event.propertyName !== "opacity" || !thumbnail.thumbnailLoaded.value) return;

  clearSettleTimer();
  artworkSettled.value = true;
};

const onLoad = () => {
  thumbnail.onThumbnailLoad();

  clearSettleTimer();
  if (reducedMotion.value === "reduce") {
    artworkSettled.value = true;
  } else {
    settleTimer = setTimeout(() => {
      settleTimer = null;
      if (thumbnail.thumbnailLoaded.value) artworkSettled.value = true;
    }, SETTLE_FALLBACK_MS);
  }

  if (image.value) emit("color", sampleCoverColor(image.value) ?? cover.value.base);
};

watch(
  [showArtwork, () => cover.value.base],
  ([artwork, base]) => {
    if (!artwork) emit("color", base);
  },
  { immediate: true },
);
</script>

<template>
  <span
    class="block overflow-hidden"
    :class="[
      floating ? 'absolute' : 'relative',
      { 'animate-pulse bg-gray-500/15 motion-reduce:animate-none': isPending },
    ]"
    :style="coverStyle"
  >
    <img
      v-if="showArtwork"
      ref="image"
      :src="thumbnail.thumbnailUrl.value"
      alt=""
      loading="lazy"
      decoding="async"
      class="absolute inset-0 size-full object-cover transition-[opacity,transform] duration-200 ease-out motion-reduce:transition-none"
      :class="{
        'opacity-0': !thumbnail.thumbnailLoaded.value,
        'group-hover:scale-105 motion-reduce:group-hover:scale-100': zoom,
      }"
      @load="onLoad"
      @error="onError"
      @transitionend.self="onTransitionEnd"
    />

    <slot />
  </span>
</template>
