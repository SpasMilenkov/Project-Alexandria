<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useElementSize } from "@vueuse/core";
import { computed, ref, useTemplateRef } from "vue";

import type { AuditLogResult } from "@/api/activity";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import { getAuditMetadataEntries, parseAuditMetadata } from "@/composables/useAuditMessage";
import { securitySummary } from "@/queries/activities";
import { useAuthStore } from "@/stores/auth";
import { formatCount } from "@/utils/count";
import { formatDateTime, formatTimeAgo } from "@/utils/date-formatters";
import { describeUserAgent } from "@/utils/user-agent";

import SignInHistoryGhost from "./SignInHistoryGhost.vue";

const { fill = false } = defineProps<{
  // The page gives the card a fixed height, so the sign-in list sizes its pages to fit it.
  fill?: boolean;
}>();

const DEFAULT_PAGE_SIZE = 5;
const MIN_FILL_PAGE_SIZE = 3;

// Two text lines, the row padding and the divider; replaced by the real height once a row renders.
const FALLBACK_ROW_HEIGHT = 59;

const authStore = useAuthStore();
const loginsPage = ref(1);

const loginsViewport = useTemplateRef<HTMLElement>("loginsViewport");
const loginRows = useTemplateRef<HTMLElement[]>("loginRows");

const { height: viewportHeight } = useElementSize(loginsViewport);
const { height: measuredRowHeight } = useElementSize(() => loginRows.value?.[0], undefined, {
  box: "border-box",
});

const { data, isPending, error, refetch } = useQuery(() =>
  securitySummary(authStore.user?.user.id ?? ""),
);

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

const recentLogins = computed(() => data.value?.recentLogins ?? []);

const loginsPageSize = computed(() => {
  if (!fill || viewportHeight.value === 0) return DEFAULT_PAGE_SIZE;

  // The first row has no divider above it, so one pixel is added back for the rows after it.
  const rowHeight = measuredRowHeight.value ? measuredRowHeight.value + 1 : FALLBACK_ROW_HEIGHT;

  return Math.max(MIN_FILL_PAGE_SIZE, Math.floor(viewportHeight.value / rowHeight));
});

// A refetch or a resize can leave fewer pages than before, so the page is clamped instead of trusted.
const currentLoginsPage = computed(() => {
  const lastPage = Math.max(1, Math.ceil(recentLogins.value.length / loginsPageSize.value));

  return Math.min(loginsPage.value, lastPage);
});

const visibleLogins = computed(() => {
  const start = (currentLoginsPage.value - 1) * loginsPageSize.value;

  return recentLogins.value.slice(start, start + loginsPageSize.value);
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
    class="flex flex-col overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
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

    <div v-if="visibleError" class="flex px-4 py-8 md:px-5">
      <GlassEmptyState
        icon="i-lucide-triangle-alert"
        title="Couldn't load your sign-in history"
        :description="visibleError.message"
        class="m-auto"
      >
        <UButton color="neutral" variant="outline" class="rounded-full px-5" @click="refetch()">
          Try again
        </UButton>
      </GlassEmptyState>
    </div>

    <div v-else-if="isPending && !data" class="flex h-24 items-center justify-center">
      <BlockSpinner :size="20" aria-label="Loading sign-in history" class="text-gray-500" />
    </div>

    <EmptyStatePanel v-else-if="!data || !hasAnything" :panel="false">
      <template #ghost>
        <SignInHistoryGhost />
      </template>

      <GlassEmptyState
        icon="i-lucide-shield-check"
        title="No sign-ins recorded yet"
        description="Sign-ins from the last 90 days appear here, along with failed attempts and the addresses you signed in from."
        class="m-auto"
      />
    </EmptyStatePanel>

    <div v-else class="flex min-h-0 flex-1 flex-col overflow-y-auto px-4 py-3 md:px-5">
      <div class="flex min-h-0 flex-1 flex-col gap-6">
        <div
          v-if="data.recentLogins.length"
          class="flex flex-col gap-1"
          :class="{ 'min-h-0 flex-1': fill }"
        >
          <h3
            class="text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
          >
            Recent sign-ins
          </h3>

          <!-- Filling, the list sits absolutely so the rows it renders never feed back into its size. -->
          <div ref="loginsViewport" :class="{ 'relative min-h-44 flex-1': fill }">
            <ul
              class="divide-y divide-gray-100/50 dark:divide-gray-800/50"
              :class="{ 'absolute inset-0 overflow-y-auto': fill }"
            >
              <li
                v-for="login in visibleLogins"
                ref="loginRows"
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

          <div v-if="recentLogins.length > loginsPageSize" class="flex justify-center pt-2">
            <UPagination
              :page="currentLoginsPage"
              :total="recentLogins.length"
              :items-per-page="loginsPageSize"
              :sibling-count="1"
              size="sm"
              @update:page="loginsPage = $event"
            />
          </div>
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
    </div>
  </section>
</template>
