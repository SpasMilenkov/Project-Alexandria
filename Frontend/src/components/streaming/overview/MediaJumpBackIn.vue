<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { isAuthError } from "@/api/shuffle";
import { mediaOverviewJumpBackIn } from "@/queries/media-overview";
import { formatDate } from "@/utils/date-formatters";

import MediaOverviewFile from "./MediaOverviewFile.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();
const query = useQuery(() => mediaOverviewJumpBackIn({ userId, session }));
const inactiveDays = computed(() => query.data.value?.inactiveDays ?? 30);
</script>

<template>
  <MediaOverviewSection
    title="Jump back in"
    :description="`Familiar songs you haven't played in ${inactiveDays} days.`"
    to="/streaming/music"
    link-label="Browse music"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    empty-icon="i-mdi-music-note-outline"
    empty-title="Nothing to rediscover yet"
    :empty-description="`Songs you've played at least twice appear after ${inactiveDays} days away.`"
    @retry="query.refetch()"
  >
    <div class="grid grid-cols-2 gap-x-4 gap-y-6 sm:grid-cols-3 xl:grid-cols-6">
      <div v-for="song in query.data.value?.items" :key="song.file.fileId" class="min-w-0">
        <MediaOverviewFile :file="song.file" :user-id="userId" playback="single" class="w-full" />

        <p class="mt-2 text-xs text-gray-500 dark:text-gray-500">
          Last played <time :datetime="song.lastPlayedAt">{{ formatDate(song.lastPlayedAt) }}</time>
        </p>
      </div>
    </div>
  </MediaOverviewSection>
</template>
