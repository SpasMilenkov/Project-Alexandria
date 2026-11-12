<script setup lang="ts">
import { computed, ref } from "vue";

import type { AuditExportBounds, AuditExportPlan } from "@/api/activity";

import AuditExportPartsModal from "@/components/dashboard/audit/AuditExportPartsModal.vue";
import { useAppToast } from "@/composables/useAppToast";
import { AuditExportFormat } from "@/enums/audit-export-format";
import { exportBounds, exportFileName } from "@/features/audit/export";
import { fileStamp, saveBlob } from "@/utils/download";

interface Checkout {
  format: AuditExportFormat;
  plan: AuditExportPlan;
  stamp: string;
}

const { plan, download, baseName } = defineProps<{
  // Both run with the page's current filters.
  plan: () => Promise<AuditExportPlan>;
  download: (format: AuditExportFormat, bounds?: AuditExportBounds) => Promise<Blob>;
  baseName: string;
}>();

const appToast = useAppToast("activity");

const pending = ref<AuditExportFormat | null>(null);
const checkout = ref<Checkout | null>(null);

// A filter that fits in one file downloads straight away; a bigger one opens the parts list.
const run = async (format: AuditExportFormat) => {
  if (pending.value !== null) return;

  pending.value = format;
  try {
    const result = await plan();
    const stamp = fileStamp();

    if (result.parts.length > 1) {
      checkout.value = { format, plan: result, stamp };
      return;
    }

    const [only] = result.parts;
    const blob = await download(format, only ? exportBounds(only) : undefined);
    saveBlob(blob, exportFileName(baseName, stamp, format));
  } catch (error) {
    appToast.error("Couldn't export the activity", error);
  } finally {
    pending.value = null;
  }
};

const items = computed(() => [
  [
    {
      description: "Opens in a spreadsheet",
      icon: "i-lucide-file-spreadsheet",
      label: "CSV",
      onSelect: () => void run(AuditExportFormat.Csv),
    },
    {
      description: "Same fields the app reads",
      icon: "i-lucide-file-text",
      label: "JSON",
      onSelect: () => void run(AuditExportFormat.Json),
    },
  ],
]);
</script>

<template>
  <UDropdownMenu :items="items" :content="{ align: 'end' }">
    <UButton
      icon="i-lucide-download"
      label="Export"
      color="neutral"
      variant="outline"
      size="sm"
      :loading="pending !== null"
    />
  </UDropdownMenu>

  <AuditExportPartsModal
    v-if="checkout"
    :open="checkout !== null"
    :plan="checkout.plan"
    :format="checkout.format"
    :base-name="baseName"
    :stamp="checkout.stamp"
    :download="download"
    @close="checkout = null"
  />
</template>
