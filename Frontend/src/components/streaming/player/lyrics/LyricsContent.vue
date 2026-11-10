<script setup lang="ts">
import type { DropdownMenuItem } from "@nuxt/ui";

import { useQuery } from "@pinia/colada";
import { computed, nextTick, onUnmounted, ref, watch } from "vue";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useLiveRefresh } from "@/composables/useLiveRefresh";
import {
  type LyricLine,
  type LyricsFormat,
  detectLyricsFormat,
  parsePlainLyrics,
  parseSyncedLyrics,
  useLyricsSync,
} from "@/composables/useLyrics";
import { LyricsProvider } from "@/enums/lyrics-provider";
import { LyricsStatus } from "@/enums/lyrics-status";
import { changeProvider, deleteLyrics, requeueLyrics, uploadLyrics } from "@/mutations/lyrics";
import { getLyrics, lyricsPollInterval } from "@/queries/lyrics";
import { usePlayerStore } from "@/stores/stream-player";
import { formatCount } from "@/utils/count";
import { formatTimeAgo } from "@/utils/date-formatters";

import LyricsEmptyState from "./LyricsEmptyState.vue";

const emit = defineEmits<{ (e: "close"): void }>();

const playerStore = usePlayerStore();
const activeFile = computed(() => playerStore.activeFile);
const isAudio = computed(() => playerStore.isAudio);
const currentTime = computed(() => playerStore.currentTime);

const lyricsJobId = computed(() => activeFile.value?.transpilationJobId ?? "");

const { data, isLoading, refetch } = useQuery(() => getLyrics(lyricsJobId.value));

// Background polls must not swap the lyrics for a spinner.
const isFirstLoad = computed(() => isLoading.value && data.value === undefined);

useLiveRefresh(
  () => {
    if (lyricsJobId.value) refetch();
  },
  () => lyricsPollInterval(data.value),
);

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

const setLineRef = (el: unknown, index: number) => {
  if (el instanceof HTMLElement) lineRefs.value[index] = el;
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
  emit("close");
};

// Provider label + relative fetched date

const PROVIDER_LABELS: Record<LyricsProvider, string> = {
  [LyricsProvider.LrclibPublic]: "LRCLIB",
  [LyricsProvider.LrclibLocal]: "LRCLIB (local)",
  [LyricsProvider.Musicxmatch]: "Musixmatch",
  [LyricsProvider.Manual]: "Manual upload",
  [LyricsProvider.None]: "None",
};

const providerLabel = computed(() => (data.value ? PROVIDER_LABELS[data.value.provider] : ""));

