<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";
import { RouterLink } from "vue-router";

import type { StreamHistoryResponse } from "@/api/streaming";

import { isAuthError } from "@/api/shuffle";
import { coverSeed } from "@/features/media/generated-cover";
import { groupByDay } from "@/features/media/overview";
import { mediaOverviewHistory } from "@/queries/media-overview";
import { formatClockTime, formatPlaybackTime } from "@/utils/date-formatters";

import MediaArtwork from "./MediaArtwork.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();
const query = useQuery(() => mediaOverviewHistory({ userId, session }));
const groups = computed(() => groupByDay(query.data.value?.items ?? []));

const subtitle = (entry: StreamHistoryResponse) => {
  if (!entry.isVideo) return entry.artist || entry.album || "Unknown artist";
  if (entry.hasFinished || entry.positionSeconds <= 0) return "Video";

  return `Video · stopped at ${formatPlaybackTime(entry.positionSeconds)}`;
};
</script>

<template>
  <MediaOverviewSection
    title="Recently played"
    to="/streaming/history"
    link-label="History"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    empty-seed="empty-history"
    empty-title="Nothing played yet"
    empty-description="Everything you listen to or watch appears here, newest first."
    @retry="query.refetch()"
  >
    <div class="-mx-2 flex flex-col gap-2">
      <div v-for="group in groups" :key="group.label">
        <h3
          class="px-2 pb-1 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-500"
        >
          {{ group.label }}
        </h3>

        <ul>
          <li v-for="entry in group.items" :key="entry.id">
            <RouterLink
              to="/streaming/history"
              class="flex min-h-14 items-center gap-3 rounded-xl px-2 py-1.5 transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary"
            >
              <MediaArtwork
                :file-id="entry.fileId"
                :seed="
                  coverSeed({ fileId: entry.fileId, artist: entry.artist, album: entry.album })
                "
                class="h-10 shrink-0 rounded-lg"
                :class="entry.isVideo ? 'w-16' : 'w-10'"
                aria-hidden="true"
              />

              <span class="min-w-0 flex-1">
                <span
                  class="block truncate text-sm font-semibold text-gray-900 dark:text-gray-100"
                  :title="entry.title"
                  >{{ entry.title }}</span
                >

                <span class="mt-0.5 block truncate text-xs text-gray-600 dark:text-gray-400">{{
                  subtitle(entry)
                }}</span>
              </span>

              <time
                :datetime="entry.lastAccessedAt"
                class="shrink-0 text-xs tabular-nums text-gray-600 dark:text-gray-400"
                >{{ formatClockTime(entry.lastAccessedAt) }}</time
              >
            </RouterLink>
          </li>
        </ul>
      </div>
    </div>
  </MediaOverviewSection>
</template>
