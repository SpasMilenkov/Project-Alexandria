<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { storeToRefs } from "pinia";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";
import type { UpNextItem } from "@/stores/player/types";

import MediaGrid from "@/components/streaming/library/MediaGrid.vue";
import VideoPlayerSkin from "@/components/streaming/player/VideoPlayerSkin.vue";
import VideoEmptyHero from "@/components/streaming/video/VideoEmptyHero.vue";
import VideoEmptyRail from "@/components/streaming/video/VideoEmptyRail.vue";
import VideoHero from "@/components/streaming/video/VideoHero.vue";
import VideoUpNext from "@/components/streaming/video/VideoUpNext.vue";
import { useVideoHistory } from "@/composables/useVideoHistory";
import { VIDEO_LIBRARY_LABEL, VIDEO_LIBRARY_REF } from "@/features/player/source";
import { getFilesForStreaming } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";

const store = usePlayerStore();
const { activeFile, isPlaying, upNextItems, playingFrom, context } = storeToRefs(store);

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
    const isResumable = entry.positionSeconds > 5 && !entry.hasFinished;
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
  <div class="flex min-h-full w-full flex-col gap-4 p-4">
    <header
      class="flex shrink-0 items-center gap-2.5 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <UIcon name="i-lucide-video" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
      <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Videos</h1>
      <UBadge v-if="!libraryLoading" color="neutral" variant="subtle" size="sm">
        {{ libraryTotal }}
      </UBadge>
    </header>

    <div
      class="grid gap-4"
      :class="gridRail || libraryEmpty ? 'xl:grid-cols-[minmax(0,1fr)_340px]' : 'grid-cols-1'"
    >
      <section aria-label="Video player" class="min-w-0">
        <div v-if="libraryEmpty" class="video-stage-frame">
          <VideoEmptyHero />
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

      <VideoEmptyRail v-if="libraryEmpty" />
    </div>

    <MediaGrid
      mediaType="video"
      :empty-message="!libraryEmpty"
      :class="{ 'flex-1': libraryEmpty }"
    />
  </div>
</template>

<style scoped>
.video-stage-frame {
  position: relative;
  width: 100%;
  aspect-ratio: 16 / 9;
  max-height: 62vh;
  min-height: 20rem;
  border-radius: var(--ui-radius);
  overflow: hidden;
  background: radial-gradient(
    120% 100% at 50% 0%,
    color-mix(in srgb, var(--ui-primary, #6366f1) 32%, #000000) 0%,
    #000000 78%
  );
  border: 1px solid rgba(128, 128, 128, 0.25);
}
</style>
