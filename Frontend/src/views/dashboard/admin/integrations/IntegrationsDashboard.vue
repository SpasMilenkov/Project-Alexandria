<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <header
      class="flex shrink-0 flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="mdi:puzzle-outline"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
          Integrations
        </h1>
        <UBadge v-if="!isLoading" :color="overall.color" variant="subtle" size="sm">
          {{ overall.label }}
        </UBadge>
      </div>

      <div class="flex items-center gap-3">
        <span class="hidden text-xs text-gray-600 sm:inline dark:text-gray-400">
          Uptime over the last {{ UPTIME_WINDOW_DAYS }} days
        </span>

        <UTooltip text="Refresh">
          <UButton
            variant="ghost"
            color="neutral"
            size="sm"
            icon="i-lucide-refresh-cw"
            square
            class="justify-center"
            aria-label="Refresh status"
            :loading="isFetching"
            @click="refetch"
          />
        </UTooltip>
      </div>
    </header>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <UAlert
        v-if="visibleError"
        color="error"
        variant="subtle"
        icon="i-lucide-alert-circle"
        title="Couldn't load live status"
        description="The dashboards still open; status and uptime will appear once monitoring responds."
        class="mb-3"
      />

      <div class="grid grid-cols-1 gap-3 md:grid-cols-2 2xl:grid-cols-3">
        <AdminHubCard
          v-for="card in cards"
          :key="card.id"
          :to="card.route"
          :icon="card.icon"
          :title="card.title"
          :description="card.description"
        >
          <div
            class="flex items-center justify-between gap-3 border-t border-gray-200/70 pt-3 dark:border-gray-700/70"
          >
            <template v-if="!card.status">
              <span class="text-sm text-gray-600 dark:text-gray-400">{{ card.note }}</span>
              <UBadge color="neutral" variant="outline" size="sm">Planned</UBadge>
            </template>

            <div v-else-if="isLoading" class="flex w-full justify-center py-1">
              <BlockSpinner :size="16" aria-label="Loading status" class="text-gray-500" />
            </div>

            <template v-else>
              <span class="flex min-w-0 items-center gap-2 text-sm">
                <span class="h-2 w-2 shrink-0 rounded-full" :class="card.status.health.dot" />
                <span class="font-medium" :class="card.status.health.text">
                  {{ card.status.health.label }}
                </span>
                <span
                  v-if="card.status.age"
                  class="truncate text-xs text-gray-600 dark:text-gray-400"
                >
                  for {{ card.status.age }}
                </span>
              </span>

              <span class="shrink-0 text-right">
                <span
                  class="text-sm font-semibold tabular-nums"
                  :class="uptimeClass(card.status.uptime)"
                >
                  {{ formatUptime(card.status.uptime) }}
                </span>
                <span class="ml-1 text-xs text-gray-600 dark:text-gray-400">uptime</span>
              </span>
            </template>
          </div>
        </AdminHubCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import AdminHubCard from "@/components/dashboard/admin/AdminHubCard.vue";
import { formatUptime, uptimeClass, useServiceStatus } from "@/composables/useServiceStatus";
import { ServiceType } from "@/enums";
import { UPTIME_WINDOW_DAYS } from "@/queries/monitoring";
import { formatAgeShort } from "@/utils/date-formatters";

interface Integration {
  id: string;
  title: string;
  description: string;
  icon: string;
  route: string | null;
  // Services whose health backs this card; empty for integrations without monitoring yet.
  services: ServiceType[];
  note?: string;
}

const INTEGRATIONS: Integration[] = [
  {
    description: "Queue depth, failure rates, durations and batch history.",
    icon: "i-lucide-audio-lines",
    id: "audio-analysis",
    route: "/dashboard/admin/integrations/audio-analysis",
    services: [ServiceType.MediaMetadata],
    title: "Audio analysis",
  },
  {
    description: "Queue depth, failure-rate trends and throughput.",
    icon: "mdi:file-swap-outline",
    id: "transpilation",
    route: "/dashboard/admin/integrations/transpilation",
    services: [ServiceType.Transpilation],
    title: "Transpilation",
  },
  {
    description: "Artifact volume and incidents across the media and document workers.",
    icon: "mdi:image-multiple-outline",
    id: "previews",
    route: "/dashboard/admin/integrations/previews",
    services: [ServiceType.MediaPreviews, ServiceType.DocumentPreviews],
    title: "Previews",
  },
  {
    description: "Sync runs, failure-rate trends and throughput.",
    icon: "mdi:playlist-music",
    id: "playlists",
    route: "/dashboard/admin/integrations/playlist",
    services: [ServiceType.Playlist],
    title: "Playlists",
  },
  {
    description: "Fetch outcomes, provider reliability and confidence.",
    icon: "mdi:script-text-outline",
    id: "lyrics",
    route: "/dashboard/admin/integrations/lyrics",
    services: [ServiceType.Lyrics],
    title: "Lyrics",
  },
  {
    description: "Genre and mood tags derived from audio analysis output.",
    icon: "mdi:tag-multiple-outline",
    id: "auto-tagging",
    note: "Runs inside audio analysis",
    route: null,
    services: [],
    title: "Auto-tagging",
  },
];

const MONITORED = INTEGRATIONS.flatMap((integration) => integration.services);

const { isFetching, isLoading, refetch, summarize, visibleError, worstOf } = useServiceStatus();

const overall = computed(() => summarize(MONITORED));

const statusOf = (services: ServiceType[]) => {
  if (services.length === 0) return null;

  const worst = worstOf(services);

  return {
    // Incident age only means something while the service is in a bad state.
    age: worst.incident ? formatAgeShort(worst.incident.createdAt) : null,
    health: worst.health,
    uptime: worst.uptime,
  };
};

const cards = computed(() =>
  INTEGRATIONS.map((integration) => ({ ...integration, status: statusOf(integration.services) })),
);
</script>
