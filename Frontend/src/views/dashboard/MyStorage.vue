<template>
  <div class="flex flex-col h-full w-full flex-1">
    <!-- Header -->
    <div class="flex w-full gap-3 px-6 py-4 border-b items-center justify-between">
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg border border-dashed opacity-50">
          <UIcon name="mdi:chart-pie" class="w-4 h-4" />
        </div>
        <div>
          <h1 class="text-lg font-semibold tracking-tight">Storage</h1>
          <p class="text-xs opacity-90">Manage and inspect your usage</p>
        </div>
      </div>
      <UButton
        variant="ghost"
        size="sm"
        @click="
          () => {
            resetPreviews();
            refreshStorageData();
            refreshMyStorage();
            refreshPreviews();
          }
        "
      >
        <UIcon name="mdi:refresh" class="w-3.5 h-3.5 mr-1.5" />
        Refresh
      </UButton>
    </div>

    <!-- Content -->
    <div ref="scrollContainerRef" class="flex-1 overflow-auto" @scroll.passive="onContentScroll">
      <div class="max-w-7xl mx-auto px-6 py-8 space-y-8">
        <!-- Storage Overview Widget -->
        <StorageInfoWidget :defaultState="true" />

        <!-- Breakdown Chart -->
        <StorageBreakdownDiagram
          v-if="sizeByCategory"
          :labels="sizeByCategory.categories"
          :data="sizeByCategory.size"
          :formatted-size="sizeByCategory.formattedSize"
        />

        <!-- Loading -->
        <div v-if="myStorageIsLoading" class="flex items-center justify-center py-20 opacity-80">
          <UIcon name="mdi:loading" class="w-6 h-6 animate-spin" />
        </div>

        <!-- Error -->
        <div v-else-if="myStorageError" class="text-center py-16 opacity-90 space-y-2">
          <UIcon name="mdi:alert-circle-outline" class="w-10 h-10 mx-auto" />
          <p class="text-sm">Failed to load storage data</p>
        </div>

        <template v-else-if="myStorageData">
          <!-- Stat row: Trash + quick stats -->
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest opacity-90 font-medium">Trash</p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ formatBytes(myStorageData.trashSize) }}
                  </p>
                  <p class="text-xs opacity-90">Awaiting cleanup</p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-90 mt-0.5">
                  <UIcon name="mdi:delete-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>

            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest opacity-90 font-medium">Old Files</p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ myStorageData.oldFiles.length }}
                  </p>
                  <p class="text-xs opacity-90">Not accessed recently</p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
                  <UIcon name="mdi:clock-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>

            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest opacity-90 font-medium">File Types</p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ sizeByCategory?.categories.length ?? "—" }}
                  </p>
                  <p class="text-xs opacity-90">Distinct categories</p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
                  <UIcon name="mdi:shape-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>
          </div>

          <!-- Derived artifacts -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest opacity-90 font-medium">Previews</p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ formatBytes(myStorageData.previewsSize) }}
                  </p>
                  <p class="text-xs opacity-90">
                    {{ myStorageData.previewsCount }} regenerable
                    {{ myStorageData.previewsCount === 1 ? "artifact" : "artifacts" }}
                  </p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
                  <UIcon name="mdi:image-multiple-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>

            <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-5' }">
              <div class="flex items-start justify-between">
                <div class="space-y-1">
                  <p class="text-xs uppercase tracking-widest opacity-90 font-medium">Transcoded</p>
                  <p class="text-2xl font-semibold tabular-nums leading-none">
                    {{ formatBytes(myStorageData.transcodedSize) }}
                  </p>
                  <p class="text-xs opacity-90">
                    {{ myStorageData.representationsCount }} system-managed
                    {{
                      myStorageData.representationsCount === 1
                        ? "representation"
                        : "representations"
                    }}
                  </p>
                </div>
                <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
                  <UIcon name="mdi:film-open-outline" class="w-4 h-4" />
                </div>
              </div>
            </UCard>
          </div>

          <!-- Old Files Section -->
          <div v-if="myStorageData.oldFiles.length > 0">
            <div class="flex items-center gap-3 mb-5">
              <div class="h-px flex-1 border-t border-dashed opacity-20" />
              <div
                class="flex items-center gap-2 text-xs opacity-90 uppercase tracking-widest font-medium"
              >
                <UIcon name="mdi:clock-outline" class="w-3.5 h-3.5" />
                Old Files
                <span class="normal-case tracking-normal font-normal opacity-70"
                  >({{ myStorageData.oldFiles.length }})</span
                >
              </div>
              <div class="h-px flex-1 border-t border-dashed opacity-20" />
            </div>

            <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <div
                v-for="file in myStorageData.oldFiles"
                :key="file.id"
                class="group flex items-start gap-3 p-4 rounded-lg border border-dashed hover:border-solid hover:border-primary/40 transition-all cursor-pointer bg-black/1 dark:bg-white/1 hover:bg-black/3 dark:hover:bg-white/3"
              >
                <div
                  class="p-2 rounded-md border opacity-50 group-hover:opacity-80 transition-opacity shrink-0"
                >
                  <UIcon :name="getFileIcon(file.fileName)" class="w-4 h-4" />
                </div>
                <div class="flex-1 min-w-0 space-y-1">
                  <p class="text-sm font-medium truncate leading-snug" :title="file.fileName">
                    {{ file.fileName }}
                  </p>
                  <div class="flex items-center gap-1.5 flex-wrap">
                    <span class="text-xs opacity-90">{{ getFileTypeReadable(file.mimeType) }}</span>
                    <span
                      v-if="file.hasPreview"
                      class="inline-flex items-center text-[10px] px-1.5 py-0.5 rounded border opacity-50 font-medium"
                    >
                      Preview
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Empty state for old files -->
          <div v-else class="text-center py-16 space-y-3 opacity-90">
            <UIcon name="mdi:check-circle-outline" class="w-10 h-10 mx-auto" />
            <div>
              <p class="font-medium text-sm">No old files</p>
              <p class="text-xs mt-0.5">Everything looks tidy</p>
            </div>
          </div>
        </template>

        <!-- Preview artifacts -->
        <div>
          <div class="flex items-center gap-3 mb-5">
            <div class="h-px flex-1 border-t border-dashed opacity-20" />
            <div
              class="flex items-center gap-2 text-xs opacity-90 uppercase tracking-widest font-medium"
            >
              <UIcon name="mdi:image-multiple-outline" class="w-3.5 h-3.5" />
              Preview artifacts
              <span class="normal-case tracking-normal font-normal opacity-70">
                ({{ previewsHeaderCount }})
              </span>
            </div>
            <div class="h-px flex-1 border-t border-dashed opacity-20" />
          </div>

          <div class="flex flex-wrap items-center gap-2 mb-4">
            <USelect v-model="selectedPeriodDays" :items="periodOptions" size="sm" class="w-52" />
            <UButton
              v-if="previewGroups.length > 0"
              color="error"
              variant="outline"
              size="sm"
              icon="i-lucide-trash-2"
              label="Delete shown"
              @click="deleteTarget = { kind: 'bulk' }"
            />
            <p class="text-xs opacity-70 w-full sm:w-auto sm:ml-auto">
              Previews regenerate from their files when needed.
            </p>
          </div>

          <div
            v-if="isInitialPreviewsLoading"
            class="flex items-center justify-center py-20 opacity-80"
          >
            <UIcon name="mdi:loading" class="w-6 h-6 animate-spin" />
          </div>

          <div v-else-if="myPreviewsError" class="text-center py-16 opacity-90 space-y-2">
            <UIcon name="mdi:alert-circle-outline" class="w-10 h-10 mx-auto" />
            <p class="text-sm">Failed to load preview artifacts</p>
          </div>

          <div
            v-else-if="previewGroups.length === 0"
            class="text-center py-16 space-y-3 opacity-90"
          >
            <UIcon name="mdi:check-circle-outline" class="w-10 h-10 mx-auto" />
            <div>
              <p class="font-medium text-sm">No preview artifacts</p>
              <p class="text-xs mt-0.5">Nothing regenerable to clean up</p>
            </div>
          </div>

          <div v-else class="space-y-4">
            <div
              v-for="group in previewGroups"
              :key="group.fileId"
              class="rounded-lg border border-dashed p-4 space-y-3 bg-black/1 dark:bg-white/1"
            >
              <div class="flex items-center gap-3 min-w-0">
                <div class="p-2 rounded-md border opacity-50 shrink-0">
                  <UIcon :name="getFileIcon(group.fileName)" class="w-4 h-4" />
                </div>
                <div class="flex-1 min-w-0">
                  <p class="text-sm font-medium truncate leading-snug" :title="group.fileName">
                    {{ group.fileName }}
                  </p>
                  <p class="text-xs opacity-70 tabular-nums">
                    {{ group.items.length }}
                    {{ group.items.length === 1 ? "artifact" : "artifacts" }} ·
                    {{ formatBytes(group.totalBytes) }}
                  </p>
                </div>
                <UButton
                  color="error"
                  variant="ghost"
                  size="xs"
                  icon="i-lucide-trash-2"
                  label="Delete"
                  @click="deleteTarget = { group, kind: 'file' }"
                />
              </div>
              <div class="divide-y divide-dashed opacity-divide">
                <div
                  v-for="preview in group.items"
                  :key="preview.previewId"
                  class="flex items-center gap-3 py-2"
                >
                  <UBadge color="neutral" variant="subtle" size="sm">
                    {{ kindLabel(preview.kind) }}
                  </UBadge>
                  <span class="text-xs tabular-nums opacity-70">
                    {{ formatBytes(preview.sizeBytes) }}
                  </span>
                  <span class="text-xs opacity-50" :title="preview.createdAt">
                    {{ formatRelativeShort(preview.createdAt) }} ago
                  </span>
                  <UButton
                    color="error"
                    variant="ghost"
                    size="xs"
                    icon="i-lucide-trash-2"
                    :aria-label="`Delete ${kindLabel(preview.kind).toLowerCase()} preview`"
                    class="ml-auto"
                    @click="deleteTarget = { kind: 'single', preview }"
                  />
                </div>
              </div>
            </div>
          </div>

          <div
            v-if="isLoadingMorePreviews"
            class="flex items-center justify-center gap-2 py-6 opacity-80"
          >
            <!-- <UIcon name="mdi:loading" class="w-5 h-5 animate-spin" /> -->
            <BlockSpinner />
            <p class="text-xs">Loading more…</p>
          </div>

          <p
            v-else-if="!hasMorePreviews && previewGroups.length > 0"
            class="text-center text-xs opacity-50 py-6"
          >
            All caught up
          </p>
        </div>

        <ConfirmModal
          :open="deleteTarget.kind !== 'none'"
          :title="deleteDialogTitle"
          :description="deleteDialogDescription"
          confirm-label="Delete"
          confirm-icon="i-lucide-trash-2"
          confirm-color="error"
          :danger-mode="true"
          :loading="isDeleting"
          @close="onDeleteDialogClose"
        />

        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          leave-active-class="transition-all duration-150 ease-in"
          enter-from-class="opacity-0 translate-y-2"
          leave-to-class="opacity-0 translate-y-2"
        >
          <UButton
            v-if="showBackToTop"
            class="fixed bottom-6 right-6 z-20 rounded-full shadow-lg"
            color="neutral"
            size="lg"
            icon="i-lucide-arrow-up"
            aria-label="Back to top"
            @click="scrollToTop"
          />
        </Transition>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { UserPreview } from "@/api/status";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import StorageBreakdownDiagram from "@/components/dashboard/metrics/StorageBreakdownChart.vue";
