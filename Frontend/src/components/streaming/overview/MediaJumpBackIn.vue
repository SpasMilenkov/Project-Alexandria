<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { isAuthError } from "@/api/shuffle";
import { daysAwayLabel } from "@/features/media/overview";
import { mediaOverviewJumpBackIn } from "@/queries/media-overview";

import MediaOverviewFile from "./MediaOverviewFile.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";
import MediaShelfScroller from "./MediaShelfScroller.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();
const query = useQuery(() => mediaOverviewJumpBackIn({ userId, session }));
const inactiveDays = computed(() => query.data.value?.inactiveDays ?? 30);
</script>

<template>
  <MediaOverviewSection
    variant="shelf"
    title="Jump back in"
    :description="`Songs you loved, unheard for ${inactiveDays}+ days`"
    to="/streaming/music"
    link-label="Browse music"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    empty-seed="empty-jump-back-in"
    empty-title="Nothing to rediscover yet"
    :empty-description="`Songs you've played at least twice return here after ${inactiveDays} days away.`"
    @retry="query.refetch()"
  >
    <MediaShelfScroller>
      <MediaOverviewFile
        v-for="song in query.data.value?.items"
        :key="song.file.fileId"
        :file="song.file"
        :user-id="userId"
        playback="single"
        :chip="daysAwayLabel(song.lastPlayedAt)"
        class="w-[clamp(160px,9.2vw,232px)] shrink-0"
      />
    </MediaShelfScroller>
  </MediaOverviewSection>
</template>
