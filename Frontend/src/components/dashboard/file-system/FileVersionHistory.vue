<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Icon icon="mdi-history" class="w-8 h-8 text-primary" />
          <span class="font-semibold">Version History</span>
        </div>
        <UBadge color="primary" variant="soft">
          {{ versionsData?.totalCount ?? 0 }}
          version{{ (versionsData?.totalCount ?? 0) !== 1 ? "s" : "" }}
        </UBadge>
      </div>
    </template>

    <div v-if="versionsLoading" class="space-y-3">
      <USkeleton v-for="i in 3" :key="i" class="h-16 w-full rounded-lg" />
    </div>

    <div v-else-if="versionsData?.items?.length" class="space-y-2">
      <div
        v-for="version in versionsData.items"
        :key="version.id"
        class="flex items-start gap-3 p-3 rounded-lg border transition-colors"
        :class="
          version.isDeleted
            ? 'border-error/30 bg-error/5'
            : version.versionNumber === currentVersionNumber
              ? 'border-primary/30 bg-primary/5'
              : 'border-neutral-200 dark:border-neutral-700 bg-neutral-50 dark:bg-neutral-800/30'
        "
      >
        <Icon
          :icon="
            version.isDeleted
              ? 'mdi-delete-clock'
              : version.versionNumber === currentVersionNumber
                ? 'mdi-check-circle'
                : 'mdi-clock-outline'
          "
          class="w-5 h-5 mt-0.5 shrink-0"
          :class="
            version.isDeleted
              ? 'text-error'
              : version.versionNumber === currentVersionNumber
                ? 'text-primary'
                : 'text-neutral-400 dark:text-neutral-500'
          "
        />

        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2 mb-1.5">
            <span class="font-medium text-sm">Version {{ version.versionNumber }}</span>

            <UTooltip
              v-if="version.isEncrypted"
              label="This version is encrypted"
              :delay-duration="400"
            >
              <Icon icon="mdi-lock" class="w-3.5 h-3.5 text-primary shrink-0" />
            </UTooltip>

            <UBadge
              v-if="version.isDeleted"
              color="error"
              variant="subtle"
              size="xs"
              label="Deleted"
            />
            <UBadge
              v-else-if="version.versionNumber === currentVersionNumber"
              color="primary"
              variant="solid"
              size="xs"
              label="Current"
            />

            <Transition
              enter-active-class="transition-all duration-200 ease-out"
              enter-from-class="opacity-0 scale-90"
              enter-to-class="opacity-100 scale-100"
              leave-active-class="transition-all duration-150 ease-in"
              leave-from-class="opacity-100 scale-100"
              leave-to-class="opacity-0 scale-90"
            >
              <UBadge
                v-if="queuedVersionIds.has(version.id)"
                color="success"
                variant="subtle"
                size="xs"
              >
                <Icon icon="mdi-check-circle-outline" class="w-3.5 h-3.5 mr-0.5" />
                Queued
              </UBadge>
            </Transition>
          </div>

          <div
            class="grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-neutral-500 dark:text-neutral-400"
          >
            <div class="flex items-center gap-1">
              <Icon icon="mdi-scale" class="w-3.5 h-3.5 shrink-0" />
              <span>{{ formatBytes(Number(version.size)) }}</span>
            </div>
            <div class="flex items-center gap-1 truncate">
              <Icon icon="mdi-file-document" class="w-3.5 h-3.5 shrink-0" />
              <span class="truncate">{{ getFileTypeReadable(version.mimeType, fileName) }}</span>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-1 shrink-0">
          <UButton
            icon="i-mdi-download"
            size="xs"
            :color="version.versionNumber === currentVersionNumber ? 'primary' : 'neutral'"
            variant="ghost"
            square
            :disabled="version.isDeleted"
            :title="`Download version ${version.versionNumber}`"
            @click="handleDownloadVersion(version.id)"
          />

          <!-- Transpilation popover -->
          <UPopover v-if="!version.isDeleted" v-model:open="transpilationPopoverOpen[version.id]">
            <UButton
              icon="i-mdi-transfer"
              size="xs"
              color="neutral"
              variant="ghost"
              square
              :loading="queuingTranspilationVersionId === version.id"
              :disabled="queuingTranspilationVersionId !== null || deletingVersionId !== null"
              :title="`Queue transpilation job for version ${version.versionNumber}`"
            />

            <template #content>
              <div class="p-4 w-72 space-y-4">
                <p class="text-sm font-medium">Queue Transpilation</p>

                <!-- Audio rungs — shown for audio files -->
                <div v-if="!isVideoFile(version.mimeType)" class="space-y-2">
                  <p class="text-xs text-neutral-500 dark:text-neutral-400">Audio quality</p>
                  <div class="flex flex-wrap gap-2">
                    <button
                      v-for="rung in audioRungOptions"
                      :key="rung.value"
                      class="px-2.5 py-1 rounded text-xs border transition-all"
                      :class="
                        selectedAudioRungs[version.id]?.includes(rung.value)
                          ? 'border-primary/60 bg-primary/10 text-primary ring-1 ring-primary scale-[1.03]'
                          : 'border-neutral-200 dark:border-neutral-700 text-neutral-600 dark:text-neutral-400 hover:border-neutral-400 dark:hover:border-neutral-500'
                      "
                      @click="toggleAudioRung(version.id, rung.value)"
                    >
                      {{ rung.label }}
                    </button>
                  </div>
                </div>

                <!-- Video rungs — shown for video files -->
                <div v-if="isVideoFile(version.mimeType)" class="space-y-2">
                  <p class="text-xs text-neutral-500 dark:text-neutral-400">Video quality</p>
                  <div class="flex flex-wrap gap-2">
                    <button
                      v-for="rung in videoRungOptions"
                      :key="rung.value"
                      class="px-2.5 py-1 rounded text-xs border transition-all"
                      :class="
                        selectedVideoRungs[version.id]?.includes(rung.value)
                          ? 'border-primary/60 bg-primary/10 text-primary ring-1 ring-primary scale-[1.03]'
                          : 'border-neutral-200 dark:border-neutral-700 text-neutral-600 dark:text-neutral-400 hover:border-neutral-400 dark:hover:border-neutral-500'
                      "
                      @click="toggleVideoRung(version.id, rung.value)"
                    >
                      {{ rung.label }}
                    </button>
                  </div>
                </div>

                <div
                  class="flex items-center justify-between gap-2 pt-1 border-t border-neutral-200/70 dark:border-neutral-700/70"
                >
                  <span class="text-xs text-neutral-400">
                    {{ selectedRungCount(version.id, version.mimeType) }} rung{{
                      selectedRungCount(version.id, version.mimeType) !== 1 ? "s" : ""
                    }}
                    selected
                  </span>
                  <UButton
                    size="xs"
                    color="primary"
                    variant="solid"
                    label="Queue"
                    :disabled="selectedRungCount(version.id, version.mimeType) === 0"
                    :loading="queuingTranspilationVersionId === version.id"
                    @click="handleQueueTranspilationJob(version.id, version.mimeType)"
                  />
                </div>
              </div>
            </template>
          </UPopover>

          <UButton
            icon="i-mdi-delete-outline"
            size="xs"
            color="error"
            variant="ghost"
            square
            :loading="deletingVersionId === version.id"
            :disabled="deletingVersionId !== null || version.isDeleted"
            :title="`Delete version ${version.versionNumber}`"
            @click="handleDeleteVersion(version.id)"
          />
          <UButton
            v-if="!version.isDeleted && version.versionNumber !== currentVersionNumber"
            icon="i-mdi-check-circle-outline"
            size="xs"
            color="primary"
            variant="ghost"
            square
            :loading="changingActiveVersionId === version.id"
            :disabled="changingActiveVersionId !== null || deletingVersionId !== null"
            :title="`Set version ${version.versionNumber} as active`"
            @click="handleChangeActiveVersion(version.id)"
          />
          <UButton
            v-if="version.isDeleted && version.versionNumber !== currentVersionNumber"
            icon="mdi-restore"
            size="xs"
            color="primary"
            variant="solid"
            label="Restore"
            :loading="changingActiveVersionId === version.id"
            :disabled="changingActiveVersionId !== null || deletingVersionId !== null"
            :title="`Restore version ${version.versionNumber}`"
            @click="handleRestoreVersion(version.id)"
          />
        </div>
      </div>

      <div
        v-if="versionsData.totalCount > versionsPageSize"
        class="flex items-center justify-between pt-2 border-t border-neutral-200 dark:border-neutral-700"
      >
        <span class="text-xs text-neutral-500 dark:text-neutral-400">
          Showing {{ versionsData.items.length }} of {{ versionsData.totalCount }} versions
        </span>
        <div class="flex items-center gap-1">
          <UButton
            icon="i-mdi-chevron-left"
            size="xs"
            variant="ghost"
            color="neutral"
            square
            :disabled="versionsPage <= 1"
            @click="versionsPage--"
          />
          <span class="text-xs px-1">{{ versionsPage }}</span>
          <UButton
            icon="i-mdi-chevron-right"
            size="xs"
            variant="ghost"
            color="neutral"
            square
            :disabled="versionsPage * versionsPageSize >= versionsData.totalCount"
            @click="versionsPage++"
          />
        </div>
      </div>
    </div>

    <div
      v-else
      class="flex flex-col items-center gap-2 py-4 text-neutral-400 dark:text-neutral-500"
    >
      <Icon icon="mdi-history" class="w-8 h-8" />
      <span class="text-sm">No version history available</span>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, reactive, ref } from "vue";

