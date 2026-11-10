<template>
  <section
    aria-labelledby="preview-artifacts-heading"
    class="flex flex-col overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
  >
    <header
      class="flex flex-wrap items-start justify-between gap-3 border-b border-gray-200/70 px-4 py-3 sm:px-6 dark:border-gray-700/70"
    >
      <div class="min-w-0">
        <div class="flex items-center gap-2.5">
          <h2
            id="preview-artifacts-heading"
            class="text-base font-semibold text-gray-900 dark:text-gray-100"
          >
            Preview artifacts
          </h2>
          <UBadge v-if="totalCount > 0" color="neutral" variant="subtle" size="sm">
            {{ totalCount.toLocaleString() }}
          </UBadge>
        </div>
        <p class="mt-0.5 text-sm text-gray-600 dark:text-gray-400">
          Thumbnails and previews regenerate from their files when needed.
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <USelect
          v-model="periodDays"
          :items="PERIOD_OPTIONS"
          size="sm"
          class="w-48"
          aria-label="Created"
        />

        <UButton
          v-if="groups.length > 0"
          color="error"
          variant="outline"
          size="sm"
          icon="i-lucide-trash-2"
          @click="deleteTarget = { kind: 'page' }"
        >
          Delete this page
        </UButton>
      </div>
    </header>

    <div v-if="isPending" class="flex justify-center py-16">
      <BlockSpinner :size="24" aria-label="Loading preview artifacts" class="text-gray-500" />
    </div>

    <div v-else-if="visibleError" class="flex flex-col items-center gap-3 px-4 py-16 text-center">
      <UIcon name="i-lucide-triangle-alert" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
        Couldn't load preview artifacts
      </p>
      <UButton color="neutral" variant="outline" size="sm" @click="refetch()">Try again</UButton>
    </div>

    <div
      v-else-if="groups.length === 0"
      class="flex flex-col items-center gap-3 px-4 py-16 text-center"
    >
      <UIcon name="i-lucide-image" class="h-12 w-12 text-gray-400 dark:text-gray-600" />

      <div class="space-y-1">
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No preview artifacts</p>
        <p class="text-xs text-gray-600 dark:text-gray-400">{{ emptyDescription }}</p>
      </div>
    </div>

    <ul v-else class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
      <li
        v-for="group in groups"
        :key="group.fileId"
        class="flex items-center gap-3 px-4 py-2.5 sm:px-6"
      >
        <Icon
          :icon="getFileIcon(group.fileName)"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />

        <div class="min-w-0 flex-1">
          <p
            class="truncate text-sm font-medium text-gray-900 dark:text-gray-100"
            :title="group.fileName"
          >
            {{ group.fileName }}
          </p>
          <p class="truncate text-xs text-gray-600 dark:text-gray-400">
            {{ describeGroup(group) }}
          </p>
        </div>

        <span
          class="hidden shrink-0 text-sm font-medium tabular-nums text-gray-900 sm:block dark:text-gray-100"
        >
          {{ formatBytes(group.totalBytes) }}
        </span>

        <UDropdownMenu :items="menuFor(group)">
          <UButton
            icon="i-lucide-ellipsis-vertical"
            variant="ghost"
            color="neutral"
            size="sm"
            square
            class="justify-center"
            :aria-label="`Delete options for ${group.fileName}`"
          />
        </UDropdownMenu>
      </li>
    </ul>

    <footer
      v-if="totalPages > 1"
      class="flex flex-wrap items-center justify-center gap-3 border-t border-gray-200/70 px-4 py-2 sm:justify-between sm:px-6 dark:border-gray-700/70"
    >
      <p class="hidden text-xs tabular-nums text-gray-600 sm:block dark:text-gray-400">
        {{ rangeLabel }}
      </p>

      <UPagination
        v-model:page="page"
        :total="totalCount"
        :items-per-page="PAGE_SIZE"
        :sibling-count="1"
        size="sm"
      />
    </footer>

    <ConfirmModal
      :open="deleteTarget.kind !== 'none'"
      :title="deleteDialog.title"
      :description="deleteDialog.description"
      confirm-label="Delete"
      confirm-icon="i-lucide-trash-2"
      confirm-color="error"
      :danger-mode="true"
      :loading="isDeleting"
      @close="handleDialogClose"
    />
  </section>
