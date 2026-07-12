<template>
  <!-- Mobile-only dim backdrop: on desktop the panel pushes layout instead of overlaying it, so no backdrop is needed there -->
  <Transition
    enter-active-class="transition-opacity duration-200 ease-out"
    enter-from-class="opacity-0"
    enter-to-class="opacity-100"
    leave-active-class="transition-opacity duration-150 ease-in"
    leave-from-class="opacity-100"
    leave-to-class="opacity-0"
  >
    <div v-if="open" class="fixed inset-0 z-20 bg-black/30 sm:hidden" @click="onClose" />
  </Transition>

  <Transition
    enter-active-class="transition-all duration-200 ease-out"
    enter-from-class="opacity-0 translate-x-4"
    enter-to-class="opacity-100 translate-x-0"
    leave-active-class="transition-all duration-150 ease-in"
    leave-from-class="opacity-100 translate-x-0"
    leave-to-class="opacity-0 translate-x-4"
  >
    <aside
      v-if="open"
      class="fixed inset-y-0 right-0 z-30 sm:static sm:inset-auto sm:z-auto sm:shrink-0 flex flex-col w-full sm:w-96 border-l border-gray-200/70 dark:border-gray-700/70 bg-white dark:bg-neutral-900 md:bg-white/60 md:dark:bg-white/[0.06] md:backdrop-blur-sm backdrop-blur-sm"
    >
      <!-- Header -->
      <div class="shrink-0 px-4 pt-4 pb-3 border-b border-gray-200/70 dark:border-gray-700/70">
        <div class="flex items-center justify-between gap-2">
          <h2
            class="text-xs font-semibold tracking-widest uppercase text-gray-500 dark:text-white/35 m-0"
          >
            Lyrics
          </h2>

          <div class="flex items-center gap-1">
            <UDropdownMenu v-if="canManageLyrics" :items="providerMenuItems">
              <UButton
                variant="ghost"
                color="neutral"
                size="sm"
                square
                aria-label="Lyrics options"
                icon="i-mdi-dots-horizontal"
              />
            </UDropdownMenu>

            <UButton
              variant="ghost"
              color="neutral"
              size="sm"
              square
              aria-label="Close lyrics"
              icon="i-mdi-close"
              @click="onClose"
            />
          </div>
        </div>

        <p v-if="activeFile" class="mt-1 text-sm text-gray-700 dark:text-white truncate m-0">
          {{ activeFile.title ?? activeFile.fileName }}
        </p>
        <p v-if="activeFile?.artist" class="text-xs text-gray-400 dark:text-white truncate m-0">
          {{ activeFile.artist }}
        </p>
      </div>

      <!-- Body -->
      <div class="flex-1 min-h-0 flex flex-col">
        <!-- Nothing playing -->
        <div
          v-if="!activeFile"
          class="flex flex-col items-center justify-center gap-3 flex-1 text-center px-6"
        >
          <Icon
            icon="mdi:music-note-outline"
            class="w-12 h-12 text-gray-300 dark:text-white/[0.18]"
          />
          <p class="text-sm text-gray-500 dark:text-white/40 m-0">Nothing playing</p>
          <p class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0">
            Play a song to see its lyrics here.
          </p>
        </div>

        <!-- Not an audio file -->
        <div
          v-else-if="!isAudio"
          class="flex flex-col items-center justify-center gap-3 flex-1 text-center px-6"
        >
          <Icon icon="mdi:video-outline" class="w-12 h-12 text-gray-300 dark:text-white/[0.18]" />
          <p class="text-sm text-gray-500 dark:text-white/40 m-0">
            Lyrics aren't available for video
          </p>
        </div>

        <!-- Loading -->
        <div v-else-if="isLoading" class="flex flex-col items-center justify-center gap-2.5 flex-1">
          <Icon icon="mdi:loading" class="w-5 h-5 animate-spin text-gray-400 dark:text-white/30" />
        </div>

        <!-- Fetching / pending on the backend -->
        <div
          v-else-if="isFetchStatus"
          class="flex flex-col items-center justify-center gap-2.5 flex-1 text-center px-6"
        >
          <template v-if="!fetchTimedOut">
            <Icon icon="mdi:loading" class="w-6 h-6 animate-spin text-gray-400 dark:text-white/30" />
            <p class="text-sm text-gray-500 dark:text-white/40 m-0">Fetching lyrics…</p>
            <p
              v-if="showSlowFetchHint"
              class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0"
            >
              {{ slowFetchHintText }}
            </p>
          </template>

          <template v-else>
            <Icon icon="mdi:timer-sand" class="w-10 h-10 text-gray-300 dark:text-white/[0.18]" />
            <p class="text-sm text-gray-500 dark:text-white/40 m-0">Taking longer than expected</p>
            <p class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0">
              The lyrics provider hasn't responded yet. You can keep waiting or try again.
            </p>
            <UButton variant="outline" color="neutral" size="sm" class="mt-1" @click="onRequeue">
              Try again
            </UButton>
          </template>
        </div>

        <!-- Fetch failed -->
        <div
          v-else-if="isFailedStatus"
          class="flex flex-col items-center justify-center gap-3 flex-1 text-center px-6"
        >
          <Icon
            icon="mdi:alert-circle-outline"
            class="w-12 h-12 text-gray-300 dark:text-white/[0.18]"
          />
          <p class="text-sm text-gray-500 dark:text-white/90 m-0">Couldn't fetch lyrics</p>
          <p class="text-xs text-gray-400 dark:text-white/95 max-w-xs leading-relaxed m-0">
            Something went wrong reaching the lyrics provider.
          </p>
          <div class="flex items-center gap-2 mt-1">
            <UButton variant="outline" color="neutral" size="sm" @click="onRequeue">
              Try again
            </UButton>
            <UButton variant="outline" color="neutral" size="sm" @click="onOpenManualUpload">
              Add manually
            </UButton>
          </div>
        </div>

        <!-- No match found -->
        <div
          v-else-if="isNoMatchStatus && !showManualUpload"
          class="flex flex-col items-center justify-center gap-3 flex-1 text-center px-6"
        >
          <Icon
            icon="mdi:file-question-outline"
            class="w-12 h-12 text-gray-300 dark:text-white/[0.18]"
          />
          <p class="text-sm text-gray-500 dark:text-white/40 m-0">No lyrics found</p>
          <p class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0">
            We couldn't match this track to a lyrics database.
          </p>
          <div class="flex items-center gap-2 mt-1">
            <UButton variant="outline" color="neutral" size="sm" @click="onRequeue">
              Try again
            </UButton>
            <UButton variant="outline" color="neutral" size="sm" @click="onOpenManualUpload">
              Add lyrics manually
            </UButton>
          </div>
        </div>

        <!-- Instrumental -->
        <div
          v-else-if="isInstrumental"
          class="flex flex-col items-center justify-center gap-3 flex-1 text-center px-6"
        >
          <Icon icon="mdi:music-note-off" class="w-12 h-12 text-gray-300 dark:text-white/[0.18]" />
          <p class="text-sm text-gray-500 dark:text-white/40 m-0">Instrumental track</p>
          <p class="text-xs text-gray-400 dark:text-white/25 max-w-xs leading-relaxed m-0">
            There are no vocals to show lyrics for on this track.
          </p>
        </div>

        <!-- Manual upload form -->
        <div v-if="showManualUpload" class="flex flex-col gap-3 px-4 py-4">
          <UFormField label="Paste lyrics">
            <UTextarea
              v-model="manualPlainLyrics"
              :rows="10"
              placeholder="Paste plain lyrics here…"
              class="w-full"
            />
          </UFormField>
          <div class="flex justify-end gap-2">
            <UButton variant="outline" color="neutral" size="sm" @click="onCancelManualUpload">
              Cancel
            </UButton>
            <UButton
              variant="solid"
              color="primary"
              size="sm"
              :loading="isUploading"
              :disabled="!manualPlainLyrics.trim()"
              @click="onManualUploadSubmit"
            >
              Save lyrics
            </UButton>
          </div>
        </div>

        <!-- Synced lyrics -->
        <div
          v-else-if="hasSyncedLyrics"
          ref="scrollerRef"
          class="flex-1 min-h-0 overflow-y-auto py-6"
        >
          <div
            v-for="(line, index) in syncedLines"
            :key="index"
            :ref="(el) => setLineRef(el, index)"
            class="px-5 py-2 rounded-md cursor-pointer text-base leading-relaxed transition-colors duration-150"
            :class="{
              'text-primary font-semibold': index === activeIndex,
              'text-gray-600 dark:text-white/90 hover:text-gray-800 dark:hover:text-white':
                index !== activeIndex,
            }"
            @click="onLineClick(line)"
          >
            <span v-if="line.text">{{ line.text }}</span>
            <span v-else class="inline-flex gap-1 opacity-50">
              <span class="w-1 h-1 rounded-full bg-current" />
              <span class="w-1 h-1 rounded-full bg-current" />
              <span class="w-1 h-1 rounded-full bg-current" />
            </span>
          </div>
        </div>

        <!-- Plain lyrics only -->
        <div v-else-if="hasPlainLyrics" class="flex-1 min-h-0 overflow-y-auto py-6 px-5">
          <p
            v-for="(line, index) in plainLines"
            :key="index"
            class="text-base leading-relaxed text-gray-600 dark:text-white/60 m-0"
            :class="{ 'py-1.5': line.trim() }"
          >
            {{ line }}
          </p>
        </div>
      </div>

      <!-- Footer: provider + confidence + fetched date -->
      <div
        v-if="showFooter"
        class="shrink-0 flex items-center justify-between gap-2 px-4 py-3 border-t border-gray-200/70 dark:border-gray-700/70"
      >
        <div class="flex items-center gap-1.5 min-w-0">
          <UBadge variant="subtle" color="neutral" size="sm">
            {{ providerLabel }}
          </UBadge>

          <UTooltip v-if="confidenceInfo" v-model:open="confidenceTooltipOpen" :text="confidenceScoreLabel">
            <button
              type="button"
              class="focus:outline-none"
              aria-label="Lyrics match confidence"
              @mouseenter="onConfidenceMouseEnter"
              @mouseleave="onConfidenceMouseLeave"
              @click="onConfidenceToggle"
            >
              <UBadge variant="subtle" :color="confidenceInfo.color" size="sm">
                {{ confidenceInfo.label }}
              </UBadge>
            </button>
          </UTooltip>
        </div>

        <span class="text-xs text-gray-400 dark:text-white/30 shrink-0">
          {{ fetchedAtLabel }}
        </span>
      </div>
    </aside>
  </Transition>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, nextTick, onUnmounted, ref, watch } from "vue";

