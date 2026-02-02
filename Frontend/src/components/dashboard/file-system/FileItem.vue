<template>
  <UDrawer
    :title="data.fileName"
    :description="'Created ' + formatDate(data.createdAt)"
    direction="right"
    v-model:open="openDrawer"
    :ui="{ container: 'md:max-w-[40rem]' }"
  >
    <!-- Grid View -->
    <UContextMenu v-if="viewMode === 'grid'" :items="contextMenuItems">
      <div class="relative group" tabindex="0">
        <button
          type="button"
          class="w-full flex flex-col items-center gap-2 p-4 rounded-lg transition-colors cursor-pointer"
          :class="[
            isSelected
              ? 'bg-primary/20 ring-2 ring-primary'
              : 'hover:bg-primary/40 dark:hover:bg-primary/35',
          ]"
          @click="handleClick"
          @dblclick="handleDoubleClick"
        >
          <Icon
            :icon="getFileIcon(data.fileName)"
            :width="iconSize"
            :height="iconSize"
            class="shrink-0"
          />
          <span class="text-sm text-center line-clamp-2 w-full wrap-break-word">
            {{ data.fileName }}
          </span>
        </button>
      </div>
    </UContextMenu>

    <!-- List View -->
    <UContextMenu v-else :items="contextMenuItems">
      <div class="relative group" tabindex="0">
        <button
          type="button"
          class="w-full flex items-center gap-3 px-4 py-2 transition-colors cursor-pointer text-left border-b last:border-b-0"
          :class="[
            isSelected
              ? 'bg-primary/20 ring-2 ring-primary'
              : 'hover:bg-primary/40 dark:hover:bg-primary/35',
          ]"
          @click="handleClick"
          @dblclick="handleDoubleClick"
        >
          <Icon
            :icon="getFileIcon(data.fileName)"
            :width="iconSize"
            :height="iconSize"
            class="shrink-0"
          />
          <span class="flex-1 truncate">{{ data.fileName }}</span>
          <span class="text-xs opacity-70 shrink-0 min-w-[60px] text-right">
            {{ formatFileSize(Number(data.currentVersion.size)) }}
          </span>
        </button>
      </div>
    </UContextMenu>

    <template #body>
      <div class="flex flex-col gap-6 p-1">
        <!-- File Header Section -->
        <div
          class="flex items-center gap-4 p-6 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
        >
          <!-- File Icon -->
          <div
            class="p-4 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg shadow-sm"
          >
            <Icon
              :icon="getFileIcon(data.fileName)"
              class="w-16 h-16 text-primary"
            />
          </div>

          <!-- File Info -->
          <div class="flex-1 min-w-0">
            <h3 class="font-semibold text-lg truncate mb-1">
              {{ data.fileName }}
            </h3>
            <div
              class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 w-full"
            >
              <Icon icon="mdi-file" class="w-4 h-4" />
              <span
                class="max-w-46 text-ellipsis max-h-16 wrap-break-word overflow-hidden"
                >{{
                  getFileTypeReadable(
                    data.currentVersion.mimeType,
                    data.fileName,
                  )
                }}</span
              >
              <UBadge
                variant="subtle"
                color="warning"
                v-if="previewLoading"
                label="Checking for available preview"
              />
              <UBadge
                :label="
                  previewUrl || archivePreview || textPreview
                    ? 'Preview available'
                    : 'Preview not available'
                "
                :color="
                  previewUrl || archivePreview || textPreview
                    ? 'success'
                    : 'error'
                "
                variant="subtle"
              />
            </div>
          </div>
        </div>

        <!-- Tags Section -->
        <div class="flex flex-col gap-2 w-full">
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
            Tags
          </h4>
          <USkeleton v-if="fileTagsLoading && openDrawer" class="h-8 w-full" />
          <div v-else-if="displayTags && displayTags.length > 0">
            <div class="flex flex-wrap gap-2">
              <TagBadge
                v-for="tag in displayTags"
                :key="tag.id"
                :tag="tag"
                :file-id="props.data.fileId"
                @remove-tag="refreshOnRemove"
              />
            </div>
          </div>
          <div v-else class="text-sm text-gray-500 dark:text-gray-400">
            No tags yet
          </div>

          <div class="flex gap-2 w-full">
            <UButton
              label="Add tag"
              @click="showTagSearch = !showTagSearch"
              variant="outline"
              size="sm"
            />
            <USelectMenu
              v-if="showTagSearch"
              :loading="tagsLoading"
              :items="
                tagsData?.items.map((t) => ({
                  label: t.name,
                  value: t.id,
                  icon: getIconByValue(t.icon),
                }))
              "
              v-model:search-term="searchQuery"
              @update:search-term="refreshFileTag()"
              placeholder="Search for tag"
              @update:model-value="
                async (tag) => {
                  await addTagMutate({
                    fileId: data.fileId,
                    data: { tagIds: [tag.value] },
                  });
                  showTagSearch = false;
                  refreshFileTag();
                }
              "
            />
          </div>
        </div>

        <!-- File Preview Section (if available) -->
        <USkeleton v-if="previewLoading" />
        <div
          v-else-if="previewUrl || archivePreview || textPreview"
          class="bg-neutral-100 dark:bg-neutral-800/50 rounded-lg p-4"
        >
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
            Preview
          </h4>

          <!-- Audio Preview -->
          <div
            v-if="
              data.currentVersion.mimeType.startsWith('audio/') && previewUrl
            "
            class="relative w-full aspect-video bg-black/5 dark:bg-black/20 rounded-lg overflow-hidden"
          >
            <img
              v-if="thumbnailUrl"
              :src="thumbnailUrl"
              alt="Audio thumbnail"
              class="w-full h-full object-contain"
            />

            <!-- Play/Pause Overlay -->
            <div
              class="absolute inset-0 flex items-center justify-center cursor-pointer transition-all duration-200"
              :class="
                isAudioPlaying
                  ? 'bg-black/30'
                  : 'bg-black/0 hover:bg-black/40 group'
              "
              @click="toggleAudio"
              @mouseenter="isAudioHovered = true"
              @mouseleave="isAudioHovered = false"
            >
              <!-- Play icon - only visible on hover when not playing -->
              <Icon
                v-if="!isAudioPlaying"
                icon="mdi-play-circle"
                class="w-16 h-16 text-white opacity-0 group-hover:opacity-100 transition-opacity duration-200"
              />

              <!-- Audio playing state -->
              <div v-else class="relative flex items-center justify-center &">
                <!-- Animated audio visualizer - shows by default when playing -->
                <Icon
                  icon="mdi-waveform"
                  class="w-16 h-16 text-white animate-pulse transition-opacity duration-200"
                  :class="isAudioHovered ? 'opacity-0' : 'opacity-100'"
                />
                <!-- Pause icon - shows on hover when playing -->
                <Icon
                  icon="mdi-pause-circle"
                  class="w-16 h-16 text-white absolute transition-opacity duration-200"
                  :class="isAudioHovered ? 'opacity-100' : 'opacity-0'"
                />
              </div>
            </div>

            <audio
              ref="audioPlayer"
              :src="previewUrl"
              @ended="isAudioPlaying = false"
              class="hidden"
            />
          </div>

          <!-- Image Preview -->
          <div
            v-else-if="
              data.currentVersion.mimeType.startsWith('image/') && previewUrl
            "
            class="relative w-full rounded-lg overflow-hidden bg-black/5 dark:bg-black/20"
          >
            <img
              :src="previewUrl"
              :alt="data.fileName"
              class="w-full h-auto max-h-96 object-contain mx-auto"
            />
          </div>

          <!-- Video Preview -->
          <div
            v-else-if="
              data.currentVersion.mimeType.startsWith('video/') && previewUrl
            "
            class="relative w-full aspect-video bg-black rounded-lg overflow-hidden"
          >
            <video
              ref="videoPlayer"
              class="w-full h-full object-contain"
              controls
            >
              <source :src="previewUrl" type="video/mp4" />
            </video>
          </div>

          <!-- PDF Preview -->
          <div
            v-else-if="
              pdfPreviewMimes.includes(data.currentVersion.mimeType) &&
              archivePreviewItems &&
              previewUrl
            "
            class="relative w-xl h-220 bg-white dark:bg-neutral-900 rounded-lg overflow-hidden"
          >
            <embed
              :src="previewUrl"
              type="application/pdf"
              class="w-full h-full"
            />
          </div>

          <!-- Archive Preview -->
          <div v-else-if="archivePreview">
            <UTree :items="archivePreviewItems" />
          </div>

          <!-- Text Preview -->
          <div
            v-else-if="textPreview"
            class="w-full p-6 bg-white dark:bg-neutral-900 rounded-lg border border-gray-200 dark:border-gray-700"
          >
            <p class="max-h-96 overflow-y-auto wrap-break-word">
              {{ textPreview }}
            </p>
          </div>
        </div>

        <!-- File Details Grid -->
        <div>
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
            Details
          </h4>
          <div class="grid grid-cols-2 gap-4">
            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <Icon icon="mdi-scale" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div>
                <div class="text-xs mb-0.5">Size</div>
                <div class="font-medium">
                  {{ formatFileSize(Number(data.currentVersion.size)) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <Icon icon="mdi-counter" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">
                  Version
                </div>
                <div class="font-medium">
                  v{{ data.currentVersion.versionNumber }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg col-span-2"
            >
              <Icon
                icon="mdi-identifier"
                class="w-10 h-10 text-gray-500 mt-0.5"
              />
              <div class="min-w-0 flex-1">
                <div class="text-xs mb-0.5">File ID</div>
                <div class="font-mono text-sm truncate">{{ data.fileId }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Owner Section -->
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <Icon icon="mdi-account" class="w-8 h-8 text-primary" />
              <span class="font-semibold">Owner</span>
            </div>
          </template>

          <div class="flex items-center gap-3">
            <UAvatar :alt="data.owner.name" size="lg" />
            <div>
              <div class="font-medium">{{ data.owner.name }}</div>
              <div class="text-sm flex items-center gap-1.5 mt-0.5">
                <Icon color="primary" icon="mdi-email" class="w-4 h-4" />
                {{ data.owner.email }}
              </div>
            </div>
          </div>
        </UCard>

        <!-- Versions Section -->
        <UCard>
          <template #header>
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <Icon icon="mdi-history" class="w-8 h-8 text-primary" />
                <span class="font-semibold">Version History</span>
              </div>
              <UBadge color="primary" variant="soft">
                {{ data.currentVersion.versionNumber }} version{{
                  data.currentVersion.versionNumber > 1 ? "s" : ""
                }}
              </UBadge>
            </div>
          </template>

          <div class="space-y-3">
            <!-- Current Version -->
            <div
              class="flex items-start gap-3 p-3 border border-primary/20 bg-primary/5 rounded-lg"
            >
              <Icon
                icon="mdi-check-circle"
                class="w-5 h-5 text-primary mt-0.5"
              />
              <div class="flex-1">
                <div class="flex items-center gap-2 mb-2">
                  <span class="font-medium"
                    >Version {{ data.currentVersion.versionNumber }}</span
                  >
                  <UBadge color="primary" variant="solid" size="xs"
                    >Current</UBadge
                  >
                </div>
                <div class="grid grid-cols-2 gap-2 text-sm">
                  <div>
                    <Icon icon="mdi-scale" class="w-3.5 h-3.5 inline mr-1" />
                    {{ formatFileSize(Number(data.currentVersion.size)) }}
                  </div>
                  <div
                    class="text-gray-600 dark:text-gray-400 text-ellipsis w-full overflow-hidden"
                  >
                    <Icon
                      icon="mdi-file-document"
                      class="w-3.5 h-3.5 inline mr-1"
                    />
                    {{
                      getFileTypeReadable(
                        data.currentVersion.mimeType,
                        data.fileName,
                      )
                    }}
                  </div>
                </div>
              </div>
              <UButton
                icon="i-mdi-download"
                size="xs"
                color="primary"
                variant="ghost"
                square
              />
            </div>
          </div>
        </UCard>
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed, ref } from "vue";
import type { FileResult } from "@/api/file";
import type { ContextMenuItem, TreeItem } from "@nuxt/ui";
import { useSettingsStore } from "@/stores/settings";
import { useQuery } from "@pinia/colada";
import { getPreview } from "@/queries/files";
import type { TagDto } from "@/api/tag";
import { addTagToFile, removeTagFromFile } from "@/mutations/tags";
import type { SearchTagsSchema } from "@/schemas/tag";
import { searchTag } from "@/queries/tags";
import { getIconByValue } from "@/utils/icon.utils";
import { getTagsForFile } from "@/queries/tags";
import { getFileTypeReadable } from "@/utils/mimetype.utils";
import { formatDate } from "@/utils/date-formatters";

const settingsStore = useSettingsStore();

const iconSize = computed(() =>
  props.viewMode === "grid"
    ? settingsStore.gridIconSize
    : settingsStore.listIconSize,
);

const props = defineProps<{
  data: FileResult;
  viewMode: "grid" | "list";
  isSelected: boolean;
  selectedCount?: number;
  tags: TagDto[] | undefined;
}>();

const openDrawer = ref(false);
const previewUrl = ref<string | null>(null);
const thumbnailUrl = ref<string | null>(null);
const archivePreview = ref<string | null>(null);
const textPreview = ref<string | null>(null);
const previewMimeType = ref<string | null>(null);
const audioPlayer = ref<HTMLAudioElement | null>(null);
const isAudioPlaying = ref<boolean>(false);
const isAudioHovered = ref<boolean>(false);

const toggleAudio = async (): Promise<void> => {
  if (audioPlayer.value) {
    if (audioPlayer.value.paused) {
      await audioPlayer.value.play();
      isAudioPlaying.value = true;
    } else {
      audioPlayer.value.pause();
      isAudioPlaying.value = false;
    }
  }
};

const { data: previewData, isLoading: previewLoading } = useQuery(
  getPreview,
  () => props.data.fileId,
);

const { mutateAsync: addTagMutate } = addTagToFile();

const currentPage = ref(1);
const pageSize = ref(25);
const searchQuery = ref("");

// Search filters
const searchFilters = computed<SearchTagsSchema>(() => ({
  page: currentPage.value,
  pageSize: pageSize.value,
  excludeOnFile: props.data.fileId,
  name: searchQuery.value || undefined,
}));

const { mutateAsync: removeTagMutateAsync } = removeTagFromFile();


const refreshOnRemove = async (id: string) => {
  await removeTagMutateAsync({ fileId: props.data.fileId, tagId: id });
  refreshFileTag();
};

// Query
const {
  data: tagsData,
  isLoading: tagsLoading,
  refresh: refreshFileTag,
} = useQuery(searchTag(searchFilters.value));

const { data: fileTagsData, isLoading: fileTagsLoading } = useQuery({
  ...getTagsForFile(props.data.fileId),
  enabled: () => openDrawer.value,
});

const displayTags = computed(() => {
  return openDrawer.value && fileTagsData.value
    ? fileTagsData.value.tags
    : props.data.tags;
});

const showTagSearch = ref(false);

const emit = defineEmits<{
  click: [event: MouseEvent];
  open: [fileId: string];
  rename: [fileId: string];
  delete: [fileIds: string[]];
  move: [fileIds: string[]];
  copy: [fileIds: string[]];
  download: [fileIds: string[]];
  preview: [fileId: string];
  share: [fileIds: string[]];
}>();

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const items: ContextMenuItem[] = [];

  // Single-select actions
  if (!isMultiSelect) {
    if (canPreview()) {
      items.push([
        {
          label: "Open",
          icon: "i-mdi-open-in-new",
          onSelect: () => emit("open", props.data.fileId),
        },
        {
          label: "Preview",
          icon: "i-mdi-eye",
          onSelect: () => emit("preview", props.data.fileId),
          disabled: !canPreview(),
        },
      ]);
    }

    items.push([
      {
        label: "Download",
        icon: "i-mdi-download",
        onSelect: () => emit("download", [props.data.fileId]),
        disabled: !canDownload(),
      },
      {
        label: "Rename",
        icon: "i-mdi-pencil",
        onSelect: () => emit("rename", props.data.fileId),
        disabled: !canRename(),
      },
    ]);

    items.push([
      {
        label: "Move",
        icon: "i-mdi-folder-move",
        onSelect: () => emit("move", [props.data.fileId]),
        disabled: !canMove(),
      },
      {
        label: "Copy",
        icon: "i-mdi-content-copy",
        onSelect: () => emit("copy", [props.data.fileId]),
        disabled: !canCopy(),
      },
      {
        label: "Share",
        icon: "i-mdi-share-variant",
        onSelect: () => emit("share", [props.data.fileId]),
        disabled: !canShare(),
      },
    ]);

    items.push([
      {
        label: "Delete",
        icon: "i-mdi-delete",
        onSelect: () => emit("delete", [props.data.fileId]),
        disabled: !canDelete(),
      },
    ]);
  } else {
    // Multi-select actions
    items.push([
      {
        label: `Download ${props.selectedCount} items`,
        icon: "i-mdi-download",
        onSelect: () => emit("download", []),
        disabled: !canDownload(),
      },
    ]);

    items.push([
      {
        label: `Move ${props.selectedCount} items`,
        icon: "i-mdi-folder-move",
        onSelect: () => emit("move", []),
        disabled: !canMove(),
      },
      {
        label: `Copy ${props.selectedCount} items`,
        icon: "i-mdi-content-copy",
        onSelect: () => emit("copy", []),
        disabled: !canCopy(),
      },
      {
        label: `Share ${props.selectedCount} items`,
        icon: "i-mdi-share-variant",
        onSelect: () => emit("share", []),
        disabled: !canShare(),
      },
    ]);

    items.push([
      {
        label: `Delete ${props.selectedCount} items`,
        icon: "i-mdi-delete",
        onSelect: () => emit("delete", []),
        disabled: !canDelete(),
      },
    ]);
  }

  return items;
});

// Permission check stubs
const canPreview = (): boolean => {
  // TODO: Implement preview capability check based on file type
  return true;
};

const canRename = (): boolean => {
  // TODO: Implement permission check
  return true;
};

const canMove = (): boolean => {
  // TODO: Implement permission check
  return true;
};

const canCopy = (): boolean => {
  // TODO: Implement permission check
  return true;
};

const canDownload = (): boolean => {
  // TODO: Implement permission check
  return true;
};

const canShare = (): boolean => {
  // TODO: Implement permission check
  return true;
};

const canDelete = (): boolean => {
  // TODO: Implement permission check
  return true;
};

const getFileIcon = (fileName: string): string => {
  const extension = fileName.split(".").pop()?.toLowerCase() ?? "";
  const iconMap: Record<string, string> = {
    // Documents
    pdf: "mdi:file-pdf-box",
    doc: "mdi:file-word",
    docx: "mdi:file-word",
    txt: "mdi:file-document-outline",
    // Spreadsheets
    xls: "mdi:file-excel",
    xlsx: "mdi:file-excel",
    csv: "mdi:file-delimited-outline",
    // Presentations
    ppt: "mdi:file-powerpoint",
    pptx: "mdi:file-powerpoint",
    // Images
    jpg: "mdi:file-image",
    jpeg: "mdi:file-image",
    png: "mdi:file-image",
    gif: "mdi:file-image",
    svg: "mdi:file-image",
    webp: "mdi:file-image",
    // Videos
    mp4: "mdi:file-video",
    avi: "mdi:file-video",
    mov: "mdi:file-video",
    mkv: "mdi:file-video",
    // Audio
    mp3: "mdi:file-music",
    wav: "mdi:file-music",
    flac: "mdi:file-music",
    // Archives
    zip: "mdi:folder-zip",
    rar: "mdi:folder-zip",
    "7z": "mdi:folder-zip",
    tar: "mdi:folder-zip",
    // Code
    js: "mdi:language-javascript",
    ts: "mdi:language-typescript",
    vue: "mdi:vuejs",
    jsx: "mdi:react",
    tsx: "mdi:react",
    py: "mdi:language-python",
    java: "mdi:language-java",
    html: "mdi:language-html5",
    css: "mdi:language-css3",
    json: "mdi:code-json",
  };
  return iconMap[extension] ?? "mdi:file-outline";
};

const pdfDocumentMimes = [
  // PDF itself
  "application/pdf",

  // Microsoft Word
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",

  // OpenDocument Text
  "application/vnd.oasis.opendocument.text",

  // Rich Text
  "application/rtf",
];

const pdfSpreadsheetMimes = [
  // Microsoft Excel
  "application/vnd.ms-excel",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",

  // OpenDocument Spreadsheet
  "application/vnd.oasis.opendocument.spreadsheet",

  // CSV / TSV
  "text/csv",
  "text/tab-separated-values",
];

const pdfPresentationMimes = [
  // Microsoft PowerPoint
  "application/vnd.ms-powerpoint",
  "application/vnd.openxmlformats-officedocument.presentationml.presentation",

  // OpenDocument Presentation
  "application/vnd.oasis.opendocument.presentation",
];

const pdfPreviewMimes = [
  ...pdfDocumentMimes,
  ...pdfSpreadsheetMimes,
  ...pdfPresentationMimes,
];

const formatFileSize = (bytes: number | undefined): string => {
  if (!bytes) return "";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let size = bytes;
  let unitIndex = 0;
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024;
    unitIndex++;
  }
  return `${size.toFixed(1)} ${units[unitIndex]}`;
};