import StorageInfoWidget from "@/components/dashboard/metrics/StorageInfoWidget.vue";
import { PreviewKind } from "@/enums/PreviewKind";
import { useDeletePreview, useDeletePreviewsByFile } from "@/mutations/storage";
import { type MyPreviewsFilters, myPreviews, myStorage, storageInfo } from "@/queries/status";
import { formatRelativeShort } from "@/utils/date-formatters";
import { getFileIcon } from "@/utils/icon.utils";
import { getFileTypeReadable, groupMimeSizeRecord } from "@/utils/mimetype.utils";
import { formatBytes } from "@/utils/size.utils";

const toast = useToast();

const PREVIEW_PAGE_SIZE = 20;
const SCROLL_LOAD_THRESHOLD_PX = 400;
const BACK_TO_TOP_THRESHOLD_PX = 600;

const { refresh: refreshStorageData } = useQuery(storageInfo);
const {
  data: myStorageData,
  isLoading: myStorageIsLoading,
  refresh: refreshMyStorage,
  error: myStorageError,
} = useQuery(myStorage);

const sizeByCategory = computed(() => {
  if (myStorageData.value) {
    return groupMimeSizeRecord(myStorageData.value.sizeByType);
  }
  return null;
});

const periodOptions = [
  { label: "All time", value: 0 },
  { label: "Older than 30 days", value: 30 },
  { label: "Older than 90 days", value: 90 },
  { label: "Older than 1 year", value: 365 },
];

