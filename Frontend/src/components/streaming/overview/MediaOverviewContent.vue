<script setup lang="ts">
import { useQueryCache } from "@pinia/colada";
import { onUnmounted } from "vue";

import { mediaOverviewKey } from "@/queries/media-overview";

import MediaJumpBackIn from "./MediaJumpBackIn.vue";
import MediaLibraryShelf from "./MediaLibraryShelf.vue";
import MediaOnRepeat from "./MediaOnRepeat.vue";
import MediaPlaylistShelf from "./MediaPlaylistShelf.vue";
import MediaRecentHistory from "./MediaRecentHistory.vue";

const { userId } = defineProps<{ userId: string }>();
const session = crypto.randomUUID();
const cache = useQueryCache();

onUnmounted(() => {
  const key = mediaOverviewKey({ userId, session });
  cache.cancelQueries({ key });

  for (const entry of cache.getEntries({ key })) cache.remove(entry);
});
</script>

<template>
  <div class="space-y-6">
    <MediaOnRepeat :user-id="userId" :session="session" />

    <MediaJumpBackIn :user-id="userId" :session="session" />

    <MediaLibraryShelf :user-id="userId" :session="session" :is-video="false" />

    <MediaLibraryShelf :user-id="userId" :session="session" :is-video="true" />

    <MediaPlaylistShelf :user-id="userId" :session="session" :automatic="true" />

    <div class="grid gap-6 xl:grid-cols-2">
      <MediaPlaylistShelf :user-id="userId" :session="session" :automatic="false" />

      <MediaRecentHistory :user-id="userId" :session="session" />
    </div>
  </div>
</template>