const handleClick = (event: MouseEvent) => {
  // Select item if not selected when right-clicking
  if (!props.isSelected && event.button === 2) {
    emit("click", event);
    return;
  }
  emit("click", event);
};

interface ArchiveEntry {
  Key: string;
  SizeKB: number;
  Modified: string;
}

interface ArchiveData {
  FileCount: number;
  FileName: string;
  Entries: ArchiveEntry[];
}

const archivePreviewItems = computed(() => parseArchivePreview());

const parseArchivePreview = () => {
  if (!archivePreview.value) return null;

  const tree: ArchiveData = JSON.parse(archivePreview.value);
  const rootNode: TreeItem = {
    label: tree.FileName,
    children: [],
    defaultExpanded: true,
    icon: "mdi-folder",
  };

  // Cache nodes by their full path for O(1) access
  const pathCache = new Map<string, TreeItem>();
  pathCache.set("", rootNode);

  tree.Entries.forEach((entry) => {
    const pathParts = entry.Key.split("/");
    let currentPath = "";
    let parentNode = rootNode;

    // Build folder structure
    for (let i = 0; i < pathParts.length - 1; i++) {
      const part = pathParts[i];
      const newPath = currentPath ? `${currentPath}/${part}` : part;

      let folder = pathCache.get(newPath);

      if (!folder) {
        folder = {
          label: part,
          children: [],
          icon: "mdi-folder",
        };

        if (!parentNode.children) {
          parentNode.children = [];
        }
        parentNode.children.push(folder);
        pathCache.set(newPath, folder);
      }

      parentNode = folder;
      currentPath = newPath;
    }

    // Add the file
    if (!parentNode.children) {
      parentNode.children = [];
    }

    const fileName = pathParts[pathParts.length - 1];
    parentNode.children.push({
      label: fileName,
      icon: "mdi-file",
    });
  });

  return [rootNode];
};
const setFilePreviews = async () => {
  // const result = await getFilePreview(props.data.fileId);
  if (previewData.value) {
    previewUrl.value = previewData.value.previewUrl;
    thumbnailUrl.value = previewData.value.thumbnailUrl;
    previewMimeType.value = previewData.value.metaData.mimeType;
    textPreview.value = previewData.value.textPreview;
    archivePreview.value = previewData.value.archivePreview;
  }
  console.log("previewUrl", previewUrl.value);
  console.log("thumbnailUrl", thumbnailUrl.value);
};

const handleDoubleClick = async () => {
  openDrawer.value = true;
  await setFilePreviews();
  parseArchivePreview();
};
</script>

<style scoped></style>