const selectedPeriodDays = ref(0);

const periodCutoff = computed(() => {
  if (selectedPeriodDays.value <= 0) return undefined;
  return new Date(Date.now() - selectedPeriodDays.value * 86400_000).toISOString();
});

const previewPage = ref(1);

const previewFilters = computed<MyPreviewsFilters>(() => {
  const filters: MyPreviewsFilters = { page: previewPage.value, pageSize: PREVIEW_PAGE_SIZE };
  if (periodCutoff.value !== undefined) filters.createdBefore = periodCutoff.value;
  return filters;
});

const {
  data: myPreviewsPage,
  isLoading: myPreviewsLoading,
  error: myPreviewsError,
  refresh: refreshPreviews,
} = useQuery(myPreviews, () => previewFilters.value);

const accumulatedPreviews = ref<UserPreview[]>([]);

watch(
  () => myPreviewsPage.value,
  (page) => {
    if (!page) return;
    if (previewPage.value === 1) {
      accumulatedPreviews.value = [...page.items];
    } else {
      const knownIds = new Set(accumulatedPreviews.value.map((p) => p.previewId));
      accumulatedPreviews.value = [
        ...accumulatedPreviews.value,
        ...page.items.filter((p) => !knownIds.has(p.previewId)),
      ];
    }
  },
  { immediate: true },
);

