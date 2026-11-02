<script setup lang="ts">
import { useQuery, useQueryCache } from "@pinia/colada";
import { computed, onUnmounted } from "vue";

import MediaSearchBar from "@/components/streaming/MediaSearchBar.vue";
import { mediaOverviewKey, mediaOverviewLibrary } from "@/queries/media-overview";

import MediaHero from "./MediaHero.vue";
import MediaJumpBackIn from "./MediaJumpBackIn.vue";
import MediaLibraryShelf from "./MediaLibraryShelf.vue";
import MediaOnRepeat from "./MediaOnRepeat.vue";
import MediaPlaylistShelf from "./MediaPlaylistShelf.vue";
import MediaRecentHistory from "./MediaRecentHistory.vue";
import MediaShelfPreviews from "./MediaShelfPreviews.vue";
import MediaWeekPanel from "./MediaWeekPanel.vue";
import MediaWelcomeHero from "./MediaWelcomeHero.vue";

const { userId } = defineProps<{ userId: string }>();
const session = crypto.randomUUID();
const cache = useQueryCache();

const music = useQuery(() => mediaOverviewLibrary({ userId, session, isVideo: false }));
const videos = useQuery(() => mediaOverviewLibrary({ userId, session, isVideo: true }));

// Only a confirmed empty library swaps in the welcome; loading or failed sections keep their own states.
const libraryEmpty = computed(
  () => music.data.value?.totalCount === 0 && videos.data.value?.totalCount === 0,
);

onUnmounted(() => {
  const key = mediaOverviewKey({ userId, session });
  cache.cancelQueries({ key });

  for (const entry of cache.getEntries({ key })) cache.remove(entry);
});
</script>

<template>
  <div class="flex flex-col gap-6">
    <!-- Raised so the results dropdown paints over the glass cards below. -->
    <div class="relative z-20 flex flex-col gap-3 sm:flex-row sm:items-center">
      <h1 id="media-title" class="sr-only">Media</h1>

      <MediaSearchBar media-type="audio" size="lg" class="min-w-0 sm:flex-1" />

      <UFieldGroup class="w-full sm:w-auto" aria-label="Browse the library">
        <UButton
          to="/streaming/music"
          color="neutral"
          variant="outline"
          icon="i-mdi-music-note-outline"
          class="frosted-glass glass-surface-strong h-11 flex-1 justify-center rounded-s-full px-5 sm:flex-none"
          >Music</UButton
        >

        <UButton
          to="/streaming/videos"
          color="neutral"
          variant="outline"
          icon="i-mdi-video-outline"
          class="frosted-glass glass-surface-strong h-11 flex-1 justify-center rounded-e-full px-5 sm:flex-none"
          >Videos</UButton
        >
      </UFieldGroup>
    </div>

    <div class="@container">
      <div
        class="flex flex-col gap-6 @[76rem]:grid @[76rem]:grid-cols-[minmax(0,1fr)_clamp(340px,28%,520px)] @[76rem]:grid-rows-[auto_1fr]"
      >
        <!-- Both columns are subgrids so the hero and the week panel share a row and its height. -->
        <div
          class="flex min-w-0 flex-col gap-6 @[76rem]:row-span-2 @[76rem]:grid @[76rem]:grid-rows-subgrid"
        >
          <template v-if="libraryEmpty">
            <MediaWelcomeHero />

            <MediaShelfPreviews />
          </template>

          <template v-else>
            <MediaHero :user-id="userId" :session="session" />

            <div
              class="frosted-glass glass-surface flex min-w-0 flex-1 flex-col gap-8 rounded-3xl border border-gray-200/70 py-5 pl-4 sm:py-6 sm:pl-6 @[76rem]:only:row-span-2 dark:border-gray-700/70"
            >
              <MediaLibraryShelf :user-id="userId" :session="session" :is-video="false" />

              <MediaJumpBackIn :user-id="userId" :session="session" />

              <MediaPlaylistShelf :user-id="userId" :session="session" :automatic="true" />

              <MediaLibraryShelf :user-id="userId" :session="session" :is-video="true" />
            </div>
          </template>
        </div>

        <aside
          aria-label="Your listening"
          class="grid min-w-0 grid-cols-[repeat(auto-fill,minmax(min(340px,100%),1fr))] gap-6 *:min-w-0 @[76rem]:row-span-2 @[76rem]:grid-cols-1 @[76rem]:grid-rows-subgrid"
        >
          <MediaWeekPanel :user-id="userId" :session="session" />

          <div
            class="contents *:min-w-0 @[76rem]:flex @[76rem]:flex-col @[76rem]:gap-6 @[76rem]:[&>*:last-child]:flex-1"
          >
            <MediaOnRepeat :user-id="userId" :session="session" />

            <MediaRecentHistory :user-id="userId" :session="session" />

            <MediaPlaylistShelf :user-id="userId" :session="session" :automatic="false" />
          </div>
        </aside>
      </div>
    </div>
  </div>
</template>