import {
  type LyricLine,
  parsePlainLyrics,
  parseSyncedLyrics,
  useLyricsSync,
} from "@/composables/useLyrics";
import { LyricsProvider } from "@/enums/lyrics-provider";
import { LyricsStatus } from "@/enums/lyrics-status";
import { changeProvider, deleteLyrics, requeueLyrics, uploadLyrics } from "@/mutations/lyrics";
import { getLyrics } from "@/queries/lyrics";
import { usePlayerStore } from "@/stores/stream-player";

const open = defineModel<boolean>("open", { default: false });

const playerStore = usePlayerStore();
const activeFile = computed(() => playerStore.activeFile);
const isAudio = computed(() => playerStore.isAudio);
const currentTime = computed(() => playerStore.currentTime);

const lyricsJobId = computed(() => activeFile.value?.transpilationJobId ?? "");

const { data, isLoading } = useQuery(() => getLyrics(lyricsJobId.value));

// The lyrics record has its own id, separate from the transpilation job id
// used to look it up. Change-provider and delete target that record id;
// requeue targets the job (it's what kicks off a fresh fetch attempt).
const lyricsId = computed(() => data.value?.id ?? "");

// Status helpers

const isFetchStatus = computed(
  () =>
    data.value?.status === LyricsStatus.PendingFetch ||
    data.value?.status === LyricsStatus.Fetching,
);
const isFailedStatus = computed(() => data.value?.status === LyricsStatus.FetchFailed);
const isNoMatchStatus = computed(() => data.value?.status === LyricsStatus.NoMatch);
const isFetchedStatus = computed(() => data.value?.status === LyricsStatus.Fetched);

