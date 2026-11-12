<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { AuditLogResult } from "@/api/activity";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { getAuditMetadataEntries, parseAuditMetadata } from "@/composables/useAuditMessage";
import { securitySummary } from "@/queries/activities";
import { useAuthStore } from "@/stores/auth";
import { formatCount } from "@/utils/count";
import { formatDateTime, formatTimeAgo } from "@/utils/date-formatters";
import { describeUserAgent } from "@/utils/user-agent";

const authStore = useAuthStore();

const { data, isPending, error } = useQuery(() => securitySummary(authStore.user?.user.id ?? ""));

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

const deviceOf = (row: AuditLogResult) => {
  const userAgent = parseAuditMetadata(row.metadata)?.user_agent;

  return describeUserAgent(typeof userAgent === "string" ? userAgent : null).label;
};

const isNewIp = (row: AuditLogResult) => parseAuditMetadata(row.metadata)?.new_ip === true;

const failureReason = (row: AuditLogResult) =>
  getAuditMetadataEntries(row.metadata).find((entry) => entry.label === "Reason")?.value ??
  "Sign-in failed";

const failedSummary = computed(() => {
  const count = data.value?.failedAttemptsLast30Days ?? 0;
  if (count === 0) return "No failed attempts in the last 30 days.";

  return `${formatCount(count, "failed attempt")} in the last 30 days.`;
});

const hasAnything = computed(
  () =>
    Boolean(data.value?.recentLogins.length) ||
    Boolean(data.value?.recentFailedAttempts.length) ||
    Boolean(data.value?.knownIpAddresses.length),
);
</script>

<template>
  <section
    class="rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    aria-labelledby="sign-in-history-heading"
  >
    <div class="border-b border-gray-200/70 px-4 py-3 md:px-5 dark:border-gray-700/70">
      <h2
        id="sign-in-history-heading"
        class="text-sm font-semibold text-gray-900 dark:text-gray-100"
      >
        Sign-in history
      </h2>
      <p class="mt-0.5 text-xs text-gray-600 dark:text-gray-400">
        The last 90 days of sign-ins to your account.
      </p>
    </div>

    <div class="px-4 py-3 md:px-5">
      <UAlert
        v-if="visibleError"
        color="error"
        variant="subtle"
        icon="i-lucide-alert-circle"
        title="Couldn't load your sign-in history"
        :description="visibleError.message"
      />

      <div v-else-if="isPending && !data" class="flex h-24 items-center justify-center">
        <BlockSpinner :size="20" aria-label="Loading sign-in history" class="text-gray-500" />
      </div>

      <div v-else-if="data && hasAnything" class="flex flex-col gap-6">
        <div v-if="data.recentLogins.length" class="flex flex-col gap-1">
          <h3
            class="text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
          >
            Recent sign-ins
          </h3>

          <ul class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
            <li
              v-for="login in data.recentLogins"
              :key="login.id"
              class="flex items-center gap-3 py-2.5"
            >
              <UIcon
                name="i-lucide-log-in"
                class="h-4 w-4 shrink-0 text-sky-600 dark:text-sky-400"
              />

              <div class="min-w-0 flex-1">
                <p class="flex items-center gap-2 text-sm text-gray-900 dark:text-gray-100">
                  <span class="truncate">{{ deviceOf(login) }}</span>
                  <UBadge v-if="isNewIp(login)" color="warning" variant="subtle" size="sm">
                    New IP address
                  </UBadge>
                </p>
                <p
                  v-if="login.ipAddress"
                  class="mt-0.5 truncate font-mono text-xs text-gray-600 dark:text-gray-400"
                >
                  {{ login.ipAddress }}
                </p>
              </div>

              <time
                :datetime="login.timestamp"
                :title="formatDateTime(login.timestamp)"
                class="shrink-0 text-xs text-gray-600 dark:text-gray-400"
              >
                {{ formatTimeAgo(login.timestamp) }}
              </time>
            </li>
          </ul>
        </div>

        <div class="flex flex-col gap-1">
          <h3
            class="text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
          >
            Failed attempts
          </h3>
          <p class="text-sm text-gray-600 dark:text-gray-400">{{ failedSummary }}</p>

          <ul
            v-if="data.recentFailedAttempts.length"
            class="divide-y divide-gray-100/50 dark:divide-gray-800/50"
          >
            <li
              v-for="attempt in data.recentFailedAttempts"
              :key="attempt.id"
              class="flex items-center gap-3 py-2.5"
            >
              <UIcon
                name="i-lucide-shield-x"
                class="h-4 w-4 shrink-0 text-rose-600 dark:text-rose-400"
              />

              <div class="min-w-0 flex-1">
                <p class="truncate text-sm text-gray-900 dark:text-gray-100">
                  {{ failureReason(attempt) }}
                </p>
                <p class="mt-0.5 truncate text-xs text-gray-600 dark:text-gray-400">
                  <span v-if="attempt.ipAddress" class="font-mono">{{ attempt.ipAddress }} · </span>
                  <span>{{ deviceOf(attempt) }}</span>
                </p>
              </div>

              <time
                :datetime="attempt.timestamp"
                :title="formatDateTime(attempt.timestamp)"
                class="shrink-0 text-xs text-gray-600 dark:text-gray-400"
              >
                {{ formatTimeAgo(attempt.timestamp) }}
              </time>
            </li>
          </ul>
        </div>

        <div v-if="data.knownIpAddresses.length" class="flex flex-col gap-1">
          <h3
            class="text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
          >
            Addresses you signed in from
          </h3>

          <ul class="divide-y divide-gray-100/50 dark:divide-gray-800/50">
            <li
              v-for="address in data.knownIpAddresses"
              :key="address.ipAddress"
              class="flex items-center gap-3 py-2.5"
            >
              <span
                class="min-w-0 flex-1 truncate font-mono text-sm text-gray-900 dark:text-gray-100"
              >
                {{ address.ipAddress }}
              </span>

              <span class="shrink-0 text-xs text-gray-600 tabular-nums dark:text-gray-400">
                {{ formatCount(address.loginCount, "sign-in") }} · last
                {{ formatTimeAgo(address.lastSeen) }}
              </span>
            </li>
          </ul>
        </div>
      </div>

      <div v-else class="flex flex-col items-center gap-2 py-8 text-center">
        <UIcon name="i-lucide-shield-check" class="h-12 w-12 text-gray-400 dark:text-gray-600" />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No sign-ins recorded</p>
        <p class="text-xs text-gray-600 dark:text-gray-400">
          Sign-ins from the last 90 days will appear here.
        </p>
      </div>
    </div>
  </section>
</template>
