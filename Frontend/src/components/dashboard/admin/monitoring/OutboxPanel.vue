<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { OutboxDeadMessage } from "@/api/outbox";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { useResendDeadOutboxMessages } from "@/mutations/outbox";
import { outboxSummary } from "@/queries/outbox";
import { formatCount } from "@/utils/count";
import { formatTimeAgo } from "@/utils/date-formatters";

const { data, error, isPending, refetch } = useQuery(() => outboxSummary());

useLiveRefresh(refetch, 30_000);

const pendingCount = computed(() => data.value?.pendingCount ?? 0);
const deadCount = computed(() => data.value?.deadCount ?? 0);
const recentDead = computed<OutboxDeadMessage[]>(() => data.value?.recentDead ?? []);

const pendingDetail = computed(() => {
  const oldest = data.value?.oldestPendingAt;
  if (!oldest) return "Nothing waiting";
  return `Oldest written ${formatTimeAgo(oldest, { style: "narrow" })}`;
});

const deadDetail = computed(() => {
  if (deadCount.value === 0) return "None refused for good";
  return "Refused too often; resend once the cause is fixed";
});

const deadAge = (message: OutboxDeadMessage): string =>
  formatTimeAgo(message.deadAt, { style: "narrow" });

const errorLabel = (message: OutboxDeadMessage): string => message.lastError ?? "No error recorded";

const toast = useAppToast("monitoring");
const { mutateAsync: resendDead, isLoading: isResending } = useResendDeadOutboxMessages();

const handleResend = async () => {
  try {
    const result = await resendDead();
    if (result.resent === 0) {
      toast.info("No dead messages to resend");
      return;
    }

    toast.success(`Resent ${formatCount(result.resent, "dead message")}`);
  } catch (err) {
    toast.error("Couldn't resend the dead messages", err, { retry: handleResend });
  }
};
</script>

<template>
  <div
    class="rounded-[14px] border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden"
  >
    <div
      class="px-5 py-3 border-b border-gray-200/70 dark:border-gray-700/70 flex items-center gap-2 flex-wrap"
    >
      <UIcon name="i-lucide-send" class="w-4 h-4 text-gray-600 dark:text-gray-400 shrink-0" />
      <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">Message relay</span>
      <span class="text-xs text-gray-600 dark:text-gray-400">
        Work saved for the workers and not yet confirmed by the message broker
      </span>

      <UButton
        v-if="deadCount > 0"
        size="xs"
        color="neutral"
        variant="outline"
        icon="i-lucide-send"
        class="ml-auto"
        :loading="isResending"
        @click="handleResend()"
      >
        Resend dead
      </UButton>
    </div>

    <div v-if="isPending" class="flex items-center justify-center py-14">
      <BlockSpinner :size="24" aria-label="Loading the message relay" class="text-gray-500" />
    </div>

    <UAlert
      v-else-if="error && !data"
      color="error"
      variant="subtle"
      icon="i-lucide-circle-alert"
      title="Failed to load the message relay"
      :description="error.message"
      class="m-4"
    />

    <template v-else>
      <div
        class="grid grid-cols-1 sm:grid-cols-2 divide-y sm:divide-y-0 sm:divide-x divide-gray-100/50 dark:divide-gray-800/50 border-b border-gray-200/70 dark:border-gray-700/70"
      >
        <div class="px-5 py-4">
          <p class="text-xs text-gray-600 dark:text-gray-400">Waiting to send</p>
          <p class="mt-1 text-2xl font-semibold tabular-nums text-gray-900 dark:text-gray-100">
            {{ pendingCount }}
          </p>
          <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">{{ pendingDetail }}</p>
        </div>

        <div class="px-5 py-4">
          <p class="text-xs text-gray-600 dark:text-gray-400">Dead</p>
          <p
            class="mt-1 text-2xl font-semibold tabular-nums"
            :class="deadCount > 0 ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-gray-100'"
          >
            {{ deadCount }}
          </p>
          <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">{{ deadDetail }}</p>
        </div>
      </div>

      <div v-if="recentDead.length === 0" class="flex flex-col items-center text-center px-6 py-10">
        <UIcon name="i-lucide-circle-check" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
        <p class="mt-3 text-sm font-medium text-gray-900 dark:text-gray-100">No dead messages</p>
        <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">
          Messages the broker keeps refusing appear here so they can be resent.
        </p>
      </div>

      <div v-else class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
        <div v-for="message in recentDead" :key="message.id" class="flex items-center gap-3 px-5 py-3">
          <span class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 bg-red-500/10">
            <UIcon name="i-lucide-circle-alert" class="w-4 h-4 text-red-500" />
          </span>

          <div class="min-w-0 flex-1">
            <p class="text-sm text-gray-900 dark:text-gray-100 truncate font-mono">
              {{ message.routingKey }}
            </p>
            <p
              class="text-xs text-gray-600 dark:text-gray-400 mt-0.5 truncate"
              :title="errorLabel(message)"
            >
              {{ errorLabel(message) }}
            </p>
          </div>

          <span class="text-xs text-gray-500 dark:text-gray-500 shrink-0 tabular-nums text-right">
            {{ deadAge(message) }}
            <br />
            {{ formatCount(message.attempts, "attempt") }}
          </span>
        </div>
      </div>
    </template>
  </div>
</template>
