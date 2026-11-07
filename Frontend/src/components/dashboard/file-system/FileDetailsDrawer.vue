<template>
  <DetailsDrawerShell
    v-model:open="isOpen"
    :title="detail?.fileName ?? ''"
    title-label="File name"
    :description="drawerDescription"
    wide
  >
    <template #icon>
      <Icon v-if="detail" :icon="getFileIcon(detail.fileName)" class="h-6 w-6" />
    </template>

    <template v-if="detail" #meta>
      <span class="max-w-full truncate">{{ fileTypeLabel }}</span>
      <span aria-hidden="true">·</span>
      <span class="tabular-nums">{{ formatBytes(Number(detail.currentVersion.size)) }}</span>
      <span aria-hidden="true">·</span>
      <span class="tabular-nums">v{{ detail.currentVersion.versionNumber }}</span>

      <template v-if="detail.currentVersion.isEncrypted">
        <span aria-hidden="true">·</span>
        <span class="flex items-center gap-1">
          <Icon icon="mdi:lock" class="h-3.5 w-3.5" aria-hidden="true" />
          Encrypted
        </span>
      </template>
    </template>

    <template v-if="displayFile && detail">
      <FilePreview
        v-if="!detail.currentVersion.isEncrypted"
        :file-id="displayFile.fileId"
        :current-version-id="detail.currentVersion.id"
        :file-name="detail.fileName"
        :mime-type="detail.currentVersion.mimeType"
        :is-encrypted="detail.currentVersion.isEncrypted"
        :cover-updated-at="detail.currentVersion.coverUpdatedAt"
      />

      <DetailsSection title="Tags">
        <template #actions>
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

          <UPopover v-model:open="showTagSearch" :content="{ side: 'bottom', align: 'end' }">
            <UButton
              label="Add tag"
              icon="i-mdi-plus"
              size="xs"
              variant="outline"
              color="neutral"
            />

            <template #content>
              <div class="w-56 p-2">
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
        </template>

        <div v-if="fileTagsPending" class="flex h-8 items-center text-gray-500">
          <BlockSpinner :size="16" />
        </div>

        <div
          v-else-if="displayTags.length > 0"
          class="flex flex-nowrap items-center gap-1.5 overflow-x-auto pb-1 md:flex-wrap md:overflow-visible md:pb-0"
        >
          <TagBadge
            v-for="tag in displayTags"
            :key="tag.id"
            :tag="tag"
            :file-id="displayFile.fileId"
            class="shrink-0"
            @remove-tag="refreshOnRemove"
          />
        </div>

        <p v-else class="text-sm text-gray-600 dark:text-gray-400">No tags yet.</p>
      </DetailsSection>

      <DetailsSection title="Details">
        <div class="grid grid-cols-2 gap-2">
          <DetailsTile label="Size">
            <span class="tabular-nums">{{ formatBytes(Number(detail.currentVersion.size)) }}</span>
          </DetailsTile>

          <DetailsTile label="Type">
            <span class="block wrap-break-word">{{ fileTypeLabel }}</span>
          </DetailsTile>

          <DetailsTile
            label="Created"
            :hint="formatDate(detail.createdAt)"
            :class="{ 'col-span-2': !detail.updatedAt }"
          >
            {{ formatCalendarDate(detail.createdAt) }}
          </DetailsTile>

          <DetailsTile
            v-if="detail.updatedAt"
            label="Modified"
            :hint="formatDate(detail.updatedAt)"
          >
            {{ formatCalendarDate(detail.updatedAt) }}
          </DetailsTile>

          <DetailsTile label="File ID" :copy-value="displayFile.fileId" class="col-span-2">
            <span class="block truncate font-mono text-xs font-normal">
              {{ displayFile.fileId }}
            </span>
          </DetailsTile>
        </div>
      </DetailsSection>

      <DetailsSection title="Owner">
        <DetailsOwner :name="detail.owner.name" :email="detail.owner.email" />
      </DetailsSection>

      <FileVersionHistory
        :file-id="displayFile.fileId"
        :file-name="detail.fileName"
        :current-version-id="detail.currentVersion.id"
        :current-version-number="detail.currentVersion.versionNumber"
        @versions-changed="refreshDetail"
      />
    </template>
  </DetailsDrawerShell>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { TagDto } from "@/api/tag";
import type { SearchTagsSchema } from "@/schemas/tag";

