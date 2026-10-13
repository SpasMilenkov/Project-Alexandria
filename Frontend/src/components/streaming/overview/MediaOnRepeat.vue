<script setup lang="ts">
import { useQuery } from "@pinia/colada";

import { isAuthError } from "@/api/shuffle";
import { mediaOverviewOnRepeat } from "@/queries/media-overview";
import { homeTopSongPlays, homeTopSongsPeriod } from "@/utils/home-top-songs";

import MediaOverviewFile from "./MediaOverviewFile.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();
const period = { ...homeTopSongsPeriod(), limit: 6 };
const query = useQuery(() => mediaOverviewOnRepeat({ userId, session, ...period }));
</script>

<template>
  <MediaOverviewSection
    title="On repeat"
    description="Your most-played songs over the last 7 days"
    to="/stats"
    link-label="Your listening"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    empty-icon="i-mdi-music-note-outline"
    empty-title="Your favorites will find their place"
    empty-description="Songs you play over the week will appear here."
    @retry="query.refetch()"
  >
    <div class="grid grid-cols-2 gap-x-4 gap-y-6 sm:grid-cols-3 xl:grid-cols-6">
      <div v-for="song in query.data.value?.items" :key="song.file.fileId" class="min-w-0">
        <MediaOverviewFile :file="song.file" :user-id="userId" playback="single" class="w-full" />
        <p class="mt-2 text-xs text-gray-500 dark:text-gray-500">{{ homeTopSongPlays(song) }}</p>
      </div>
    </div>
  </MediaOverviewSection>
</template>