watch(selectedPeriodDays, () => resetPreviews());

const resetPreviews = () => {
  accumulatedPreviews.value = [];
  previewPage.value = 1;
};

const totalPreviewPages = computed(() => myPreviewsPage.value?.totalPages ?? 1);
const totalPreviewCount = computed(
  () => myPreviewsPage.value?.totalCount ?? accumulatedPreviews.value.length,
);
const hasMorePreviews = computed(() => previewPage.value < totalPreviewPages.value);
const isInitialPreviewsLoading = computed(
  () => myPreviewsLoading.value && accumulatedPreviews.value.length === 0,
);
const isLoadingMorePreviews = computed(
  () => myPreviewsLoading.value && accumulatedPreviews.value.length > 0,
);

const loadMorePreviews = () => {
  if (myPreviewsLoading.value || !hasMorePreviews.value) return;
  previewPage.value++;
};

const scrollContainerRef = ref<HTMLElement | null>(null);
const showBackToTop = ref(false);

const onContentScroll = (event: Event) => {
  const el = event.target as HTMLElement;
  showBackToTop.value = el.scrollTop > BACK_TO_TOP_THRESHOLD_PX;
  if (
    el.scrollTop + el.clientHeight > el.scrollHeight - SCROLL_LOAD_THRESHOLD_PX &&
    hasMorePreviews.value &&
    !myPreviewsLoading.value
  ) {
    loadMorePreviews();
  }
};

const scrollToTop = () => {
  scrollContainerRef.value?.scrollTo({ behavior: "smooth", top: 0 });
};

const { mutateAsync: deletePreview } = useDeletePreview();
const { mutateAsync: deletePreviewsByFile } = useDeletePreviewsByFile();

interface PreviewFileGroup {
  fileId: string;
  fileName: string;
  totalBytes: number;
  items: UserPreview[];
}

