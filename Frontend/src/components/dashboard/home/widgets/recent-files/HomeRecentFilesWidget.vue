<script setup lang="ts">
import { useQueryCache } from "@pinia/colada";
import { computed, defineAsyncComponent, ref, watch } from "vue";

import type { FileResult } from "@/api/file";
import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import FileUploadModal from "@/components/dashboard/file-system/Modals/FileUploadModal.vue";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { useLazyModal } from "@/composables/useLazyOverlay";
import { HomeRecentFilesSource, HomeRecentFilesStyle } from "@/enums/home-widget";
import { HOME_WIDGET_QUERY_KEYS, homeRecentFiles } from "@/queries/home-widgets";
import { STATUS_QUERY_KEYS } from "@/queries/status";
import { useHomeStore } from "@/stores/home";
import { homeRecentFileDate } from "@/utils/home-file-widgets";

import HomeRecentFilesDisplay from "./HomeRecentFilesDisplay.vue";

const FileDetailsDrawer = defineAsyncComponent(
  () => import("@/components/dashboard/file-system/FileDetailsDrawer.vue"),
);

const { widget } = defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();

const cache = useQueryCache();

const temporarySource = ref<HomeRecentFilesSource | null>(null);

const activeFile = ref<FileResult | null>(null);

const detailsMounted = ref(false);

const source = computed(() =>
  store.isEditing
    ? widget.options.recentFilesSource
    : (temporarySource.value ?? widget.options.recentFilesSource),
);

const sourceChoices = [
  { label: "Created", value: HomeRecentFilesSource.Created },
  { label: "Updated", value: HomeRecentFilesSource.Updated },
];

watch(
  () => widget.options.recentFilesSource,
  () => {
    temporarySource.value = null;
  },
);
watch(
  () => store.ownerEpoch,
  () => {
    activeFile.value = null;
    temporarySource.value = null;
    detailsMounted.value = false;
  },
);

const query = useHomeWidgetQuery(
  () =>
    homeRecentFiles({ userId: store.ownerId ?? "", epoch: store.ownerEpoch, source: source.value }),
  (preview) => ({
    userId: store.ownerId ?? "",
    epoch: store.ownerEpoch,
    source: source.value,
    value: preview.files,
  }),
);

const data = computed(() => {
  const response = query.data.value;
  if (
    response?.userId !== store.ownerId ||
    response.epoch !== store.ownerEpoch ||
    response.source !== source.value
  )
    return null;

  return response.value.items.filter((file) => homeRecentFileDate(file, source.value) !== null);
});

const authPending = computed(() => isAuthError(query.error.value));

const upload = useLazyModal<
  { directoryId?: string; directoryName?: string; droppedFiles?: File[] },
  boolean
>(FileUploadModal, { persistOnDispose: true });

const open = (file: FileResult) => {
  if (store.isEditing) return;

  detailsMounted.value = true;
  activeFile.value = file;
};

const refreshHomeData = () =>
  Promise.allSettled([
    cache.invalidateQueries({ key: HOME_WIDGET_QUERY_KEYS.filesOwner(store.ownerId ?? "") }),
    cache.invalidateQueries({ key: STATUS_QUERY_KEYS.myStorage() }),
  ]);

const uploadFiles = async () => {
  const epoch = store.ownerEpoch;
  const instance = upload.open();
  const saved = await instance.result;
  if (saved && epoch === store.ownerEpoch && !upload.isScopeDisposed()) await refreshHomeData();
};
</script>

<template>
  <div
    class="recent-widget flex min-h-0 flex-1 flex-col gap-2"
    :class="{ 'pointer-events-none': store.isEditing }"
  >
    <div
      v-if="widget.options.recentFilesStyle === HomeRecentFilesStyle.Table"
      class="flex items-center justify-between gap-2"
    >
      <span class="feed-label text-xs text-gray-600 dark:text-gray-400">Show</span>

      <USelect
        :model-value="source"
        :items="sourceChoices"
        :disabled="store.isEditing"
        size="xs"
        aria-label="Recent files feed"
        class="min-w-0 flex-1"
        @update:model-value="temporarySource = $event"
      />
    </div>

    <HomeWidgetDataState
      :loading="authPending || (!data && !query.error.value)"
      :failed="!data && Boolean(query.error.value)"
      @retry="query.refresh()"
    >
      <div
        v-if="data && !data.length"
        class="recent-empty flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon
          name="i-heroicons-document-text"
          class="size-8 shrink-0 text-gray-500 dark:text-gray-500"
        />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No recent files</p>
        <p class="empty-description text-xs text-gray-600 dark:text-gray-400">
          {{
            source === HomeRecentFilesSource.Created
              ? "Files you upload will appear here."
              : "Files you update will appear here."
          }}
        </p>

        <UButton
          class="empty-upload"
          color="primary"
          variant="solid"
          size="sm"
          :disabled="store.isEditing"
          @click="uploadFiles"
          >Upload</UButton
        >
      </div>

      <HomeRecentFilesDisplay
        v-else-if="data"
        :files="data"
        :source="source"
        :recent-style="widget.options.recentFilesStyle"
        :disabled="store.isEditing"
        @open="open"
      />

      <RouterLink
        v-if="data?.length"
        to="/dashboard"
        class="recent-link self-start text-xs text-gray-600 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-400"
        >View all files</RouterLink
      >
    </HomeWidgetDataState>
  </div>

  <FileDetailsDrawer
    v-if="detailsMounted"
    v-model:file="activeFile"
    @file-trashed="refreshHomeData"
    @file-restored="refreshHomeData"
  />
</template>

<style scoped>
.recent-link {
  display: none;
}

@container home-widget (min-height: 240px) {
  .recent-link {
    display: block;
  }
}

@container home-widget (max-height: 200px) {
  .empty-description,
  .empty-upload {
    display: none;
  }
}

@container home-widget (max-width: 180px) {
  .feed-label {
    display: none;
  }
}
</style>
