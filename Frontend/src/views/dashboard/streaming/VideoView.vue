<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { storeToRefs } from "pinia";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRouter } from "vue-router";

import type { MediaFileDto } from "@/api/streaming";
import type { UpNextItem } from "@/stores/player/types";

import MediaGrid from "@/components/streaming/MediaGrid.vue";
import VideoHero from "@/components/streaming/video/VideoHero.vue";
import VideoUpNext from "@/components/streaming/video/VideoUpNext.vue";
import VideoPlayerSkin from "@/components/streaming/VideoPlayerSkin.vue";
import { useVideoHistory } from "@/composables/useVideoHistory";
import { getFilesForStreaming } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import { VIDEO_LIBRARY_LABEL, VIDEO_LIBRARY_REF } from "@/utils/player-source";

const store = usePlayerStore();
const { activeFile, isPlaying, upNextItems, playingFrom, context } = storeToRefs(store);
const router = useRouter();

const railOpen = ref(true);
const skinRef = ref<InstanceType<typeof VideoPlayerSkin> | null>(null);

const { data: libraryPreview, isLoading: libraryLoading } = useQuery(() =>
  getFilesForStreaming({ page: 1, pageSize: 20, isVideo: true, query: null }),
);

const { historyByFileId, recentEntries } = useVideoHistory();

const hasActiveVideo = computed(() => activeFile.value?.isVideo ?? false);

const libraryTotal = computed(() => libraryPreview.value?.totalCount ?? 0);
const libraryEmpty = computed(
  () => !libraryLoading.value && libraryTotal.value === 0 && !hasActiveVideo.value,
);

const showRail = computed(() => railOpen.value && !libraryEmpty.value);

// Hold the two-column grid until the rail finishes sliding out so the
// layout does not snap while the exit transition is still running.
const gridRail = ref(showRail.value);
watch(showRail, (visible) => {
  if (visible) gridRail.value = true;
});

const onRailHidden = () => {
  gridRail.value = false;
};

const resumeCandidate = computed(() => {
  if (hasActiveVideo.value) return null;
  const items = libraryPreview.value?.items ?? [];
  if (items.length === 0) return null;
  const byId = new Map(items.map((file) => [file.fileId, file]));
  for (const entry of recentEntries.value.slice(0, 10)) {
    const isResumable = entry.positionSeconds > 5 && (entry.timesCompleted ?? 0) === 0;
    if (isResumable) {
      const file = byId.get(entry.fileId);
      if (file) return { file, positionSeconds: entry.positionSeconds };
    }
  }
  return null;
});

const suggested = computed<MediaFileDto[]>(() => libraryPreview.value?.items ?? []);

interface HeroSelection {
  file: MediaFileDto;
  positionSeconds: number | null;
}

const heroFile = computed<HeroSelection | null>(() => {
  if (hasActiveVideo.value) return null;
  if (resumeCandidate.value) return resumeCandidate.value;
  const first = libraryPreview.value?.items[0] ?? null;
  if (!first) return null;
  return { file: first, positionSeconds: null };
});

const videoEntries = computed<UpNextItem[]>(() =>
  upNextItems.value.filter((item) => item.file.isVideo && item.kind !== "history"),
);

const contextLabel = computed(() => context.value?.label ?? VIDEO_LIBRARY_LABEL);

const playInLibrary = (file: MediaFileDto) => {
  void store.playTrackInContext(file, VIDEO_LIBRARY_REF, VIDEO_LIBRARY_LABEL);
  window.scrollTo({ top: 0, behavior: "smooth" });
};

const playCandidate = () => {
  if (heroFile.value) playInLibrary(heroFile.value.file);
};

const openExplorer = () => {
  void router.push("/dashboard");
};

const setRailOpen = (open: boolean) => {
  railOpen.value = open;
};

