<template>
  <DetailsDrawerShell
    v-model:open="isOpen"
    :title="detail?.fileName ?? ''"
    title-label="File name"
    :description="drawerDescription"
    wide
  >
    <template #icon>
      <UIcon v-if="detail" :name="getFileIcon(detail.fileName)" class="h-6 w-6" />
    </template>

    <template v-if="canOpenInEditor && displayFile" #actions>
      <UTooltip text="Open in editor">
        <UButton
          icon="i-lucide-file-pen-line"
          color="neutral"
          variant="ghost"
          square
          class="justify-center"
          aria-label="Open in editor"
          :to="{ name: 'note-editor', params: { fileId: displayFile.fileId } }"
        />
      </UTooltip>
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
          <UIcon name="i-lucide-lock" class="h-3.5 w-3.5" aria-hidden="true" />
          Encrypted
        </span>
      </template>

      <template v-else-if="detail.currentVersion.isPasswordProtected">
        <span aria-hidden="true">·</span>
        <span class="flex items-center gap-1">
          <UIcon name="i-lucide-file-lock" class="h-3.5 w-3.5" aria-hidden="true" />
          Password protected
        </span>
      </template>
    </template>

    <template v-if="displayFile && detail">
      <section
        v-if="detail.currentVersion.isPasswordProtected"
        class="flex flex-col gap-3"
        data-password-protected-notice
      >
        <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Preview</h3>

        <div
          class="flex items-start gap-3 rounded-lg border border-gray-200/70 p-3 dark:border-gray-700/70"
        >
          <UIcon
            name="i-lucide-file-lock"
            class="mt-0.5 h-5 w-5 shrink-0 text-gray-500 dark:text-gray-400"
            aria-hidden="true"
          />
          <div class="flex min-w-0 flex-col gap-0.5">
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
              No preview for locked documents
            </p>
            <p class="text-sm text-gray-600 dark:text-gray-400">
              The document needs a password to open. Download it and open it with the password.
            </p>
          </div>
        </div>
      </section>

      <FilePreview
        v-else-if="!detail.currentVersion.isEncrypted"
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
            v-if="isAudioFile && isAutoTaggingEnabled"
            icon="i-lucide-tag"
            label="Auto-tag"
            size="xs"
            variant="outline"
            color="neutral"
            :loading="isAutoTagging"
            :disabled="isAutoTagging"
            @click="autoTag"
          />

          <FileTagPicker :file-id="displayFile.fileId" :file-label="fileLabel" />
        </template>

        <FileTagList
          :file-id="displayFile.fileId"
          :fallback-tags="fallbackTags"
          :enabled="isOpen"
          scroll-on-mobile
        />
      </DetailsSection>

      <DetailsSection title="Mentioned in">
        <template #actions>
          <UButton
            icon="i-lucide-notebook-pen"
            label="New note"
            size="xs"
            variant="outline"
            color="neutral"
            :loading="isCreatingNote"
            :disabled="isCreatingNote"
            :aria-label="`New note about ${detail.fileName}`"
            @click="createNoteAbout"
          />
        </template>

        <!-- Rows carry their own hover padding; pulling them out keeps their text in line with
             the other sections. -->
        <NoteBacklinksList :target="backlinkTarget" :enabled="isOpen" class="-mx-2" />
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
            :hint="formatTimeAgo(detail.createdAt)"
            :class="{ 'col-span-2': !detail.updatedAt }"
          >
            {{ formatCalendarDate(detail.createdAt) }}
          </DetailsTile>

          <DetailsTile
            v-if="detail.updatedAt"
            label="Modified"
            :hint="formatTimeAgo(detail.updatedAt)"
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

      <FileCaptureDetails
        v-if="hasCaptureMetadata"
        :file-id="displayFile.fileId"
        :version-id="detail.currentVersion.id"
        :enabled="isOpen"
      />

      <FileDocumentDetails
        v-if="hasDocumentMetadata"
        :file-id="displayFile.fileId"
        :version-id="detail.currentVersion.id"
        :is-book="isEbookMimeType(detail.currentVersion.mimeType)"
        :enabled="isOpen"
      />

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

      <EntityHistorySection
        :entity-type="EntityType.File"
        :entity-id="displayFile.fileId"
        :enabled="isOpen"
      />
    </template>
  </DetailsDrawerShell>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { TagDto } from "@/api/tag";