import { AudioRung, VideoRung } from "@/api/policy";
import { useAppToast } from "@/composables/useAppToast";
import { useFileDownload } from "@/composables/useFileDownload";
import { changeActiveVersion, deleteVersion, restoreFileVersion } from "@/mutations/files";
import { queueTranspilationJob } from "@/mutations/streaming";
import { getVersionsForFile } from "@/queries/files";
import { getFileTypeReadable } from "@/utils/mimetype.utils";
import { formatBytes } from "@/utils/size.utils";

const props = defineProps<{
  fileId: string;
  fileName: string;
  currentVersionId: string;
  currentVersionNumber: number;
}>();

const emit = defineEmits<{
  "versions-changed": [];
}>();

const toast = useAppToast();

// Rung option definitions

const audioRungOptions = [
  { label: "96 kbps", value: AudioRung.Kbps96 },
  { label: "128 kbps", value: AudioRung.Kbps128 },
  { label: "192 kbps", value: AudioRung.Kbps192 },
  { label: "256 kbps", value: AudioRung.Kbps256 },
  { label: "320 kbps", value: AudioRung.Kbps320 },
];

const videoRungOptions = [
  { label: "360p", value: VideoRung.P360 },
  { label: "480p", value: VideoRung.P480 },
  { label: "720p", value: VideoRung.P720 },
  { label: "1080p", value: VideoRung.P1080 },
  { label: "1440p", value: VideoRung.P1440 },
  { label: "2160p", value: VideoRung.P2160 },
];