import { type FileResult } from "@/api/file";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import DetailsDrawerShell from "@/components/common/details/DetailsDrawerShell.vue";
import DetailsOwner from "@/components/common/details/DetailsOwner.vue";
import DetailsSection from "@/components/common/details/DetailsSection.vue";
import DetailsTile from "@/components/common/details/DetailsTile.vue";
import { useAppToast } from "@/composables/useAppToast";
import { quoted } from "@/features/toast/format";
import { autoTagFile } from "@/mutations/files";
import { addTagToFile, removeTagFromFile } from "@/mutations/tags";
import { getFile } from "@/queries/files";
import { getTagsForFile, searchTag } from "@/queries/tags";
import { formatCalendarDate, formatDate } from "@/utils/date-formatters";
import { getFileIcon, getIconByValue } from "@/utils/files/icons";
import { getFileTypeReadable, isAutoTagSupportedFileType } from "@/utils/files/mimetype";
import { formatBytes } from "@/utils/size";

import FilePreview from "./FilePreview.vue";
import FileVersionHistory from "./FileVersionHistory.vue";

const appToast = useAppToast("tags");

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

// File detail query, keyed reactively off the active file so switching
// files while the drawer stays mounted refetches correctly.
const { data: fileDetail, refresh: refreshDetail } = useQuery(() => ({
  ...getFile(displayFile.value?.fileId ?? ""),
  enabled: isOpen.value,
}));
const detail = computed(() => fileDetail.value ?? displayFile.value);

const drawerDescription = computed(() => {
  if (!detail.value) return undefined;
  return `Created ${formatDate(detail.value.createdAt)}`;
});

const fileTypeLabel = computed(() => {
  if (!detail.value) return "";
  return getFileTypeReadable(detail.value.currentVersion.mimeType, detail.value.fileName);
});

const { mutateAsync: addTagMutate } = addTagToFile();
const { mutateAsync: removeTagMutateAsync } = removeTagFromFile();
const { mutateAsync: autoTagMutate, isLoading: isAutoTagging } = autoTagFile();

// Supports tag-derivation (narrower than "is music"), so it only gates Auto-tag.
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

const fileLabel = computed(() => {
  const name = detail.value?.fileName;

  return name ? quoted(name) : "this file";
});

const handleAutoTag = async () => {
  if (isAutoTagging.value || !displayFile.value) return;
  try {
    const result = await autoTagMutate(displayFile.value.fileId);
    if (result.queued) {
      appToast.success(
        `Auto-tagging ${fileLabel.value}`,
        "Tags appear here once the background analysis finishes.",
      );
    } else {
      appToast.info(`${fileLabel.value} is already auto-tagged`, result.message);
    }
  } catch (error) {
    const message = extractAutoTagError(error);
    if (message.toLowerCase().includes("disabled")) {
      appToast.info("Auto-tagging is turned off", message);
    } else {
      appToast.error(`Couldn't auto-tag ${fileLabel.value}`, message, { retry: handleAutoTag });
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

const {
  data: fileTags,
  isPending: fileTagsQueryPending,
  refresh: refreshFileTags,
} = useQuery(() => ({
  ...getTagsForFile(displayFile.value?.fileId ?? ""),
  enabled: isOpen.value,
}));

// The list falls back to tags already on the file, so the spinner only shows with nothing to draw.
const fileTagsPending = computed(
  () => fileTagsQueryPending.value && !fileTags.value && !displayFile.value?.tags?.length,
);

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
  } catch (error) {
    selectedTagId.value = undefined;
    appToast.error(`Couldn't add the tag to ${fileLabel.value}`, error);
  }
};

watch(selectedTagId, (tagId) => {
  if (tagId) void handleTagAdd(tagId);
});

const refreshOnRemove = async (id: string) => {
  if (!displayFile.value) return;
  const fileId = displayFile.value.fileId;
  const previousTags = displayFile.value.tags;
  displayFile.value = {
    ...displayFile.value,
    tags: previousTags.filter((tag) => tag.id !== id),
  };
  const removedName = previousTags.find((tag) => tag.id === id)?.name;
  const tagLabel = removedName ? quoted(removedName) : "the tag";

  try {
    await removeTagMutateAsync({ fileId, tagId: id });
    refreshFileTags();
    refreshFileTag();
    appToast.success(`Removed ${tagLabel}`, {
      actions: [
        {
          icon: "i-lucide-undo-2",
          label: "Undo",
          onClick: () => {
            void restoreRemovedTag(fileId, id, tagLabel);
          },
        },
      ],
    });
  } catch (error) {
    if (displayFile.value?.fileId === fileId) {
      displayFile.value = { ...displayFile.value, tags: previousTags };
    }
    appToast.error(`Couldn't remove ${tagLabel}`, error);
  }
};

const restoreRemovedTag = async (fileId: string, tagId: string, tagLabel: string) => {
  try {
    await addTagMutate({ fileId, data: { tagIds: [tagId] } });
    refreshFileTags();
    refreshFileTag();
  } catch (error) {
    appToast.error(`Couldn't restore ${tagLabel}`, error);
  }
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