const previewGroups = computed<PreviewFileGroup[]>(() => {
  const groups: PreviewFileGroup[] = [];
  const byId = new Map<string, PreviewFileGroup>();
  for (const item of accumulatedPreviews.value) {
    let group = byId.get(item.fileId);
    if (!group) {
      group = { fileId: item.fileId, fileName: item.fileName, items: [], totalBytes: 0 };
      byId.set(item.fileId, group);
      groups.push(group);
    }
    group.items.push(item);
    group.totalBytes += item.sizeBytes;
  }
  return groups;
});

const shownPreviewsCount = computed(() =>
  previewGroups.value.reduce((sum, group) => sum + group.items.length, 0),
);

const shownPreviewsBytes = computed(() =>
  previewGroups.value.reduce((sum, group) => sum + group.totalBytes, 0),
);

const previewsHeaderCount = computed(() => {
  const loaded = accumulatedPreviews.value.length;
  if (hasMorePreviews.value) return `${loaded} of ${totalPreviewCount.value}`;
  return `${loaded}`;
});

const kindLabel = (kind: PreviewKind): string => {
  if (kind === PreviewKind.Thumbnail) return "Thumbnail";
  return "Preview";
};

type PreviewDeleteTarget =
  | { kind: "none" }
  | { kind: "single"; preview: UserPreview }
  | { kind: "file"; group: PreviewFileGroup }
  | { kind: "bulk" };

const deleteTarget = ref<PreviewDeleteTarget>({ kind: "none" });
const isDeleting = ref(false);

const onDeleteDialogClose = (confirmed: boolean) => {
  if (isDeleting.value) return;
  if (!confirmed) {
    deleteTarget.value = { kind: "none" };
    return;
  }
  void confirmPreviewDelete();
};

const deleteDialogTitle = computed(() => {
  const target = deleteTarget.value;
  if (target.kind === "single") return "Delete preview";
  if (target.kind === "file") return "Delete file previews";
  return "Delete shown previews";
});

const deleteDialogDescription = computed(() => {
  const target = deleteTarget.value;
  if (target.kind === "single")
    return "This removes the preview artifact. It regenerates from the file when needed.";
  if (target.kind === "file")
    return `${target.group.fileName}: ${target.group.items.length} artifacts (${formatBytes(target.group.totalBytes)}). They regenerate from the file when needed.`;
  return `${shownPreviewsCount.value} artifacts across ${previewGroups.value.length} files (${formatBytes(shownPreviewsBytes.value)}). They regenerate from their files when needed.`;
});

const confirmPreviewDelete = async () => {
  const target = deleteTarget.value;
  if (target.kind === "none" || isDeleting.value) return;
  isDeleting.value = true;
  try {
    if (target.kind === "single") {
      const result = await deletePreview(target.preview.previewId);
      toast.add({
        color: "success",
        description: `Freed ${formatBytes(result.freedBytes)}. Regenerates from the file when needed.`,
        icon: "i-lucide-check-circle",
        title: "Preview deleted",
      });
    } else if (target.kind === "file") {
      const result = await deletePreviewsByFile({
        createdBefore: periodCutoff.value,
        fileId: target.group.fileId,
      });
      toast.add({
        color: "success",
        description: `Freed ${formatBytes(result.freedBytes)}.`,
        icon: "i-lucide-check-circle",
        title: `${result.deletedCount} previews deleted`,
      });
    } else {
      let freed = 0;
      let count = 0;
      for (const group of previewGroups.value) {
        const result = await deletePreviewsByFile({
          createdBefore: periodCutoff.value,
          fileId: group.fileId,
        });
        freed += result.freedBytes;
        count += result.deletedCount;
      }
      toast.add({
        color: "success",
        description: `Freed ${formatBytes(freed)}.`,
        icon: "i-lucide-check-circle",
        title: `${count} previews deleted`,
      });
    }
    deleteTarget.value = { kind: "none" };
    resetPreviews();
  } catch {
    toast.add({
      color: "error",
      icon: "i-lucide-x-circle",
      title: "Delete failed",
    });
  } finally {
    isDeleting.value = false;
  }
};
</script>