const isInstrumental = computed(
  () => isFetchedStatus.value && !data.value?.playLyrics && !data.value?.syncedLyrics,
);

const hasSyncedLyrics = computed(() => isFetchedStatus.value && Boolean(data.value?.syncedLyrics));
const hasPlainLyrics = computed(
  () => isFetchedStatus.value && !hasSyncedLyrics.value && Boolean(data.value?.playLyrics),
);

const showFooter = computed(
  () => isFetchedStatus.value && !isInstrumental.value && !showManualUpload.value,
);

// Parsed lyrics + sync

const syncedLines = computed<LyricLine[]>(() =>
  data.value?.syncedLyrics ? parseSyncedLyrics(data.value.syncedLyrics) : [],
);
const plainLines = computed<string[]>(() =>
  data.value?.playLyrics ? parsePlainLyrics(data.value.playLyrics) : [],
);

const { activeIndex } = useLyricsSync(syncedLines, currentTime);

const onLineClick = (line: LyricLine) => {
  playerStore.seek(line.time);
};

// Auto-scroll the active line into view

const scrollerRef = ref<HTMLElement | null>(null);
const lineRefs = ref<(HTMLElement | null)[]>([]);

const setLineRef = (el: Element | null, index: number) => {
  lineRefs.value[index] = el as HTMLElement | null;
};