const isVideoFile = (mimeType: string) => mimeType.startsWith("video/");

// Per-version rung selection state

const selectedAudioRungs = reactive<Record<string, AudioRung[]>>({});
const selectedVideoRungs = reactive<Record<string, VideoRung[]>>({});
const transpilationPopoverOpen = reactive<Record<string, boolean>>({});

const toggleAudioRung = (versionId: string, rung: AudioRung) => {
  if (!selectedAudioRungs[versionId]) selectedAudioRungs[versionId] = [];
  const idx = selectedAudioRungs[versionId].indexOf(rung);
  if (idx === -1) selectedAudioRungs[versionId].push(rung);
  else selectedAudioRungs[versionId].splice(idx, 1);
};

const toggleVideoRung = (versionId: string, rung: VideoRung) => {
  if (!selectedVideoRungs[versionId]) selectedVideoRungs[versionId] = [];
  const idx = selectedVideoRungs[versionId].indexOf(rung);
  if (idx === -1) selectedVideoRungs[versionId].push(rung);
  else selectedVideoRungs[versionId].splice(idx, 1);
};

const selectedRungCount = (versionId: string, mimeType: string) =>
  isVideoFile(mimeType)
    ? (selectedVideoRungs[versionId]?.length ?? 0)
    : (selectedAudioRungs[versionId]?.length ?? 0);

