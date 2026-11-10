<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <header
      class="flex shrink-0 flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="i-lucide-shield-check"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
          Administration
        </h1>
        <UBadge v-if="!isLoading" :color="overall.color" variant="subtle" size="sm">
          {{ overall.label }}
        </UBadge>
      </div>

      <time class="text-xs text-gray-600 dark:text-gray-400">
        {{ todayLabel }}
      </time>
    </header>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <div class="flex flex-col gap-6">
        <ServiceHealthStrip />

        <section
          v-for="group in GROUPS"
          :key="group.label"
          :aria-labelledby="`admin-group-${group.id}`"
        >
          <h2
            :id="`admin-group-${group.id}`"
            class="mb-2 inline-flex rounded-full border border-gray-200/70 px-3 py-1 text-xs font-medium uppercase tracking-wider text-gray-600 frosted-glass glass-surface-strong dark:border-gray-700/70 dark:text-gray-400"
          >
            {{ group.label }}
          </h2>

          <div class="grid grid-cols-1 gap-3 md:grid-cols-2 2xl:grid-cols-3">
            <AdminHubCard
              v-for="link in group.links"
              :key="link.route"
              :to="link.route"
              :icon="link.icon"
              :title="link.title"
              :description="link.description"
            >
              <template v-if="link.id === 'integrations' && !isLoading" #meta>
                <UBadge :color="integrationsStatus.color" variant="subtle" size="sm">
                  {{ integrationsStatus.label }}
                </UBadge>
              </template>
            </AdminHubCard>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

import AdminHubCard from "@/components/dashboard/admin/AdminHubCard.vue";
import ServiceHealthStrip from "@/components/dashboard/admin/monitoring/ServiceHealthStrip.vue";
import { useServiceStatus } from "@/composables/useServiceStatus";
import { ServiceType } from "@/enums";
import { MONITORED_SERVICES } from "@/features/monitoring/display";
import { formatFullDate } from "@/utils/date-formatters";

interface HubLink {
  id: string;
  title: string;
  description: string;
  icon: string;
  route: string;
}

const GROUPS: { id: string; label: string; links: HubLink[] }[] = [
  {
    id: "operations",
    label: "Operations",
    links: [
      {
        description: "Live checks for the server, database, storage and broker.",
        icon: "mdi:heart-pulse",
        id: "status",
        route: "/dashboard/admin/service-status",
        title: "System health",
      },
      {
        description: "Daily incident patterns, per-service uptime and the full event log.",
        icon: "mdi:calendar-clock-outline",
        id: "incidents",
        route: "/dashboard/admin/incident-history",
        title: "Incident history",
      },
      {
        description:
          "Dashboards for audio analysis, transpilation, previews, playlists and lyrics.",
        icon: "mdi:puzzle-outline",
        id: "integrations",
        route: "/dashboard/admin/integrations",
        title: "Integrations",
      },
    ],
  },
  {
    id: "people",
    label: "Users and storage",
    links: [
      {
        description: "Accounts, roles, quotas and access.",
        icon: "mdi:account-multiple",
        id: "users",
        route: "/dashboard/admin/user-registry",
        title: "User registry",
      },
      {
        description: "Server-wide usage by artifact type and per-user consumption.",
        icon: "mdi:archive-outline",
        id: "storage",
        route: "/dashboard/admin/storage",
        title: "Storage",
      },
    ],
  },
  {
    id: "configuration",
    label: "Configuration",
    links: [
      {
        description: "Feature flags, capability toggles and system-wide policies.",
        icon: "mdi:tune-vertical",
        id: "configuration",
        route: "/dashboard/admin/system-configuration",
        title: "System configuration",
      },
    ],
  },
];

const INTEGRATION_SERVICES = MONITORED_SERVICES.filter((service) => service !== ServiceType.Api);

// The health strip below polls these queries; the hub only reads the shared cache.
const { isLoading, summarize } = useServiceStatus({ poll: false });

const overall = computed(() => summarize(MONITORED_SERVICES));

const integrationsStatus = computed(() => {
  const status = summarize(INTEGRATION_SERVICES);

  if (status.color === "success") return { ...status, label: "All healthy" };

  return status;
});

const todayLabel = formatFullDate(new Date());
</script>
