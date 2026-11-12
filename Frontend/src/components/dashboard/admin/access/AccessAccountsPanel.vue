<script setup lang="ts">
import type { AccountAccessSummary } from "@/api/adminAudit";

import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import AccessAccountsGhost from "@/components/dashboard/admin/access/AccessAccountsGhost.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import { isLockedOut, sessionDeviceLabels } from "@/features/audit/access";
import { toAuditRouteQuery } from "@/features/audit/query";
import { formatCount } from "@/utils/count";
import { formatDateTime, formatTimeAgo } from "@/utils/date-formatters";

const { accounts, windowDays } = defineProps<{
  accounts: AccountAccessSummary[];
  windowDays: number;
}>();

const MAX_DEVICE_LABELS = 2;

// One template for both layouts: below md each cell carries its own label, above it the
// header row does.
const GRID = "md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_4.5rem_minmax(0,1.5fr)_5.5rem_4.5rem_6rem]";

const logLink = (account: AccountAccessSummary) => ({
  name: "admin-activity-log",
  query: toAuditRouteQuery({ owner: account.userId }),
});

const initial = (account: AccountAccessSummary) => account.name.charAt(0).toUpperCase() || "?";

const lastSignInLabel = (account: AccountAccessSummary) => {
  if (!account.lastSignInAt) return "Never";

  return formatTimeAgo(account.lastSignInAt, { absoluteAfterDays: 60 });
};

const deviceSummary = (account: AccountAccessSummary) => {
  const labels = sessionDeviceLabels(account.sessionUserAgents);
  if (labels.length === 0) return "";

  const shown = labels.slice(0, MAX_DEVICE_LABELS).join(", ");
  const hidden = labels.length - MAX_DEVICE_LABELS;
  if (hidden <= 0) return shown;

  return `${shown} +${hidden}`;
};

const lockedUntil = (account: AccountAccessSummary) => {
  if (!account.lockoutEnd) return undefined;

  return `Locked until ${formatDateTime(account.lockoutEnd)}`;
};

const failedClass = (account: AccountAccessSummary) => {
  if (account.failedSignIns > 0) return "font-semibold text-amber-600 dark:text-amber-400";

  return "text-gray-900 dark:text-gray-100";
};
</script>

<template>
  <IntegrationPanel
    title="Accounts"
    :hint="`Counts cover the last ${windowDays} days · select an account for its log`"
    flush
  >
    <EmptyStatePanel v-if="accounts.length === 0" :panel="false">
      <template #ghost>
        <AccessAccountsGhost />
      </template>

      <GlassEmptyState
        icon="i-lucide-users"
        title="No accounts yet"
        description="Each account gets a row with its sign-ins, live sessions and failed attempts once it exists."
        class="m-auto"
      >
        <UButton
          to="/dashboard/admin/user-registry"
          color="neutral"
          variant="outline"
          class="rounded-full px-5"
        >
          Open user registry
        </UButton>
      </GlassEmptyState>
    </EmptyStatePanel>

    <div v-else>
      <div
        class="hidden gap-4 border-b border-gray-200/70 px-5 py-2 text-xs font-medium text-gray-600 md:grid dark:border-gray-700/70 dark:text-gray-400"
        :class="GRID"
        aria-hidden="true"
      >
        <span>Account</span>
        <span>Last sign-in</span>
        <span class="text-right">Sign-ins</span>
        <span>Live sessions</span>
        <span class="text-right">New networks</span>
        <span class="text-right">Failed</span>
        <span class="text-right">Status</span>
      </div>

      <ul class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
        <li v-for="account in accounts" :key="account.userId">
          <RouterLink
            :to="logLink(account)"
            class="grid grid-cols-2 gap-x-4 gap-y-2 px-4 py-3 transition-colors hover:bg-gray-500/5 focus-visible:bg-gray-500/10 focus-visible:outline-none sm:px-5 md:items-center dark:hover:bg-gray-400/5"
            :class="GRID"
          >
            <div class="col-span-2 flex min-w-0 items-center gap-2.5 md:col-span-1">
              <span
                class="flex size-7 shrink-0 items-center justify-center rounded-full bg-gray-500/10 text-xs font-semibold text-gray-700 dark:bg-gray-400/10 dark:text-gray-300"
                aria-hidden="true"
              >
                {{ initial(account) }}
              </span>

              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                  {{ account.name }}
                </p>
                <p class="truncate text-xs text-gray-600 dark:text-gray-400">
                  {{ account.email }}
                </p>
              </div>

              <UBadge
                v-if="isLockedOut(account)"
                color="error"
                variant="subtle"
                size="sm"
                icon="i-lucide-lock"
                class="shrink-0 md:hidden"
                :title="lockedUntil(account)"
              >
                Locked
              </UBadge>
            </div>

            <div class="flex flex-col md:block">
              <span class="text-xs text-gray-600 md:hidden dark:text-gray-400">Last sign-in</span>
              <span
                class="text-sm text-gray-900 dark:text-gray-100"
                :title="account.lastSignInAt ? formatDateTime(account.lastSignInAt) : undefined"
              >
                {{ lastSignInLabel(account) }}
              </span>
            </div>

            <div class="flex flex-col md:block md:text-right">
              <span class="text-xs text-gray-600 md:hidden dark:text-gray-400">Sign-ins</span>
              <span class="text-sm text-gray-900 tabular-nums dark:text-gray-100">
                {{ account.signIns }}
              </span>
            </div>

            <div class="flex min-w-0 flex-col md:block">
              <span class="text-xs text-gray-600 md:hidden dark:text-gray-400">Live sessions</span>
              <span class="text-sm text-gray-900 tabular-nums dark:text-gray-100">
                {{ formatCount(account.liveSessions, "session") }}
              </span>
              <span
                v-if="deviceSummary(account)"
                class="block truncate text-xs text-gray-600 dark:text-gray-400"
              >
                {{ deviceSummary(account) }}
              </span>
            </div>

            <div class="flex flex-col md:block md:text-right">
              <span class="text-xs text-gray-600 md:hidden dark:text-gray-400">New networks</span>
              <span class="text-sm text-gray-900 tabular-nums dark:text-gray-100">
                {{ account.newNetworks }}
              </span>
            </div>

            <div class="flex flex-col md:block md:text-right">
              <span class="text-xs text-gray-600 md:hidden dark:text-gray-400">Failed</span>
              <span class="text-sm tabular-nums" :class="failedClass(account)">
                {{ account.failedSignIns }}
              </span>
            </div>

            <div class="hidden justify-end md:flex">
              <UBadge
                v-if="isLockedOut(account)"
                color="error"
                variant="subtle"
                size="sm"
                icon="i-lucide-lock"
                :title="lockedUntil(account)"
              >
                Locked
              </UBadge>
              <UBadge v-else color="success" variant="subtle" size="sm">Active</UBadge>
            </div>
          </RouterLink>
        </li>
      </ul>
    </div>
  </IntegrationPanel>
</template>
