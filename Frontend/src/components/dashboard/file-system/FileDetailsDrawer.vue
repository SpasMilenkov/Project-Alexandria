<template>
  <UDrawer
    v-model:open="isOpen"
    :title="detail?.fileName"
    :description="detail ? 'Created ' + formatDate(detail.createdAt) : undefined"
    :direction="isMobile ? 'bottom' : 'right'"
    :ui="drawerUi"
    :handle-only="!isMobile"
  >
    <template #body>
      <div v-if="displayFile && detail" class="flex flex-col gap-6 p-1">
        <!-- File Header Section -->
        <div
          class="flex items-center bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
          :class="isMobile ? 'gap-3 p-3' : 'gap-4 p-6'"
        >
          <div
            class="bg-neutral-100 dark:bg-neutral-800/50 rounded-lg shadow-sm shrink-0"
            :class="isMobile ? 'p-2' : 'p-4'"
          >
            <Icon
              :icon="getFileIcon(detail.fileName)"
              class="text-primary"
              :class="isMobile ? 'w-10 h-10' : 'w-16 h-16'"
            />
          </div>

          <div class="flex-1 min-w-0">
            <div
              class="flex items-center gap-1 group mb-1 min-w-0"
              :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
              @click="isMobile && copyWithFeedback(detail.fileName, 'File name')"
            >
              <h3 class="font-semibold truncate" :class="isMobile ? 'text-base' : 'text-lg'">
                {{ detail.fileName }}
              </h3>
              <UButton
                icon="i-mdi-content-copy"
                size="xs"
                variant="ghost"
                color="neutral"
                :class="
                  isMobile
                    ? 'shrink-0 opacity-50'
                    : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity'
                "
                aria-label="Copy file name"
                @click.stop="copyWithFeedback(detail.fileName, 'File name')"
              />
            </div>

            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 w-full">
              <Icon icon="mdi-file" class="w-4 h-4 shrink-0" />
              <span
                class="text-ellipsis overflow-hidden"
                :class="isMobile ? 'line-clamp-1' : 'max-w-46 max-h-16 wrap-break-word'"
              >
                {{ getFileTypeReadable(detail.currentVersion.mimeType, detail.fileName) }}
              </span>
            </div>
          </div>
        </div>

        <!-- Tags Section -->
        <div class="flex flex-col gap-3">
          <div class="flex items-center justify-between gap-2">
            <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">Tags</h4>
            <UButton
              v-if="isAudioFile"
              icon="i-mdi-tag-outline"
              label="Auto-tag"
              size="xs"
              variant="outline"
              color="neutral"
              :loading="isAutoTagging"
              :disabled="isAutoTagging"
              @click="handleAutoTag"
            />
          </div>

          <USkeleton v-if="fileTagsLoading" class="h-8 w-48 rounded-full" />

          <div
            v-else
            class="flex items-center gap-1.5"
            :class="isMobile ? 'flex-nowrap overflow-x-auto pb-1' : 'flex-wrap'"
          >
            <TagBadge
              v-for="tag in displayTags"
              :key="tag.id"
              :tag="tag"
              :file-id="displayFile.fileId"
              class="shrink-0"
              @remove-tag="refreshOnRemove"
            />

            <span
              v-if="!displayTags?.length && !showTagSearch"
              class="text-xs text-gray-400 dark:text-gray-500 italic mr-1 shrink-0"
            >
              No tags
            </span>

            <UPopover v-model:open="showTagSearch" :content="{ side: 'bottom', align: 'start' }">
              <UButton
                label="Add tag"
                icon="i-mdi-plus"
                size="xs"
                variant="outline"
                color="neutral"
                class="rounded-full shrink-0"
              />

              <template #content>
                <div class="p-2 w-56">
                  <USelectMenu
                    v-model="selectedTagId"
                    :loading="tagsLoading"
                    :items="tagOptions"
                    value-key="value"
                    value-attribute="value"
                    v-model:search-term="searchQuery"
                    placeholder="Search tags…"
                    autofocus
                    class="w-full"
                  />
                </div>
              </template>
            </UPopover>
          </div>
        </div>

        <!-- Metadata Section -->
        <div class="flex flex-col gap-3">
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">Metadata</h4>
          <div class="grid gap-3" :class="isMobile ? 'grid-cols-1' : 'grid-cols-2'">
            <UFormField label="Title" name="metadata-title">
              <UInput v-model="metadataForm.title" class="w-full" placeholder="Unchanged" />
            </UFormField>
            <UFormField label="Artist" name="metadata-artist">
              <UInput v-model="metadataForm.artist" class="w-full" placeholder="Unchanged" />
            </UFormField>
            <UFormField label="Album" name="metadata-album">
              <UInput v-model="metadataForm.album" class="w-full" placeholder="Unchanged" />
            </UFormField>
            <UFormField label="Year" name="metadata-year">
              <UInput v-model="metadataForm.year" class="w-full" placeholder="Unchanged" />
            </UFormField>
          </div>
          <p class="text-xs text-gray-500 dark:text-gray-500 m-0">
            Blank fields keep their current values.
          </p>
          <div class="flex justify-end">
            <UButton
              label="Save metadata"
              size="xs"
              variant="outline"
              color="neutral"
              :loading="isSavingMetadata"
              :disabled="!isMetadataDirty || isSavingMetadata"
              @click="handleMetadataSave"
            />
          </div>
        </div>

        <!-- File Preview Section -->
        <FilePreview
          v-if="!detail.currentVersion.isEncrypted"
          :file-id="displayFile.fileId"
          :current-version-id="detail.currentVersion.id"
          :file-name="detail.fileName"
          :mime-type="detail.currentVersion.mimeType"
        />

        <!-- File Details Grid -->
        <div>
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">Details</h4>
          <div class="grid gap-4" :class="isMobile ? 'grid-cols-1' : 'grid-cols-2'">
            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <UIcon name="i-heroicons-scale" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Size</div>
                <div class="font-medium text-sm">
                  {{ formatBytes(Number(detail.currentVersion.size)) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <UIcon name="i-heroicons-archive-box" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Version</div>
                <div class="font-medium text-sm">v{{ detail.currentVersion.versionNumber }}</div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
              :class="[isMobile ? 'cursor-pointer active:opacity-60' : 'col-span-2']"
              @click="isMobile && copyWithFeedback(displayFile.fileId, 'File ID')"
            >
              <Icon icon="mdi-identifier" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div class="min-w-0 flex-1">
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">File ID</div>
                <div class="flex items-center gap-1 group min-w-0">
                  <span class="font-mono text-sm truncate">{{ displayFile.fileId }}</span>
                  <UButton
                    icon="i-mdi-content-copy"
                    size="xs"
                    variant="ghost"
                    color="neutral"
                    :class="
                      isMobile
                        ? 'shrink-0 opacity-50 ml-auto'
                        : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity'
                    "
                    aria-label="Copy file ID"
                    @click.stop="copyWithFeedback(displayFile.fileId, 'File ID')"
                  />
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Owner Section -->
        <UCard class="frosted-glass glass-surface" :ui="isMobile ? { body: 'p-3' } : {}">
          <template #header>
            <div class="flex items-center gap-2" :class="isMobile ? 'p-3 pb-0' : ''">
              <Icon icon="mdi-account" class="w-5 h-5 text-primary" />
              <span class="font-semibold text-sm">Owner</span>
            </div>
          </template>
          <div class="flex items-center gap-3">
            <UAvatar :alt="detail.owner.name" :size="isMobile ? 'md' : 'lg'" />
            <div class="min-w-0 flex-1">
              <div class="font-medium text-sm">{{ detail.owner.name }}</div>
              <div
                class="text-sm flex items-center gap-1.5 mt-0.5 text-gray-600 dark:text-gray-400 group"
                :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
                @click="isMobile && copyWithFeedback(detail.owner.email, 'Email')"
              >
                <Icon icon="mdi-email" class="w-4 h-4 text-primary shrink-0" />
                <span class="truncate">{{ detail.owner.email }}</span>
                <UButton
                  icon="i-mdi-content-copy"
                  size="xs"
                  variant="ghost"
                  color="neutral"
                  :class="
                    isMobile
                      ? 'shrink-0 opacity-50 ml-auto'
                      : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity ml-auto'
                  "
                  aria-label="Copy owner email"
                  @click.stop="copyWithFeedback(detail.owner.email, 'Email')"
                />
              </div>
            </div>
          </div>
        </UCard>

        <!-- Versions Section -->
        <FileVersionHistory
          :file-id="displayFile.fileId"
          :file-name="detail.fileName"
          :current-version-id="detail.currentVersion.id"
          :current-version-number="detail.currentVersion.versionNumber"
          @versions-changed="refreshDetail"
        />
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { breakpointsTailwind, useBreakpoints, useClipboard } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import type { TagDto } from "@/api/tag";
import type { SearchTagsSchema } from "@/schemas/tag";

import { type FileResult } from "@/api/file";
import { useAppToast } from "@/composables/useAppToast";
import { autoTagFile, updateFileMetadata } from "@/mutations/files";
import { addTagToFile, removeTagFromFile } from "@/mutations/tags";
import { getFile } from "@/queries/files";
import { getTagsForFile, searchTag } from "@/queries/tags";
import { metadataYearSchema } from "@/schemas/file";
import { formatDate } from "@/utils/date-formatters";
import { getFileIcon, getIconByValue } from "@/utils/icon.utils";
import { getFileTypeReadable, isAutoTagSupportedFileType } from "@/utils/mimetype.utils";
import { glassDrawerContent } from "@/utils/modalUi";
import { formatBytes } from "@/utils/size.utils";

import FilePreview from "./FilePreview.vue";
import FileVersionHistory from "./FileVersionHistory.vue";

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

const drawerUi = computed(() => {
  if (isMobile.value) {
    return { content: glassDrawerContent, container: "h-[85vh] rounded-t-2xl" };
  }
  return { content: glassDrawerContent, container: "md:max-w-[40rem] lg:min-w-[40rem]" };
});

const toast = useToast();
const { copy } = useClipboard();
const appToast = useAppToast();

const props = defineProps<{
  file: FileResult | null;
}>();

const emit = defineEmits<{
  "update:file": [file: FileResult | null];
  "file-trashed": [fileId: string];
  "file-restored": [];
}>();

// Keeps the last file rendered during the close transition, so the drawer
// doesn't blank out mid-animation when the parent nulls the active file.
const displayFile = ref<FileResult | null>(null);
watch(
  () => props.file,
  (f) => {
    if (f) displayFile.value = f;
  },
  { immediate: true },
);

const isOpen = computed({
  get: () => props.file !== null,
  set: (val: boolean) => {
    if (!val) emit("update:file", null);
  },
});

const copyWithFeedback = async (value: string, label: string) => {
  await copy(value);
  toast.add({
    color: "success",
    duration: 2000,
    icon: "i-mdi-check-circle",
    title: `${label} copied`,
  });
};

// File detail query, keyed reactively off the active file so switching
// files while the drawer stays mounted refetches correctly.
const { data: fileDetail, refresh: refreshDetail } = useQuery(() => ({
  ...getFile(displayFile.value?.fileId ?? ""),
  enabled: isOpen.value,
}));
const detail = computed(() => fileDetail.value ?? displayFile.value);

const { mutateAsync: addTagMutate } = addTagToFile();
const { mutateAsync: removeTagMutateAsync } = removeTagFromFile();
const { mutateAsync: autoTagMutate, isLoading: isAutoTagging } = autoTagFile();
const { mutateAsync: updateMetadataMutate, isLoading: isSavingMetadata } = updateFileMetadata();

const blankMetadataForm = () => ({ title: "", artist: "", album: "", year: "" });
const metadataForm = ref(blankMetadataForm());

const isMetadataDirty = computed(
  () =>
    metadataForm.value.title.trim() !== "" ||
    metadataForm.value.artist.trim() !== "" ||
    metadataForm.value.album.trim() !== "" ||
    metadataForm.value.year.trim() !== "",
);

const blankToUndefined = (value: string) => {
  const trimmed = value.trim();
  return trimmed ? trimmed : undefined;
};

const handleMetadataSave = async () => {
  if (!displayFile.value || !isMetadataDirty.value) return;
  const year = blankToUndefined(metadataForm.value.year);
  if (year !== undefined && !metadataYearSchema.safeParse(year).success) {
    appToast.error("Invalid year", "Year must be a 4-digit year (e.g. 1999).");
    return;
  }
  try {
    await updateMetadataMutate({
      id: displayFile.value.fileId,
      title: blankToUndefined(metadataForm.value.title),
      artist: blankToUndefined(metadataForm.value.artist),
      album: blankToUndefined(metadataForm.value.album),
      year,
    });
    metadataForm.value = blankMetadataForm();
    refreshDetail();
    appToast.success("Metadata saved");
  } catch {
    appToast.error("Failed to save metadata");
  }
};

watch(
  () => displayFile.value?.fileId,
  () => {
    metadataForm.value = blankMetadataForm();
  },
);

const isAudioFile = computed(() =>
  detail.value ? isAutoTagSupportedFileType(detail.value.currentVersion.mimeType) : false,
);

const extractAutoTagError = (err: any): string => {
  const data = err?.response?.data;
  if (data?.errors && typeof data.errors === "object") {
    const parts = Object.values(data.errors as Record<string, string[]>)
      .flat()
      .filter(Boolean);
    if (parts.length > 0) return parts.join(" · ");
  }
  return data?.message ?? data?.error ?? err?.message ?? "Unknown error";
};

const handleAutoTag = async () => {
  if (isAutoTagging.value || !displayFile.value) return;
  try {
    const result = await autoTagMutate(displayFile.value.fileId);
    if (result.queued) {
      appToast.success("Auto-tag queued", "Tags will be derived in the background.");
    } else {
      appToast.info("Already tagged", result.message);
    }
  } catch (error) {
    const message = extractAutoTagError(error);
    if (message.toLowerCase().includes("disabled")) {
      appToast.info("Auto-tagging disabled", message);
    } else {
      appToast.error("Auto-tag failed", message);
    }
  }
};

const currentPage = ref(1);
const pageSize = ref(25);
const searchQuery = ref("");
const selectedTagId = ref<string | undefined>(undefined);

const searchFilters = computed<SearchTagsSchema>(() => ({
  excludeOnFile: displayFile.value?.fileId ?? "",
  nameContains: searchQuery.value || undefined,
  ownerScope: "all",
  page: currentPage.value,
  pageSize: pageSize.value,
}));

const {
  data: tagsData,
  isLoading: tagsLoading,
  refresh: refreshFileTag,
} = useQuery(() => ({ ...searchTag(searchFilters.value), enabled: isOpen.value }));

const tagOptions = computed(() =>
  (tagsData.value?.items ?? []).map((t) => ({
    label: t.name,
    value: t.id,
    icon: getIconByValue(t.icon),
  })),
);

const { data: fileTags, isLoading: fileTagsLoading } = useQuery(() => ({
  ...getTagsForFile(displayFile.value?.fileId ?? ""),
  enabled: isOpen.value,
}));

const displayTags = computed((): TagDto[] => {
  if (fileTags.value?.tags) return fileTags.value.tags;
  if (isOpen.value && fileDetail.value?.tags) return fileDetail.value.tags;
  return displayFile.value?.tags ?? [];
});

const showTagSearch = ref(false);

const handleTagAdd = async (tagId: string) => {
  if (!tagId || !displayFile.value) return;
  try {
    await addTagMutate({
      fileId: displayFile.value.fileId,
      data: { tagIds: [tagId] },
    });
    selectedTagId.value = undefined;
    searchQuery.value = "";
    showTagSearch.value = false;
  } catch {
    selectedTagId.value = undefined;
  }
};

watch(selectedTagId, (tagId) => {
  if (tagId) void handleTagAdd(tagId);
});

const refreshOnRemove = async (id: string) => {
  if (!displayFile.value) return;
  await removeTagMutateAsync({ fileId: displayFile.value.fileId, tagId: id });
  refreshFileTag();
};

watch(
  () => fileDetail.value?.deletedAt,
  (current, prev) => {
    if (prev === undefined || !displayFile.value) return;
    if (current) {
      const id = displayFile.value.fileId;
      isOpen.value = false;
      setTimeout(() => emit("file-trashed", id), 300);
      return;
    }
    if (prev) {
      isOpen.value = false;
      setTimeout(() => emit("file-restored"), 300);
    }
  },
);

watch(showTagSearch, (open) => {
  if (!open) {
    searchQuery.value = "";
    selectedTagId.value = undefined;
  }
});
</script>
