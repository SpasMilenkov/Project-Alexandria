<script setup lang="ts">
import { computed, ref, watch } from "vue";

import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { useSettingsStore } from "@/stores/settings";
import { sampleCoverColor } from "@/utils/cover-color";
import { generatedCover } from "@/utils/generated-cover";

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

watch(
  () => [fileId, versionId],
  () => {
    firstAttemptFailed.value = false;
  },
);

const isPending = computed(
  () =>
    skeleton && showArtwork.value && !thumbnail.thumbnailLoaded.value && !firstAttemptFailed.value,
);

const onError = () => {
  firstAttemptFailed.value = true;
  emit("color", cover.value.base);
  thumbnail.onThumbnailError();
};

const onLoad = () => {
  thumbnail.onThumbnailLoad();

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
    :style="isPending ? undefined : { background: cover.background }"
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
    />

    <slot />
  </span>
</template>