import { type FileResult } from "@/api/file";
import DetailsDrawerShell from "@/components/common/details/DetailsDrawerShell.vue";
import DetailsOwner from "@/components/common/details/DetailsOwner.vue";
import DetailsSection from "@/components/common/details/DetailsSection.vue";
import DetailsTile from "@/components/common/details/DetailsTile.vue";
import EntityHistorySection from "@/components/dashboard/audit/EntityHistorySection.vue";
import FileTagList from "@/components/dashboard/tags/FileTagList.vue";
import FileTagPicker from "@/components/dashboard/tags/FileTagPicker.vue";
import NoteBacklinksList from "@/components/notes/NoteBacklinksList.vue";
import { useAutoTag } from "@/composables/useAutoTag";
import { useCreateNoteAbout } from "@/composables/useCreateNoteAbout";
import { EntityType } from "@/enums/entity-type";
import { LinkTargetKind } from "@/enums/link-target-kind";
import { quoted } from "@/features/toast/format";
import { getFile } from "@/queries/files";
import { formatCalendarDate, formatTimeAgo } from "@/utils/date-formatters";
import { getFileIcon } from "@/utils/files/icons";
import {
  getFileTypeReadable,
  isAutoTagSupportedFileType,
  isCaptureMetadataSupportedMimeType,
  isDocumentMetadataSupportedMimeType,
  isEbookMimeType,
  isMarkdownMimeType,
} from "@/utils/files/mimetype";
import { formatBytes } from "@/utils/size";

import FileCaptureDetails from "./FileCaptureDetails.vue";
import FileDocumentDetails from "./FileDocumentDetails.vue";
import FilePreview from "./FilePreview.vue";
import FileVersionHistory from "./FileVersionHistory.vue";

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
  return `Created ${formatTimeAgo(detail.value.createdAt)}`;
});

const fileTypeLabel = computed(() => {
  if (!detail.value) return "";
  return getFileTypeReadable(detail.value.currentVersion.mimeType, detail.value.fileName);
});


// Supports tag-derivation (narrower than "is music"), so it only gates Auto-tag.
const isAudioFile = computed(() =>
  detail.value ? isAutoTagSupportedFileType(detail.value.currentVersion.mimeType) : false,
);

// Encrypted versions are never read by the worker, so asking would only report "not read yet".
const hasCaptureMetadata = computed(() => {
  if (!detail.value || detail.value.currentVersion.isEncrypted) return false;

  return isCaptureMetadataSupportedMimeType(detail.value.currentVersion.mimeType);
});

const hasDocumentMetadata = computed(() => {
  if (!detail.value || detail.value.currentVersion.isEncrypted) return false;

  return isDocumentMetadataSupportedMimeType(detail.value.currentVersion.mimeType);
});

// The editor has no password step yet, so encrypted and locked notes stay download-only.
const canOpenInEditor = computed(() => {
  const version = detail.value?.currentVersion;

  if (!version || version.isEncrypted || version.isPasswordProtected) return false;

  return isMarkdownMimeType(version.mimeType);
});

const fileLabel = computed(() => {
  const name = detail.value?.fileName;

  return name ? quoted(name) : "this file";
});

const backlinkTarget = computed(() => ({
  id: displayFile.value?.fileId ?? "",
  kind: LinkTargetKind.File,
}));

const { createAbout, isCreating: isCreatingNote } = useCreateNoteAbout();

const createNoteAbout = () => {
  if (!displayFile.value || !detail.value) return;

  void createAbout({
    id: displayFile.value.fileId,
    kind: LinkTargetKind.File,
    name: detail.value.fileName,
  });
};

const { autoTag, isAutoTagging, isAutoTaggingEnabled } = useAutoTag({
  fileId: () => displayFile.value?.fileId,
  fileLabel,
});

// What the drawer already knows about the file's tags, shown until the file's own list loads.
const fallbackTags = computed((): TagDto[] => {
  if (isOpen.value && fileDetail.value?.tags) return fileDetail.value.tags;

  return displayFile.value?.tags ?? [];
});

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
</script>
