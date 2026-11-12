<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { AuditExportBounds, AuditExportPart, AuditExportPlan } from "@/api/activity";
import type { AuditExportFormat } from "@/enums/audit-export-format";

import { useAppToast } from "@/composables/useAppToast";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { exportBounds, exportFileName, exportPartSummary } from "@/features/audit/export";
import { formatCount } from "@/utils/count";
import { saveBlob } from "@/utils/download";
import { glassModalContent } from "@/utils/modal-ui";

type PartStatus = "idle" | "downloading" | "saved";

const { open, plan, format, baseName, stamp, download } = defineProps<{
  open: boolean;
  plan: AuditExportPlan;
  format: AuditExportFormat;
  baseName: string;
  // Shared by every part so the files sort together in the downloads folder.
  stamp: string;
  download: (format: AuditExportFormat, bounds: AuditExportBounds) => Promise<Blob>;
}>();

const emit = defineEmits<{ close: [] }>();

useModalBackGuard(() => emit("close"));

const appToast = useAppToast("activity");

const statuses = ref<Record<number, PartStatus>>({});
const isDownloadingAll = ref(false);

watch(
  () => plan,
  () => {
    statuses.value = {};
  },
);

const statusOf = (part: AuditExportPart): PartStatus => statuses.value[part.index] ?? "idle";

const isBusy = computed(() =>
  Object.values(statuses.value).some((status) => status === "downloading"),
);

const allSaved = computed(() => plan.parts.every((part) => statusOf(part) === "saved"));

const description = computed(
  () =>
    `${formatCount(plan.total, "row")} is more than one file holds, so the export comes in ` +
    `${plan.parts.length} files of up to ${formatCount(plan.partSize, "row")}, newest first. ` +
    "Activity recorded after you started the export is left out.",
);

const savePart = async (part: AuditExportPart) => {
  if (statusOf(part) === "downloading") return;

  statuses.value = { ...statuses.value, [part.index]: "downloading" };

  try {
    const blob = await download(format, exportBounds(part));
    saveBlob(blob, exportFileName(baseName, stamp, format, { count: plan.parts.length, index: part.index }));
    statuses.value = { ...statuses.value, [part.index]: "saved" };
  } catch (error) {
    statuses.value = { ...statuses.value, [part.index]: "idle" };
    appToast.error(`Couldn't export part ${part.index}`, error);
  }
};

// One at a time, so a large export never holds several files in memory at once.
const saveAll = async () => {
  if (isDownloadingAll.value) return;

  isDownloadingAll.value = true;
  try {
    for (const part of plan.parts) {
      if (statusOf(part) !== "saved") await savePart(part);
    }
  } finally {
    isDownloadingAll.value = false;
  }
};

const buttonLabel = (part: AuditExportPart) => {
  if (statusOf(part) === "saved") return "Saved";

  return "Download";
};

const buttonIcon = (part: AuditExportPart) => {
  if (statusOf(part) === "saved") return "i-lucide-check";

  return "i-lucide-download";
};
</script>

<template>
  <UModal
    :open="open"
    title="Export in parts"
    :description="description"
    :close="{ onClick: () => emit('close') }"
    :ui="{ content: glassModalContent }"
  >
    <template #body>
      <ul
        class="divide-y divide-gray-200/70 dark:divide-gray-700/70"
        aria-label="Export parts"
      >
        <li v-for="part in plan.parts" :key="part.index" class="flex items-center gap-3 py-3">
          <UIcon
            name="i-lucide-file-text"
            class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
          />

          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
              Part {{ part.index }} of {{ plan.parts.length }}
            </p>
            <p class="truncate text-xs text-gray-600 dark:text-gray-400">
              {{ exportPartSummary(part) }}
            </p>
          </div>

          <UButton
            :label="buttonLabel(part)"
            :icon="buttonIcon(part)"
            :loading="statusOf(part) === 'downloading'"
            :variant="statusOf(part) === 'saved' ? 'ghost' : 'outline'"
            color="neutral"
            size="sm"
            class="shrink-0"
            @click="savePart(part)"
          />
        </li>
      </ul>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton label="Close" color="neutral" variant="outline" @click="emit('close')" />
        <UButton
          label="Download all"
          icon="i-lucide-download"
          color="primary"
          variant="solid"
          :loading="isDownloadingAll"
          :disabled="allSaved || (isBusy && !isDownloadingAll)"
          @click="saveAll"
        />
      </div>
    </template>
  </UModal>
</template>