const formatFetchedAt = (iso: string | null): string => {
  if (!iso) return "Unknown date";

  return formatTimeAgo(iso, { absoluteAfterDays: 30 });
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

const confidenceInfo = computed(() => {
  const score = data.value?.confidenceScore;
  if (score === null || score === undefined) return null;
  return getConfidenceTier(score);
});

const confidenceScoreLabel = computed(() => {
  const score = data.value?.confidenceScore;
  if (score === null || score === undefined) return "";
  return `${score.toFixed(1)} / 10`;
});

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

const slowFetchHintText =
  "First fetches reach out to an external lyrics provider and can take a moment.";

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

// Manual upload. A single textarea accepts both formats; the pasted content is
// classified with the same parser the panel renders with, so whatever gets
// labeled synced here is guaranteed to come back as a synced view after save.

const showManualUpload = ref(false);
const manualLyrics = ref("");

const FORMAT_META: Record<LyricsFormat, { icon: string; label: string; unit: string }> = {
  plain: { icon: "i-lucide-align-left", label: "Plain text", unit: "line" },
  synced: { icon: "i-mdi-clock-outline", label: "Synced", unit: "timed line" },
};

const MANUAL_PLACEHOLDER =
  "[00:12.00] First line\n[00:15.50] Second line\n\nor plain lyrics, one per row";

const manualFormat = computed(() => {
  const raw = manualLyrics.value;
  if (!raw.trim()) return null;

  const format = detectLyricsFormat(raw);
  const lineCount =
    format === "synced"
      ? parseSyncedLyrics(raw).length
      : raw.split("\n").filter((line) => line.trim()).length;

  return { ...FORMAT_META[format], count: formatCount(lineCount, FORMAT_META[format].unit) };
});

const manualUploadTitle = computed(() =>
  hasExistingLyricsText.value ? "Edit lyrics" : "Add lyrics",
);

// Synced text is preferred so editing existing lyrics never silently drops their timing.
const onOpenManualUpload = () => {
  manualLyrics.value = data.value?.syncedLyrics ?? data.value?.playLyrics ?? "";
  showManualUpload.value = true;
};
const onCancelManualUpload = () => {
  showManualUpload.value = false;
};

watch(lyricsJobId, () => {
  showManualUpload.value = false;
  manualLyrics.value = "";
});

const appToast = useAppToast("lyrics");

const { mutateAsync: uploadMutateAsync, isLoading: isUploading } = uploadLyrics();

const onManualUploadSubmit = async () => {
  if (!lyricsJobId.value || !manualLyrics.value.trim() || isUploading.value) return;

  const isSynced = detectLyricsFormat(manualLyrics.value) === "synced";

  try {
    await uploadMutateAsync({
      jobId: lyricsJobId.value,
      plainLyrics: isSynced ? null : manualLyrics.value,
      syncedLyrics: isSynced ? manualLyrics.value : null,
      isInstrumental: false,
    });
  } catch (cause) {
    appToast.error("Couldn't save lyrics", cause, { retry: onManualUploadSubmit });

    return;
  }

  appToast.success("Lyrics saved", activeFile.value?.title ?? activeFile.value?.fileName);
  showManualUpload.value = false;
  manualLyrics.value = "";
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

const providerMenuItems = computed<DropdownMenuItem[][]>(() => {
  const currentProvider = data.value?.provider;
  const switchItems = (Object.values(LyricsProvider) as LyricsProvider[])
    .filter((provider) => typeof provider === "number")
    .filter(
      (provider) =>
        provider !== currentProvider &&
        provider !== LyricsProvider.Manual &&
        provider !== LyricsProvider.None,
    )
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

<template>
  <div class="flex h-full min-h-0 flex-col">
    <!-- Header -->
    <div class="shrink-0 px-4 pt-4 pb-3 border-b border-gray-200/70 dark:border-gray-700/70">
      <div class="flex items-center justify-between gap-2">
        <h2
          class="text-xs font-semibold tracking-widest uppercase text-gray-600 dark:text-gray-400 m-0"
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

      <p
        v-if="activeFile"
        class="mt-1 text-sm font-medium text-gray-900 dark:text-gray-100 truncate m-0"
      >
        {{ activeFile.title ?? activeFile.fileName }}
      </p>
      <p v-if="activeFile?.artist" class="text-xs text-gray-600 dark:text-gray-400 truncate m-0">
        {{ activeFile.artist }}
      </p>
    </div>

    <!-- Body -->
    <div class="flex-1 min-h-0 flex flex-col">
      <LyricsEmptyState
        v-if="!activeFile"
        icon="i-mdi-music-note-outline"
        title="Nothing playing"
        description="Play a song and its lyrics will follow along here."
      >
        <UButton
          to="/media"
          label="Browse media"
          variant="outline"
          color="neutral"
          size="sm"
          class="rounded-full px-4"
        />
      </LyricsEmptyState>

      <LyricsEmptyState
        v-else-if="!isAudio"
        icon="i-mdi-video-outline"
        title="No lyrics for videos"
        description="Lyrics are only available for audio tracks."
        :ghost="false"
      />

      <LyricsEmptyState v-else-if="isFirstLoad" title="Loading lyrics">
        <template #icon>
          <BlockSpinner :size="22" />
        </template>
      </LyricsEmptyState>

      <LyricsEmptyState
        v-else-if="isFetchStatus && fetchTimedOut"
        icon="i-mdi-timer-sand"
        title="Taking longer than expected"
        description="The lyrics provider hasn't answered yet. Keep waiting, or ask it again."
      >
        <UButton
          label="Try again"
          icon="i-mdi-refresh"
          variant="outline"
          color="neutral"
          size="sm"
          class="rounded-full px-4"
          @click="onRequeue"
        />
      </LyricsEmptyState>

      <LyricsEmptyState
        v-else-if="isFetchStatus"
        title="Looking for lyrics"
        :description="showSlowFetchHint ? slowFetchHintText : undefined"
      >
        <template #icon>
          <BlockSpinner :size="22" />
        </template>
      </LyricsEmptyState>

      <LyricsEmptyState
        v-else-if="isFailedStatus && !showManualUpload"
        icon="i-mdi-alert-circle-outline"
        title="Couldn't fetch lyrics"
        description="Something went wrong reaching the lyrics provider. Try again, or paste them yourself."
      >
        <UButton
          label="Try again"
          icon="i-mdi-refresh"
          variant="outline"
          color="neutral"
          size="sm"
          class="rounded-full px-4"
          @click="onRequeue"
        />
        <UButton
          label="Add manually"
          icon="i-mdi-pencil-outline"
          variant="ghost"
          color="neutral"
          size="sm"
          class="rounded-full px-4"
          @click="onOpenManualUpload"
        />
      </LyricsEmptyState>

      <LyricsEmptyState
        v-else-if="isNoMatchStatus && !showManualUpload"
        icon="i-mdi-magnify"
        title="No lyrics found"
        description="This track didn't match anything in the lyrics providers. Search again, or paste them yourself."
      >
        <UButton
          label="Search again"
          icon="i-mdi-refresh"
          variant="outline"
          color="neutral"
          size="sm"
          class="rounded-full px-4"
          @click="onRequeue"
        />
        <UButton
          label="Add manually"
          icon="i-mdi-pencil-outline"
          variant="ghost"
          color="neutral"
          size="sm"
          class="rounded-full px-4"
          @click="onOpenManualUpload"
        />
      </LyricsEmptyState>

      <LyricsEmptyState
        v-else-if="isInstrumental"
        icon="i-mdi-music-note-off"
        title="Instrumental track"
        description="There are no vocals on this track, so there's nothing to show."
        :ghost="false"
      />

      <!-- Manual upload form -->
      <form
        v-if="showManualUpload"
        class="flex min-h-0 flex-1 flex-col"
        @submit.prevent="onManualUploadSubmit"
      >
        <div class="flex min-h-0 flex-1 flex-col gap-3 px-4 pt-4 pb-3">
          <div class="flex flex-col gap-1">
            <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100 m-0">
              {{ manualUploadTitle }}
            </h3>
            <p class="text-xs leading-relaxed text-gray-600 dark:text-gray-400 m-0">
              Paste plain text, or LRC with timestamps like [01:23.45] to follow along with
              playback.
            </p>
          </div>

          <UTextarea
            v-model="manualLyrics"
            :rows="8"
            autofocus
            aria-label="Lyrics"
            :placeholder="MANUAL_PLACEHOLDER"
            :disabled="isUploading"
            :ui="{
              root: 'flex min-h-0 w-full flex-1',
              base: 'h-full min-h-40 resize-none font-mono text-sm leading-relaxed',
            }"
            @keydown.enter.meta.prevent="onManualUploadSubmit"
            @keydown.enter.ctrl.prevent="onManualUploadSubmit"
          />

          <div class="flex min-h-5 items-center gap-2 text-xs">
            <template v-if="manualFormat">
              <UBadge
                variant="subtle"
                color="neutral"
                size="sm"
                :icon="manualFormat.icon"
                :label="manualFormat.label"
              />
              <span class="text-gray-600 dark:text-gray-400">{{ manualFormat.count }}</span>
            </template>
            <span v-else class="text-gray-500 dark:text-gray-500">
              The format is detected as you paste.
            </span>
          </div>
        </div>

        <div
          class="shrink-0 flex items-center justify-between gap-2 px-4 py-3 border-t border-gray-200/70 dark:border-gray-700/70"
        >
          <span
            class="hidden items-center gap-1 text-xs text-gray-500 md:inline-flex dark:text-gray-500"
          >
            <UKbd value="meta" size="sm" />
            <UKbd value="enter" size="sm" />
            to save
          </span>

          <div class="ml-auto flex items-center gap-2">
            <UButton
              type="button"
              label="Cancel"
              variant="outline"
              color="neutral"
              size="sm"
              :disabled="isUploading"
              @click="onCancelManualUpload"
            />
            <UButton
              type="submit"
              label="Save lyrics"
              variant="solid"
              color="primary"
              size="sm"
              :loading="isUploading"
              :disabled="!manualLyrics.trim()"
            />
          </div>
        </div>
      </form>

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
            'text-gray-700 dark:text-gray-300 hover:text-gray-900 dark:hover:text-gray-100':
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
          class="text-base leading-relaxed text-gray-700 dark:text-gray-300 m-0"
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

        <UTooltip
          v-if="confidenceInfo"
          v-model:open="confidenceTooltipOpen"
          :text="confidenceScoreLabel"
        >
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

      <span class="text-xs text-gray-500 dark:text-gray-500 shrink-0">
        {{ fetchedAtLabel }}
      </span>
    </div>
  </div>
</template>
