<script setup lang="ts">
import { computed } from "vue";

import type { AccessNetwork } from "@/api/adminAudit";

import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import AccessNetworksGhost from "@/components/dashboard/admin/access/AccessNetworksGhost.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import { AuditEventCode } from "@/enums/audit-event-code";
import { WIDEST_ACCESS_WINDOW, isWithinDays } from "@/features/audit/access";
import { toAuditRouteQuery } from "@/features/audit/query";
import { formatCount } from "@/utils/count";
import { formatCalendarDate, formatDateTime, formatTimeAgo } from "@/utils/date-formatters";

const { networks, windowDays } = defineProps<{
  networks: AccessNetwork[];
  windowDays: number;
}>();

const emit = defineEmits<{ widen: [] }>();

const canWiden = computed(() => windowDays < WIDEST_ACCESS_WINDOW);

const networkLink = (network: AccessNetwork) => ({
  name: "admin-activity-log",
  query: toAuditRouteQuery({ events: [AuditEventCode.UserLogin], ip: network.ipAddress }),
});

const networkCaption = (network: AccessNetwork) =>
  `${formatCount(network.signIns, "sign-in")} · ${formatCount(network.accountCount, "account")}`;
</script>

<template>
  <IntegrationPanel
    title="Networks"
    :hint="`Addresses signed in from in the last ${windowDays} days`"
    :flush="networks.length === 0"
  >
    <EmptyStatePanel v-if="networks.length === 0" :panel="false">
      <template #ghost>
        <AccessNetworksGhost />
      </template>

      <GlassEmptyState
        icon="i-lucide-map-pin"
        title="No sign-ins yet"
        :description="`Nobody signed in during the last ${windowDays} days. Each address an account signs in from will be listed here, with new ones marked.`"
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

    <ul v-else class="-mx-2 flex flex-col">
      <li v-for="network in networks" :key="network.ipAddress">
        <RouterLink
          :to="networkLink(network)"
          class="flex items-center gap-3 rounded-lg px-2 py-1.5 transition-colors hover:bg-gray-500/5 focus-visible:bg-gray-500/10 focus-visible:outline-none dark:hover:bg-gray-400/5"
        >
          <UIcon
            name="i-lucide-map-pin"
            class="h-4 w-4 shrink-0 text-gray-600 dark:text-gray-400"
          />

          <div class="min-w-0 flex-1">
            <p class="flex items-center gap-2">
              <span class="truncate font-mono text-sm text-gray-900 dark:text-gray-100">
                {{ network.ipAddress }}
              </span>
              <UBadge
                v-if="isWithinDays(network.firstSeen, windowDays)"
                color="info"
                variant="subtle"
                size="sm"
                class="shrink-0"
              >
                New
              </UBadge>
            </p>
            <p class="truncate text-xs text-gray-600 dark:text-gray-400">
              {{ networkCaption(network) }} · first seen
              {{ formatCalendarDate(network.firstSeen) }}
            </p>
          </div>

          <time
            :datetime="network.lastSeen"
            :title="formatDateTime(network.lastSeen)"
            class="shrink-0 text-xs text-gray-600 dark:text-gray-400"
          >
            {{ formatTimeAgo(network.lastSeen, { style: "narrow" }) }}
          </time>
        </RouterLink>
      </li>
    </ul>
  </IntegrationPanel>
</template>
