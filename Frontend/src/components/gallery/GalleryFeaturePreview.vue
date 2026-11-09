<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { GalleryPhoto } from "@/api/gallery";

import { getPreviewByVersion } from "@/queries/files";

const { photo } = defineProps<{ photo: GalleryPhoto }>();

// Featured tiles are several times larger than the 320px thumbnail, so they swap in the full
// preview over it. The thumbnail stays underneath until then, and remains if there is none.
const { data: preview } = useQuery(() =>
  getPreviewByVersion({
    fileId: photo.fileId,
    mimeType: photo.mimeType,
    versionId: photo.versionId,
  }),
);

const previewUrl = computed(() => preview.value?.previewUrl ?? null);
const loaded = ref(false);

watch(previewUrl, () => {
  loaded.value = false;
});
</script>

<template>
  <img
    v-if="previewUrl"
    :src="previewUrl"
    alt=""
    draggable="false"
    class="absolute inset-0 size-full object-cover transition-opacity duration-200 ease-out"
    :class="loaded ? 'opacity-100' : 'opacity-0'"
    @load="loaded = true"
  />
</template>