</template>

<script setup lang="ts">
import type { DropdownMenuItem } from "@nuxt/ui";

import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { UserPreview } from "@/api/status";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useAppToast } from "@/composables/useAppToast";
import { PreviewKind } from "@/enums/PreviewKind";
import { useDeletePreview, useDeletePreviewsByFile } from "@/mutations/storage";
import { type MyPreviewsFilters, myPreviews } from "@/queries/status";
import { formatCount } from "@/utils/count";
import { formatTimeAgo } from "@/utils/date-formatters";
import { getFileIcon } from "@/utils/files/icons";
import { formatBytes } from "@/utils/size";

interface PreviewFileGroup {
  fileId: string;
  fileName: string;
  totalBytes: number;
  newestAt: string;
  items: UserPreview[];
}

type DeleteTarget =
  | { kind: "none" }
  | { kind: "single"; preview: UserPreview; fileName: string }
  | { kind: "file"; group: PreviewFileGroup }
  | { kind: "page" };

const PAGE_SIZE = 20;
const DAY_MS = 86_400_000;

const PERIOD_OPTIONS = [
  { label: "Created any time", value: 0 },
  { label: "Older than 30 days", value: 30 },
  { label: "Older than 90 days", value: 90 },
  { label: "Older than 1 year", value: 365 },
];

const KIND_LABELS: Record<PreviewKind, string> = {
  [PreviewKind.Thumbnail]: "Thumbnail",
  [PreviewKind.Preview]: "Preview",
};

const toast = useAppToast("previews");

const periodDays = ref(0);
const page = ref(1);
const deleteTarget = ref<DeleteTarget>({ kind: "none" });
const isDeleting = ref(false);

const createdBefore = computed(() => {
  if (periodDays.value <= 0) return undefined;

  return new Date(Date.now() - periodDays.value * DAY_MS).toISOString();
});

const filters = computed<MyPreviewsFilters>(() => ({
  createdBefore: createdBefore.value,
  page: page.value,
  pageSize: PAGE_SIZE,
}));

const { data, error, isPending, refetch } = useQuery(() => ({
  ...myPreviews(filters.value),
  placeholderData: (prev) => prev,
}));

const { mutateAsync: deletePreview } = useDeletePreview();
const { mutateAsync: deletePreviewsByFile } = useDeletePreviewsByFile();

const totalCount = computed(() => data.value?.totalCount ?? 0);
const totalPages = computed(() => data.value?.totalPages ?? 0);

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

const groups = computed<PreviewFileGroup[]>(() => {
  const byFile = new Map<string, PreviewFileGroup>();

  for (const item of data.value?.items ?? []) {
    const group = byFile.get(item.fileId) ?? {
      fileId: item.fileId,
      fileName: item.fileName,
      items: [],
      newestAt: item.createdAt,
      totalBytes: 0,
    };

    group.items.push(item);
    group.totalBytes += item.sizeBytes;

    if (item.createdAt > group.newestAt) group.newestAt = item.createdAt;

    byFile.set(item.fileId, group);
  }

  return [...byFile.values()];
});

const rangeLabel = computed(() => {
  const start = (page.value - 1) * PAGE_SIZE + 1;
  const end = Math.min(page.value * PAGE_SIZE, totalCount.value);

  return `${start}–${end} of ${totalCount.value.toLocaleString()}`;
});

const emptyDescription = computed(() => {
  if (periodDays.value > 0) return "Nothing that old. Try a shorter window.";

  return "Nothing regenerable to clean up.";
});

const describeGroup = (group: PreviewFileGroup) => {
  const parts = group.items.map(
    (item) => `${KIND_LABELS[item.kind]} ${formatBytes(item.sizeBytes)}`,
  );

  parts.push(`created ${formatTimeAgo(group.newestAt)}`);

  return parts.join(" · ");
};

