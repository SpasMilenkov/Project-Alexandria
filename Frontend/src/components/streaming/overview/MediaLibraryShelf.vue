<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { isAuthError } from "@/api/shuffle";
import { mediaOverviewLibrary } from "@/queries/media-overview";

import MediaOverviewFile from "./MediaOverviewFile.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session, isVideo } = defineProps<{
  userId: string;
  session: string;
  isVideo: boolean;
}>();

const query = useQuery(() => mediaOverviewLibrary({ userId, session, isVideo }));
const title = computed(() => (isVideo ? "Recently added videos" : "Recently added music"));
const destination = computed(() => (isVideo ? "/streaming/videos" : "/streaming/music"));
const description = computed(() => {
  const count = query.data.value?.totalCount;

  if (count === undefined || query.error.value) return "The newest additions, ready to play.";

  const noun = isVideo ? "video" : "track";

  return `${count.toLocaleString()} ${noun}${count === 1 ? "" : "s"} ready to play`;
});
</script>

<template>
  <MediaOverviewSection
    :title="title"
    :description="description"
    :to="destination"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    :empty-icon="isVideo ? 'i-mdi-film-open-outline' : 'i-mdi-music-note-outline'"
    :empty-title="isVideo ? 'No videos ready yet' : 'No music ready yet'"
    empty-description="Upload media to your files. It appears here when processing finishes."
    @retry="query.refetch()"
  >
    <div
      class="grid grid-cols-2 gap-x-4 gap-y-6"
      :class="isVideo ? 'lg:grid-cols-4' : 'sm:grid-cols-3 xl:grid-cols-6'"
    >
      <MediaOverviewFile
        v-for="file in query.data.value?.items"
        :key="file.fileId"
        :file="file"
        :user-id="userId"
      />
    </div>
  </MediaOverviewSection>
</template>