// Download

const { downloadVersion } = useFileDownload();

// Versions query

const versionsPage = ref(1);
const versionsPageSize = ref(10);

const versionsQueryParams = computed(() => ({
  id: props.fileId,
  page: versionsPage.value,
  pageSize: versionsPageSize.value,
}));

const {
  data: versionsData,
  isLoading: versionsLoading,
  refresh: refreshVersions,
} = useQuery(() => getVersionsForFile(versionsQueryParams.value));

// Mutations

const { mutateAsync: deleteVersionMutate } = deleteVersion();
const { mutateAsync: changeActiveVersionMutate } = changeActiveVersion();
const { mutateAsync: restoreVersionMutate } = restoreFileVersion();
const { mutateAsync: queueTranspilationJobMutate } = queueTranspilationJob();

const deletingVersionId = ref<string | null>(null);
const changingActiveVersionId = ref<string | null>(null);
const queuingTranspilationVersionId = ref<string | null>(null);

const queuedVersionIds = ref<Set<string>>(new Set());

// Handlers

const handleDownloadVersion = (versionId: string) => {
  downloadVersion(versionId);
};

const handleDeleteVersion = async (versionId: string) => {
  deletingVersionId.value = versionId;
  try {
    await deleteVersionMutate({ fileId: props.fileId, versionId });
    await refreshVersions();
    emit("versions-changed");
  } catch (err) {
    toast.error("Failed to delete version", err);
  } finally {
    deletingVersionId.value = null;
  }
};

const handleChangeActiveVersion = async (versionId: string) => {
  changingActiveVersionId.value = versionId;
  try {
    await changeActiveVersionMutate({ fileId: props.fileId, versionId });
    await refreshVersions();
    emit("versions-changed");
  } catch (err) {
    toast.error("Failed to change active version", err);
  } finally {
    changingActiveVersionId.value = null;
  }
};

const handleRestoreVersion = async (versionId: string) => {
  changingActiveVersionId.value = versionId;
  try {
    await restoreVersionMutate({ fileId: props.fileId, versionId });
    await refreshVersions();
    emit("versions-changed");
  } catch (err) {
    toast.error("Failed to restore version", err);
  } finally {
    changingActiveVersionId.value = null;
  }
};

const handleQueueTranspilationJob = async (versionId: string, mimeType: string) => {
  queuingTranspilationVersionId.value = versionId;
  try {
    await queueTranspilationJobMutate({
      versionId,
      audioRungs: isVideoFile(mimeType) ? [] : (selectedAudioRungs[versionId] ?? []),
      videoRungs: isVideoFile(mimeType) ? (selectedVideoRungs[versionId] ?? []) : [],
    });

    transpilationPopoverOpen[versionId] = false;

    queuedVersionIds.value = new Set([...queuedVersionIds.value, versionId]);
    setTimeout(() => {
      queuedVersionIds.value = new Set(
        [...queuedVersionIds.value].filter((id) => id !== versionId),
      );
    }, 2500);
  } catch (err) {
    toast.error("Failed to queue transpilation job", err);
  } finally {
    queuingTranspilationVersionId.value = null;
  }
};
</script>
