<script setup lang="ts">
import { computed } from "vue";

import type {
  FailedSignInReasonCount,
  FailedSignInSource,
  UnknownAccountAttempt,
} from "@/api/adminAudit";

import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import FailedSignInsGhost from "@/components/dashboard/admin/access/FailedSignInsGhost.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import { AuditEventCode } from "@/enums/audit-event-code";
import { WIDEST_ACCESS_WINDOW, failureReasonLabel } from "@/features/audit/access";
import { toAuditRouteQuery } from "@/features/audit/query";
import { formatCount } from "@/utils/count";
import { formatDateTime, formatTimeAgo } from "@/utils/date-formatters";

const { total, reasons, sources, unknownAccounts, windowDays } = defineProps<{
  total: number;
  reasons: FailedSignInReasonCount[];
  sources: FailedSignInSource[];
  unknownAccounts: UnknownAccountAttempt[];
  windowDays: number;
}>();

const emit = defineEmits<{ widen: [] }>();

const canWiden = computed(() => windowDays < WIDEST_ACCESS_WINDOW);

const largestReason = computed(() => Math.max(1, ...reasons.map((reason) => reason.count)));

const barWidth = (count: number) => `${Math.max(4, (count / largestReason.value) * 100)}%`;

const sourceLink = (source: FailedSignInSource) => ({
  name: "admin-activity-log",
  query: toAuditRouteQuery({ events: [AuditEventCode.UserLoginFailed], ip: source.ipAddress }),
});

const sourceCaption = (source: FailedSignInSource) => {
  const attempts = formatCount(source.count, "attempt");
  if (source.accountCount === 0) return attempts;

  return `${attempts} · ${formatCount(source.accountCount, "account")}`;
};
</script>

<template>
  <IntegrationPanel title="Failed sign-ins" :hint="`Last ${windowDays} days`" :flush="total === 0">
    <EmptyStatePanel v-if="total === 0" :panel="false">
      <template #ghost>
        <FailedSignInsGhost />
      </template>

      <GlassEmptyState
        icon="i-lucide-shield-check"
        title="No failed sign-ins"
        :description="`Every sign-in in the last ${windowDays} days went through. Wrong passwords, locked accounts and unknown emails will show up here.`"
        class="m-auto"
      >
        <UButton
          v-if="canWiden"
          color="neutral"
          variant="outline"
          class="rounded-full px-5"
          @click="emit('widen')"
        >
          Look back {{ WIDEST_ACCESS_WINDOW }} days
        </UButton>
      </GlassEmptyState>
    </EmptyStatePanel>

    <div v-else class="flex flex-col gap-5">
      <section aria-labelledby="failed-reasons" class="flex flex-col gap-2.5">
        <h3 id="failed-reasons" class="text-xs font-medium text-gray-600 dark:text-gray-400">
          Why they failed
        </h3>

        <ul class="flex flex-col gap-2.5">
          <li v-for="reason in reasons" :key="reason.reason" class="flex flex-col gap-1">
            <div class="flex items-baseline justify-between gap-3 text-sm">
              <span class="truncate text-gray-900 dark:text-gray-100">
                {{ failureReasonLabel(reason.reason) }}
              </span>
              <span class="shrink-0 font-semibold text-gray-900 tabular-nums dark:text-gray-100">
                {{ reason.count }}
              </span>
            </div>

            <div class="h-1.5 overflow-hidden rounded-full bg-gray-500/10 dark:bg-gray-400/10">
              <div
                class="h-full rounded-full bg-amber-500"
                :style="{ width: barWidth(reason.count) }"
              />
            </div>
          </li>
        </ul>
      </section>

      <section v-if="sources.length" aria-labelledby="failed-sources" class="flex flex-col gap-1.5">
        <h3 id="failed-sources" class="text-xs font-medium text-gray-600 dark:text-gray-400">
          Where they came from
        </h3>

        <ul class="-mx-2 flex flex-col">
          <li v-for="source in sources" :key="source.ipAddress">
            <RouterLink
              :to="sourceLink(source)"
              class="flex items-center gap-3 rounded-lg px-2 py-1.5 transition-colors hover:bg-gray-500/5 focus-visible:bg-gray-500/10 focus-visible:outline-none dark:hover:bg-gray-400/5"
            >
              <UIcon
                name="i-lucide-map-pin"
                class="h-4 w-4 shrink-0 text-gray-600 dark:text-gray-400"
              />

              <div class="min-w-0 flex-1">
                <p class="truncate font-mono text-sm text-gray-900 dark:text-gray-100">
                  {{ source.ipAddress }}
                </p>
                <p class="truncate text-xs text-gray-600 dark:text-gray-400">
                  {{ sourceCaption(source) }}
                </p>
              </div>

              <time
                :datetime="source.lastSeen"
                :title="formatDateTime(source.lastSeen)"
                class="shrink-0 text-xs text-gray-600 dark:text-gray-400"
              >
                {{ formatTimeAgo(source.lastSeen, { style: "narrow" }) }}
              </time>
            </RouterLink>
          </li>
        </ul>
      </section>

      <section
        v-if="unknownAccounts.length"
        aria-labelledby="failed-unknown"
        class="flex flex-col gap-1.5"
      >
        <h3 id="failed-unknown" class="text-xs font-medium text-gray-600 dark:text-gray-400">
          Emails with no account
        </h3>

        <ul class="flex flex-col">
          <li
            v-for="attempt in unknownAccounts"
            :key="attempt.email"
            class="flex items-center gap-3 py-1.5"
          >
            <UIcon name="i-lucide-mail" class="h-4 w-4 shrink-0 text-gray-600 dark:text-gray-400" />

            <div class="min-w-0 flex-1">
              <p class="truncate text-sm text-gray-900 dark:text-gray-100">{{ attempt.email }}</p>
              <p class="text-xs text-gray-600 dark:text-gray-400">
                {{ formatCount(attempt.count, "attempt") }}
              </p>
            </div>

            <time
              :datetime="attempt.lastSeen"
              :title="formatDateTime(attempt.lastSeen)"
              class="shrink-0 text-xs text-gray-600 dark:text-gray-400"
            >
              {{ formatTimeAgo(attempt.lastSeen, { style: "narrow" }) }}
            </time>
          </li>
        </ul>
      </section>
    </div>
  </IntegrationPanel>
</template>
