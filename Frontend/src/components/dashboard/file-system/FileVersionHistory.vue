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

    <!-- Loading skeleton -->
    <div v-if="versionsLoading" class="space-y-3">
      <USkeleton v-for="i in 3" :key="i" class="h-16 w-full rounded-lg" />
    </div>

    <!-- Version list -->
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

      <!-- Pagination -->
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

    <!-- Empty state -->
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
import { changeActiveVersion, deleteVersion, restoreFileVersion } from "@/mutations/files";
import { getVersionsForFile } from "@/queries/files";
import { getFileTypeReadable } from "@/utils/mimetype.utils";
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";
import { fileApi } from "@/api/file";
import { formatBytes } from "@/utils/size.utils";
import { logger } from "@/utils/logger";

const props = defineProps<{
  fileId: string;
  fileName: string;
  currentVersionId: string;
  currentVersionNumber: number;
}>();

const emit = defineEmits<{
  "versions-changed": [];
}>();

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

const { mutation: deleteVersionMutate } = deleteVersion();
const { mutation: changeActiveVersionMutate } = changeActiveVersion();
const { mutation: restoreVersionMutate } = restoreFileVersion();

const deletingVersionId = ref<string | null>(null);
const changingActiveVersionId = ref<string | null>(null);

// Handlers
const handleDownloadVersion = async (versionId: string, fileName: string) => {
  const url = await fileApi.downloadFileVersion(versionId);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
};

const handleDeleteVersion = async (versionId: string) => {
  deletingVersionId.value = versionId;
  try {
    await deleteVersionMutate({ fileId: props.fileId, versionId });
    await refreshVersions();
    emit("versions-changed");
  } finally {
    deletingVersionId.value = null;
  }
};

const handleChangeActiveVersion = async (versionId: string) => {
  changingActiveVersionId.value = versionId;
  logger.log("activeVersion ", versionId);
  try {
    await changeActiveVersionMutate({ fileId: props.fileId, versionId });
    await refreshVersions();
    emit("versions-changed");
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
  } finally {
    changingActiveVersionId.value = null;
  }
};
</script>
