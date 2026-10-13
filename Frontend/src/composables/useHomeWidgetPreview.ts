import { type DefineQueryOptions, useQuery } from "@pinia/colada";
import { type InjectionKey, computed, inject, provide, ref } from "vue";

import type { HomeWidgetPreview } from "@/types/home-widget-preview";

import { type FileThumbnailSource, useFileThumbnail } from "@/composables/useFileThumbnail";
import { isThumbnailSupportedMimeType } from "@/utils/mimetype.utils";

const previewKey: InjectionKey<HomeWidgetPreview> = Symbol("home-widget-preview");

export const provideHomeWidgetPreview = (preview: HomeWidgetPreview) =>
  provide(previewKey, preview);

export const useHomeWidgetPreview = () => inject(previewKey, null);

export const useHomeWidgetQuery = <TData>(
  options: () => DefineQueryOptions<TData>,
  sample: (preview: HomeWidgetPreview) => NoInfer<TData>,
) => {
  const preview = useHomeWidgetPreview();

  if (!preview) return useQuery(options);

  // Demo data stays outside the query cache, including its fetch and retry lifecycle.
  const data = computed(() => sample(preview));

  return { data, error: ref<Error | null>(null), refresh: () => Promise.resolve() };
};

export const useHomeWidgetThumbnail = (source: () => FileThumbnailSource) => {
  const preview = useHomeWidgetPreview();

  if (!preview) return useFileThumbnail(source);

  const thumbnailLoaded = ref(true);
  const thumbnailErrored = ref(false);

  return {
    canHaveThumbnail: computed(() => isThumbnailSupportedMimeType(source().mimeType)),
    thumbnailUrl: computed(() => preview.artwork),
    thumbnailLoaded,
    thumbnailErrored,
    onThumbnailLoad: () => {
      thumbnailLoaded.value = true;
    },
    onThumbnailError: () => {
      thumbnailErrored.value = true;
    },
  };
};
