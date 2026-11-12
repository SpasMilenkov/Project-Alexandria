<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import AccessAccountsPanel from "@/components/dashboard/admin/access/AccessAccountsPanel.vue";
import AccessNetworksPanel from "@/components/dashboard/admin/access/AccessNetworksPanel.vue";
import AccessViewTabs from "@/components/dashboard/admin/access/AccessViewTabs.vue";
import FailedSignInsPanel from "@/components/dashboard/admin/access/FailedSignInsPanel.vue";
import SegmentedControl from "@/components/dashboard/integrations/SegmentedControl.vue";
import StatTile from "@/components/dashboard/integrations/StatTile.vue";
import {
  ACCESS_WINDOW_OPTIONS,
  type AccessWindowDays,
  DEFAULT_ACCESS_WINDOW,
  WIDEST_ACCESS_WINDOW,
  failedTrendCaption,
} from "@/features/audit/access";
import { adminAccessOverview } from "@/queries/adminAudit";
import { formatCount } from "@/utils/count";

const windowDays = ref<AccessWindowDays>(DEFAULT_ACCESS_WINDOW);

const { data, isPending, error, refetch } = useQuery(() => adminAccessOverview(windowDays.value));

const visibleError = computed(() => {
  if (!error.value || isAuthError(error.value)) return null;

  return error.value;
});

const sessionsCaption = computed(() => {
  if (!data.value) return "";

  const holders = data.value.accounts.filter((account) => account.liveSessions > 0).length;

  return `across ${formatCount(holders, "account")}`;
});

const failedCaption = computed(() => {
  if (!data.value) return "";

  return failedTrendCaption(
    data.value.failedSignIns,
    data.value.failedSignInsPrevious,
    data.value.windowDays,
  );
});

const failedTone = computed(() => {
  if (!data.value) return "default";
  if (data.value.failedSignIns > data.value.failedSignInsPrevious) return "warning";

  return "default";
});

const lockedTone = computed(() => {
  if (data.value && data.value.lockedAccounts > 0) return "danger";

  return "default";
});
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <header
      class="flex shrink-0 flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="i-lucide-shield-check"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
          Access & security
        </h1>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <SegmentedControl
          v-model="windowDays"
          :options="ACCESS_WINDOW_OPTIONS"
          label="Time window"
        />
        <AccessViewTabs />
      </div>
    </header>

    <div v-if="isPending && !data" class="flex justify-center py-24">
      <BlockSpinner :size="24" aria-label="Loading access overview" class="text-gray-500" />
    </div>

    <div v-else-if="visibleError || !data" class="flex py-16">
      <GlassEmptyState
        icon="i-lucide-triangle-alert"
        title="Couldn't load the access overview"
        :description="
          visibleError?.message ?? 'Something went wrong while loading it. Try again in a moment.'
        "
        class="m-auto"
      >
        <UButton color="neutral" variant="outline" class="rounded-full px-5" @click="refetch()">
          Try again
        </UButton>
      </GlassEmptyState>
    </div>

    <div v-else class="flex flex-col gap-3">
      <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatTile
          label="Active accounts"
          icon="i-lucide-user-check"
          :caption="`of ${formatCount(data.totalAccounts, 'account')} signed in`"
        >
          {{ data.activeAccounts }}
        </StatTile>

        <StatTile label="Live sessions" icon="i-lucide-monitor" :caption="sessionsCaption">
          {{ data.liveSessions }}
        </StatTile>

        <StatTile
          label="Failed sign-ins"
          icon="i-lucide-shield-x"
          :caption="failedCaption"
          :tone="failedTone"
        >
          {{ data.failedSignIns }}
        </StatTile>

        <StatTile
          label="Locked accounts"
          icon="i-lucide-lock"
          caption="Lockouts still in effect"
          :tone="lockedTone"
        >
          {{ data.lockedAccounts }}
        </StatTile>
      </div>

      <AccessAccountsPanel :accounts="data.accounts" :window-days="data.windowDays" />

      <div class="grid grid-cols-1 items-start gap-3 xl:grid-cols-2">
        <FailedSignInsPanel
          :total="data.failedSignIns"
          :reasons="data.failedByReason"
          :sources="data.failedSources"
          :unknown-accounts="data.unknownAccountAttempts"
          :window-days="data.windowDays"
          @widen="windowDays = WIDEST_ACCESS_WINDOW"
        />

        <AccessNetworksPanel
          :networks="data.networks"
          :window-days="data.windowDays"
          @widen="windowDays = WIDEST_ACCESS_WINDOW"
        />
      </div>
    </div>
  </div>
</template>