const menuFor = (group: PreviewFileGroup): DropdownMenuItem[][] => {
  const singles = group.items.map((preview) => ({
    icon: "i-lucide-trash-2",
    label: `Delete ${KIND_LABELS[preview.kind].toLowerCase()}`,
    onSelect: () => {
      deleteTarget.value = { fileName: group.fileName, kind: "single", preview };
    },
  }));

  if (group.items.length === 1) return [singles];

  return [
    singles,
    [
      {
        color: "error",
        icon: "i-lucide-trash-2",
        label: "Delete all for this file",
        onSelect: () => {
          deleteTarget.value = { group, kind: "file" };
        },
      },
    ],
  ];
};

const deleteDialog = computed(() => {
  const target = deleteTarget.value;

  if (target.kind === "single") {
    const kind = KIND_LABELS[target.preview.kind].toLowerCase();

    return {
      description: `Removes the ${kind} of ${target.fileName} (${formatBytes(target.preview.sizeBytes)}). It regenerates from the file when needed.`,
      title: `Delete ${kind}`,
    };
  }

  if (target.kind === "file") {
    return {
      description: `Removes ${target.group.items.length} artifacts of ${target.group.fileName} (${formatBytes(target.group.totalBytes)}). They regenerate from the file when needed.`,
      title: "Delete file previews",
    };
  }

  const pageBytes = groups.value.reduce((sum, group) => sum + group.totalBytes, 0);

  return {
    description: `Removes the previews of the ${formatCount(groups.value.length, "file")} on this page (${formatBytes(pageBytes)}). They regenerate from their files when needed.`,
    title: "Delete this page",
  };
});

const notifySuccess = (title: string, freedBytes: number) => {
  toast.success(title, `Freed ${formatBytes(freedBytes)} of storage.`);
};

const notifyFailure = (cause?: unknown) => {
  toast.error("Couldn't delete the previews", cause);
};

const deleteFiles = async (fileIds: string[]) => {
  const results = await Promise.allSettled(
    fileIds.map((fileId) => deletePreviewsByFile({ createdBefore: createdBefore.value, fileId })),
  );

  let freedBytes = 0;
  let deletedCount = 0;

  for (const result of results) {
    if (result.status === "fulfilled") {
      freedBytes += result.value.freedBytes;
      deletedCount += result.value.deletedCount;
    }
  }

  return { deletedCount, failed: results.some((r) => r.status === "rejected"), freedBytes };
};

const runDelete = async (target: DeleteTarget) => {
  if (target.kind === "single") {
    const result = await deletePreview(target.preview.previewId);
    notifySuccess("Preview deleted", result.freedBytes);
    return;
  }

  if (target.kind !== "file" && target.kind !== "page") return;

  const fileIds =
    target.kind === "file" ? [target.group.fileId] : groups.value.map((g) => g.fileId);
  const result = await deleteFiles(fileIds);

  if (result.failed && result.deletedCount > 0) {
    toast.warning(
      `Deleted ${formatCount(result.deletedCount, "preview")}, some files failed`,
      `Freed ${formatBytes(result.freedBytes)}. Run the delete again for the rest.`,
    );
    return;
  }

  if (result.failed) {
    notifyFailure();
    return;
  }

  notifySuccess(`Deleted ${formatCount(result.deletedCount, "preview")}`, result.freedBytes);
};

const handleDialogClose = async (confirmed: boolean) => {
  if (isDeleting.value) return;

  if (!confirmed) {
    deleteTarget.value = { kind: "none" };
    return;
  }

  isDeleting.value = true;

  try {
    await runDelete(deleteTarget.value);
  } catch (err) {
    notifyFailure(err);
  } finally {
    isDeleting.value = false;
    deleteTarget.value = { kind: "none" };
  }
};

watch(periodDays, () => {
  page.value = 1;
});

// Deleting the last rows of the final page would otherwise strand the view on an empty page.
watch(totalPages, (pages) => {
  if (pages > 0 && page.value > pages) page.value = pages;
});
</script>
