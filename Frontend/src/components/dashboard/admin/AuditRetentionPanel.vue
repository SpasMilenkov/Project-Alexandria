<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import { useAppToast } from "@/composables/useAppToast";
import { updateAuditRetention } from "@/mutations/adminSettings";
import { auditRetention } from "@/queries/adminSettings";
import { formatCount } from "@/utils/count";
import { formatDateTime, formatTimeAgo } from "@/utils/date-formatters";

const appToast = useAppToast("general");

const { data, isPending, error } = useQuery(auditRetention());

const { mutateAsync: save, isLoading: isSaving } = updateAuditRetention();

const days = ref<number | null>(null);

// The field always mirrors what the server last confirmed, never the value sent.
watch(
  () => data.value?.retentionDays,
  (value) => {
    if (value !== undefined) days.value = value;
  },
  { immediate: true },
);

const limits = computed(() => ({
  max: data.value?.maxDays ?? 3650,
  min: data.value?.minDays ?? 30,
}));

const isInRange = (value: number) => value >= limits.value.min && value <= limits.value.max;

const persist = useDebounceFn(async () => {
  const value = days.value;
  if (value === null || !isInRange(value) || value === data.value?.retentionDays) return;

  try {
    await save(value);
  } catch (err) {
    days.value = data.value?.retentionDays ?? null;
    appToast.error("Couldn't change how long activity is kept", err);
  }
}, 600);

const rangeError = computed(() => {
  if (days.value === null || isInRange(days.value)) return undefined;

  return `Choose between ${limits.value.min} and ${limits.value.max} days.`;
});

const keptFor = computed(() => {
  const value = data.value?.retentionDays;
  if (!value) return "";
  if (value % 365 === 0) return formatCount(value / 365, "year");

  return formatCount(value, "day");
});

const lastPurge = computed(() => {
  const at = data.value?.lastPurgeAt;
  if (!at) return "The daily cleanup has not removed anything yet.";

  const count = data.value?.lastPurgeDeletedCount;
  const removed = count ? `removed ${formatCount(count, "row")}` : "ran";

  return `Last cleanup ${removed} ${formatTimeAgo(at)}.`;
});
</script>

<template>
  <IntegrationPanel title="Activity history" hint="Cleaned up once a day">
    <template #actions>
      <span
        v-if="isSaving"
        class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400"
      >
        <BlockSpinner :size="12" aria-label="Saving" />
        Saving…
      </span>
    </template>

    <div v-if="isPending && !data" class="flex justify-center py-6">
      <BlockSpinner :size="24" aria-label="Loading retention" class="text-gray-500" />
    </div>

    <UAlert
      v-else-if="error"
      color="error"
      variant="subtle"
      icon="i-lucide-alert-circle"
      title="Couldn't load the retention setting"
      :description="error.message"
    />

    <div v-else class="flex flex-col gap-4">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between sm:gap-6">
        <UFormField
          label="Keep activity for"
          :description="`Records older than this are removed. Currently ${keptFor}.`"
          :error="rangeError"
          class="flex-1"
        />

        <div class="flex shrink-0 items-center gap-2">
          <UInputNumber
            v-model="days"
            :min="limits.min"
            :max="limits.max"
            :step="30"
            aria-label="Days to keep activity"
            class="w-36"
            @update:model-value="persist"
          />
          <span class="text-sm text-gray-600 dark:text-gray-400">days</span>
        </div>
      </div>

      <p
        class="border-t border-gray-200/70 pt-4 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
        :title="data?.lastPurgeAt ? formatDateTime(data.lastPurgeAt) : undefined"
      >
        {{ lastPurge }}
      </p>
    </div>
  </IntegrationPanel>
</template>