const onKeydown = (e: KeyboardEvent) => {
  const target = e.target as HTMLElement | null;
  const tag = target?.tagName ?? "";
  if (tag === "INPUT" || tag === "TEXTAREA" || target?.isContentEditable) return;
  if (e.code === "Space" && hasActiveVideo.value) {
    e.preventDefault();
    store.togglePlay();
    return;
  }
  if (!hasActiveVideo.value) return;
  if (e.key === "n") store.next();
  if (e.key === "f") skinRef.value?.toggleFullscreen();
};

onMounted(() => {
  document.addEventListener("keydown", onKeydown);
});

onUnmounted(() => {
  document.removeEventListener("keydown", onKeydown);
});
</script>

<template>
  <div class="flex flex-col gap-4 w-full p-4">
    <h1 class="text-lg font-semibold text-gray-900 dark:text-gray-100">Videos</h1>

    <div
      class="grid gap-4"
      :class="gridRail ? 'xl:grid-cols-[minmax(0,1fr)_340px]' : 'grid-cols-1'"
    >
      <section aria-label="Video player" class="min-w-0">
        <div
          v-if="libraryEmpty"
          class="video-stage-frame flex flex-col items-center justify-center gap-2 text-center p-4 sm:p-6"
        >
          <Icon
            icon="mdi:film-open-outline"
            class="w-10 h-10 sm:w-12 sm:h-12 text-gray-400 dark:text-gray-600"
          />
          <h2 class="text-sm sm:text-base font-semibold text-gray-900 dark:text-gray-100 m-0">
            No streamable media yet
          </h2>
          <p class="text-xs sm:text-sm text-gray-600 dark:text-gray-400 m-0 max-w-full sm:max-w-xs">
            Files need a finished transcoding job before they show up here.
          </p>
          <UButton class="mt-2" color="neutral" variant="outline" size="sm" @click="openExplorer">
            Open File Explorer
          </UButton>
        </div>

        <div
          v-else-if="libraryLoading && !hasActiveVideo"
          class="video-stage-frame flex items-center justify-center"
          role="status"
          aria-label="Loading videos"
        >
          <Icon icon="mdi:loading" class="w-8 h-8 animate-spin text-gray-600 dark:text-gray-400" />
        </div>

        <div v-else-if="heroFile" class="video-stage-frame">
          <VideoHero
            :file="heroFile.file"
            :position-seconds="heroFile.positionSeconds"
            @resume="playCandidate"
            @restart="playCandidate"
          />
        </div>

        <VideoPlayerSkin
          v-else
          ref="skinRef"
          :rail-open="railOpen"
          @toggle-rail="setRailOpen(!railOpen)"
        />
      </section>

      <Transition
        enter-active-class="transition-all duration-200 ease-out"
        enter-from-class="opacity-0 translate-x-2"
        enter-to-class="opacity-100 translate-x-0"
        leave-active-class="transition-all duration-150 ease-in"
        leave-from-class="opacity-100 translate-x-0"
        leave-to-class="opacity-0 translate-x-2"
        @after-leave="onRailHidden"
      >
        <aside
          v-if="showRail"
          class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden flex flex-col min-w-0 min-h-0 max-h-[420px] xl:max-h-[62vh]"
          aria-label="Up next"
        >
          <VideoUpNext
            :entries="videoEntries"
            :now-playing="activeFile?.isVideo ? activeFile : null"
            :suggested="hasActiveVideo ? [] : suggested"
            :is-playing="isPlaying"
            :playing-from="playingFrom"
            :context-label="contextLabel"
            @hide="setRailOpen(false)"
            @select="playInLibrary"
          />
        </aside>
      </Transition>
    </div>

    <MediaGrid v-if="!libraryEmpty" mediaType="video" />
  </div>
</template>

<style scoped>
.video-stage-frame {
  position: relative;
  width: 100%;
  aspect-ratio: 16 / 9;
  max-height: 62vh;
  min-height: 20rem;
  border-radius: 1rem;
  overflow: hidden;
  background: radial-gradient(
    120% 100% at 50% 0%,
    color-mix(in srgb, var(--ui-primary, #6366f1) 32%, #000000) 0%,
    #000000 78%
  );
  border: 1px solid rgba(128, 128, 128, 0.25);
}
</style>