const prefersReducedMotion = () =>
  typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

watch(activeIndex, async (index) => {
  if (index < 0) return;
  await nextTick();
  lineRefs.value[index]?.scrollIntoView({
    block: "center",
    behavior: prefersReducedMotion() ? "auto" : "smooth",
  });
});

// Header / panel actions

const onClose = () => {
  open.value = false;
};

// Provider label + relative fetched date

const PROVIDER_LABELS: Record<LyricsProvider, string> = {
    [LyricsProvider.LrclibPublic]: "LRCLIB",
    [LyricsProvider.LrclibLocal]: "LRCLIB (local)",
    [LyricsProvider.Musicxmatch]: "Musixmatch",
    [LyricsProvider.Manual]: "Manual upload",
    [LyricsProvider.None]: "None"
};

const providerLabel = computed(() => (data.value ? PROVIDER_LABELS[data.value.provider] : ""));

const relativeTimeFormatter = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });

const formatFetchedAt = (iso: string | null): string => {
  if (!iso) return "Unknown date";
  const date = new Date(iso);
  const diffMs = date.getTime() - Date.now();
  const diffDays = Math.round(diffMs / (1000 * 60 * 60 * 24));

  if (Math.abs(diffDays) >= 1 && Math.abs(diffDays) < 30) {
    return relativeTimeFormatter.format(diffDays, "day");
  }
  if (Math.abs(diffDays) >= 30) {
    return date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  }
  const diffHours = Math.round(diffMs / (1000 * 60 * 60));
  if (Math.abs(diffHours) >= 1) {
    return relativeTimeFormatter.format(diffHours, "hour");
  }
  const diffMinutes = Math.round(diffMs / (1000 * 60));
  return relativeTimeFormatter.format(diffMinutes, "minute");
};

const fetchedAtLabel = computed(() => formatFetchedAt(data.value?.fetchedAt ?? null));

// Confidence tag: the API gives a 1-10 decimal score. We surface a plain-language
// tier by default (Low/Medium/High confidence) and reveal the raw number out of
// 10 on hover (desktop) or tap (mobile) via a controlled tooltip, since the exact
// figure matters when deciding whether to trust a possibly-mismatched result.

type ConfidenceColor = "error" | "warning" | "success";

const getConfidenceTier = (score: number): { label: string; color: ConfidenceColor } => {
  if (score < 4) return { label: "Low confidence", color: "error" };
  if (score < 7.5) return { label: "Medium confidence", color: "warning" };
  return { label: "High confidence", color: "success" };
};

const confidenceInfo = computed(() =>
  data.value?.confidenceScore != null ? getConfidenceTier(data.value.confidenceScore) : null,
);

const confidenceScoreLabel = computed(() =>
  data.value?.confidenceScore != null ? `${data.value.confidenceScore.toFixed(1)} / 10` : "",
);

const confidenceTooltipOpen = ref(false);

const onConfidenceMouseEnter = () => {
  confidenceTooltipOpen.value = true;
};
const onConfidenceMouseLeave = () => {
  confidenceTooltipOpen.value = false;
};
const onConfidenceToggle = () => {
  confidenceTooltipOpen.value = !confidenceTooltipOpen.value;
};

// Requeue: tells the backend to attempt fetching lyrics again (goes back to
// Pending/Fetching). Its onSettled invalidates the byJob query, so the panel
// picks up the new status automatically once it flips.

const { mutate: requeueLyricsMutate } = requeueLyrics();

const onRequeue = () => {
  if (!lyricsJobId.value) return;
  requeueLyricsMutate(lyricsJobId.value);
};

// Slow-fetch feedback: a first fetch reaches out to an external provider and
// can genuinely take a while, so a bare spinner with no explanation reads as
// broken. After a short delay we surface a tooltip explaining the wait: after
// a longer delay we stop pretending it's still in progress and swap to a
// "taking longer than expected" state with a manual retry, so it can't be
// stuck spinning forever.

const SLOW_HINT_DELAY_MS = 5_000;
const FETCH_TIMEOUT_MS = 45_000;

