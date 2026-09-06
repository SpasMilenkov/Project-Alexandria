import { computed, ref, watch } from "vue";

import { fileApi } from "@/api/file";

export interface FileThumbnailSource {
  fileId: string;
  versionId: string;
}

// Version-scoped thumbnail URL: permanently cacheable, browser + nginx do the
// work, no fetch layer. Non-image files 404 and fall through to the icon.
export const useFileThumbnail = (source: () => FileThumbnailSource) => {
  const thumbnailUrl = computed(() =>
    fileApi.getThumbnailUrlForVersion(source().fileId, source().versionId),
  );

  const thumbnailLoaded = ref(false);
  const thumbnailErrored = ref(false);

  watch(source, () => {
    thumbnailLoaded.value = false;
    thumbnailErrored.value = false;
  });

  const onThumbnailLoad = () => {
    thumbnailLoaded.value = true;
  };

  const onThumbnailError = () => {
    thumbnailErrored.value = true;
  };

  return {
    thumbnailErrored,
    thumbnailLoaded,
    thumbnailUrl,
    onThumbnailError,
    onThumbnailLoad,
  };
};
