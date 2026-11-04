<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { isAuthError } from "@/api/shuffle";
import { addedThisWeekLabel } from "@/features/media/overview";
import { mediaOverviewLibrary } from "@/queries/media-overview";
import { formatDate } from "@/utils/date-formatters";

import MediaOverviewFile from "./MediaOverviewFile.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";
import MediaShelfScroller from "./MediaShelfScroller.vue";

const { userId, session, isVideo } = defineProps<{
  userId: string;
  session: string;
  isVideo: boolean;
}>();

const query = useQuery(() => mediaOverviewLibrary({ userId, session, isVideo }));
const title = computed(() => (isVideo ? "Videos" : "Fresh in your library"));
const destination = computed(() => (isVideo ? "/streaming/videos" : "/streaming/music"));

const description = computed(() => {
  const data = query.data.value;

  if (!data || query.error.value) return "The newest additions, ready to play.";

  const noun = isVideo ? "video" : "track";
  const total = `${data.totalCount.toLocaleString()} ${noun}${data.totalCount === 1 ? "" : "s"}`;
  const added = addedThisWeekLabel(data.items, data.totalCount);

  if (isVideo || !added) return total;

  return `${total} · ${added}`;
});

const chipFor = (file: MediaFileDto) => {
  if (isVideo || !file.createdAt) return null;

  return formatDate(file.createdAt);
};
</script>

<template>
  <MediaOverviewSection
    variant="shelf"
    :title="title"
    :description="description"
    :to="destination"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    :empty-seed="isVideo ? 'empty-videos' : 'empty-tracks'"
    :empty-wide="isVideo"
    :empty-title="isVideo ? 'No videos ready yet' : 'No music ready yet'"
    empty-description="Media appears here once its conversion finishes."
    @retry="query.refetch()"
  >
    <MediaShelfScroller>
      <MediaOverviewFile
        v-for="file in query.data.value?.items"
        :key="file.fileId"
        :file="file"
        :user-id="userId"
        :chip="chipFor(file)"
        class="shrink-0"
        :class="isVideo ? 'w-[clamp(280px,16.5vw,420px)]' : 'w-[clamp(160px,9.2vw,232px)]'"
      />
    </MediaShelfScroller>
  </MediaOverviewSection>
</template>