const slowFetchHintText = "First fetches reach out to an external lyrics provider and can take a bit — hang tight.";

const showSlowFetchHint = ref(false);
const fetchTimedOut = ref(false);

let slowHintTimer: ReturnType<typeof setTimeout> | null = null;
let fetchTimeoutTimer: ReturnType<typeof setTimeout> | null = null;

const clearFetchTimers = () => {
  if (slowHintTimer !== null) {
    clearTimeout(slowHintTimer);
    slowHintTimer = null;
  }
  if (fetchTimeoutTimer !== null) {
    clearTimeout(fetchTimeoutTimer);
    fetchTimeoutTimer = null;
  }
};

const resetFetchWaitState = () => {
  clearFetchTimers();
  showSlowFetchHint.value = false;
  fetchTimedOut.value = false;
};

watch(
  isFetchStatus,
  (isFetching) => {
    resetFetchWaitState();
    if (!isFetching) return;

    slowHintTimer = setTimeout(() => {
      showSlowFetchHint.value = true;
    }, SLOW_HINT_DELAY_MS);

    fetchTimeoutTimer = setTimeout(() => {
      fetchTimedOut.value = true;
    }, FETCH_TIMEOUT_MS);
  },
  { immediate: true },
);

// Switching tracks should always restart the wait state, even if both the
// old and new track happen to be Fetching (isFetchStatus wouldn't change).
watch(lyricsJobId, resetFetchWaitState);

onUnmounted(clearFetchTimers);

// Manual upload

const showManualUpload = ref(false);
const manualPlainLyrics = ref("");

const onOpenManualUpload = () => {
  manualPlainLyrics.value = data.value?.playLyrics ?? "";
  showManualUpload.value = true;
};
const onCancelManualUpload = () => {
  showManualUpload.value = false;
};

watch(lyricsJobId, () => {
  showManualUpload.value = false;
  manualPlainLyrics.value = "";
});

const { mutateAsync: uploadMutateAsync, isLoading: isUploading } = uploadLyrics();

const onManualUploadSubmit = async () => {
  if (!lyricsJobId.value || !manualPlainLyrics.value.trim()) return;
  await uploadMutateAsync({
    jobId: lyricsJobId.value,
    plainLyrics: manualPlainLyrics.value,
    syncedLyrics: null,
    isInstrumental: false,
  });
  showManualUpload.value = false;
  manualPlainLyrics.value = "";
};

// Overflow menu: refetch / change provider / delete lyrics
// Manual isn't a real provider to switch *to* — it has its own upload flow
// (above) and the change-provider endpoint rejects it, so it's excluded here.

const canManageLyrics = computed(() => isFetchedStatus.value);

const { mutate: changeProviderMutate } = changeProvider();
const { mutate: deleteLyricsMutate } = deleteLyrics();

const onChangeProvider = (provider: LyricsProvider) => {
  changeProviderMutate({ lyricsId: lyricsId.value, provider });
};
const onDeleteLyrics = () => {
  deleteLyricsMutate(lyricsId.value);
};

const hasExistingLyricsText = computed(
  () => Boolean(data.value?.playLyrics) || Boolean(data.value?.syncedLyrics),
);
const manualUploadMenuLabel = computed(() =>
  hasExistingLyricsText.value ? "Override with manual lyrics" : "Add lyrics manually",
);

const providerMenuItems = computed(() => {
  const currentProvider = data.value?.provider;
  const switchItems = (Object.values(LyricsProvider) as LyricsProvider[])
    .filter((provider) => typeof provider === "number")
    .filter((provider) => provider !== currentProvider && provider !== LyricsProvider.Manual && provider !== LyricsProvider.None)
    .map((provider) => ({
      label: `Switch to ${PROVIDER_LABELS[provider]}`,
      icon: "i-mdi-swap-horizontal",
      onSelect: () => onChangeProvider(provider),
    }));

  return [
    [
      {
        label: manualUploadMenuLabel.value,
        icon: "i-mdi-pencil-outline",
        onSelect: onOpenManualUpload,
      },
      {
        label: "Refetch lyrics",
        icon: "i-mdi-refresh",
        disabled: isFetchStatus.value,
        onSelect: onRequeue,
      },
    ],
    switchItems,
    [
      {
        label: "Delete lyrics",
        icon: "i-mdi-trash-can-outline",
        color: "error",
        onSelect: onDeleteLyrics,
      },
    ],
  ];
});
</script>