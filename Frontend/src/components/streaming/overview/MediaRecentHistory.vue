<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { RouterLink } from "vue-router";

import { isAuthError } from "@/api/shuffle";
import { mediaOverviewHistory } from "@/queries/media-overview";
import { formatDate } from "@/utils/date-formatters";

import MediaHistoryArtwork from "./MediaHistoryArtwork.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();
const query = useQuery(() => mediaOverviewHistory({ userId, session }));
</script>

<template>
  <MediaOverviewSection
    title="Recently played"
    description="Your latest listening and watching history."
    to="/streaming/history"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    empty-icon="i-mdi-history"
    empty-title="Start your playback history"
    empty-description="The music and videos you play will appear here."
    @retry="query.refetch()"
  >
    <ul class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
      <li v-for="entry in query.data.value?.items" :key="entry.id">
        <RouterLink
          to="/streaming/history"
          class="flex min-h-16 items-center justify-between gap-4 rounded-lg py-4 focus-visible:outline-2 focus-visible:outline-primary"
        >
          <MediaHistoryArtwork :file-id="entry.fileId" />

          <span
            class="min-w-0 flex-1 truncate text-sm font-medium text-gray-900 dark:text-gray-100"
            :title="entry.title"
            >{{ entry.title }}</span
          >
          <time
            :datetime="entry.lastAccessedAt"
            class="shrink-0 text-xs text-gray-500 dark:text-gray-500"
            >{{ formatDate(entry.lastAccessedAt) }}</time
          >
        </RouterLink>
      </li>
    </ul>
  </MediaOverviewSection>
</template>
