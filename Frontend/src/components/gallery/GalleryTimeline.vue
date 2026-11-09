<script setup lang="ts">
import type { RecycleScrollerExposed } from "vue-virtual-scroller";

import { useQuery } from "@pinia/colada";
import { useDebounceFn, useElementSize, useEventListener, useLocalStorage } from "@vueuse/core";
import {
  type ShallowUnwrapRef,
  computed,
  onBeforeUnmount,
  ref,
  shallowRef,
  useTemplateRef,
  watch,
} from "vue";
import { useRoute, useRouter } from "vue-router";

import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import { useGalleryActions } from "@/composables/useGalleryActions";
import { useGalleryMonths } from "@/composables/useGalleryMonths";
import { useGallerySelection } from "@/composables/useGallerySelection";
import { GallerySort } from "@/enums/gallery-sort";
import { monthKey, photoCountLabel, viewerTimeZone } from "@/features/gallery/months";
import { type PhotoPick, photoRange } from "@/features/gallery/selection";
import {
  type TimelineItem,
  type TimelineMetrics,
  buildTimeline,
  rowSpanOf,
  sectionAt,
  sectionsBetween,
} from "@/features/gallery/timeline";
import { findPhoto, locatePhoto, stepFrom } from "@/features/gallery/viewer";
import { galleryTimeline } from "@/queries/gallery";

import GalleryEmpty from "./GalleryEmpty.vue";
import GalleryGhost from "./GalleryGhost.vue";
import GalleryScrubber from "./GalleryScrubber.vue";
import GalleryTile from "./GalleryTile.vue";
import GalleryViewer from "./GalleryViewer.vue";

// Item heights include the space above each header, which is what separates months and years.
const MONTH_HEADER_HEIGHT = 72;
const YEAR_HEADER_HEIGHT = 112;
const LEADING_YEAR_HEADER_HEIGHT = 68;
const LOAD_DEBOUNCE_MS = 120;

const SORT_OPTIONS = [
  {
    hint: "Photos without a capture date use their upload date",
    label: "Date taken",
    shortLabel: "Taken",
    value: GallerySort.Taken,
  },
  { hint: undefined, label: "Date uploaded", shortLabel: "Uploaded", value: GallerySort.Uploaded },
];

const timeZone = viewerTimeZone();
const sort = useLocalStorage<GallerySort>("gallery-sort", GallerySort.Taken);

const {
  data: timeline,
  error: timelineError,
  isPending,
  refetch: refetchTimeline,
} = useQuery(() => galleryTimeline({ sort: sort.value, timeZone }));

const months = useGalleryMonths(
  () => sort.value,
  () => timeZone,
);

const timelineMonths = computed(() => timeline.value?.months ?? []);

const selection = useGallerySelection();
const { active: selecting, count: selectedCount, photos: selectedPhotos, isSelected } = selection;

const actions = useGalleryActions({
  loader: months,
  months: () => timelineMonths.value,
  onRemoved: selection.forget,
});

// Measured inside the scroller so the native scrollbar width never counts as room for photos.
const widthProbe = useTemplateRef<HTMLElement>("widthProbe");
const { width: containerWidth } = useElementSize(widthProbe);

const viewport = useTemplateRef<HTMLElement>("viewport");
const { height: viewportHeight } = useElementSize(viewport);

// Refs exposed by a component arrive unwrapped on its instance.
const scroller = useTemplateRef<ShallowUnwrapRef<RecycleScrollerExposed<TimelineItem>>>("scroller");

const targetRowHeight = computed(() => {
  if (containerWidth.value < 640) return 120;
  if (containerWidth.value < 1024) return 160;

  return 200;
});

// Phones keep tighter gaps: at 120px rows, wide gutters would eat into the photos themselves.
const tileGap = computed(() => (containerWidth.value < 640 ? 6 : 12));

const metrics = computed<TimelineMetrics>(() => ({
  containerWidth: containerWidth.value,
  gap: tileGap.value,
  headerHeight: MONTH_HEADER_HEIGHT,
  leadingYearHeaderHeight: LEADING_YEAR_HEADER_HEIGHT,
  targetHeight: targetRowHeight.value,
  yearHeaderHeight: YEAR_HEADER_HEIGHT,
}));

const layout = computed(() =>
  buildTimeline(timeline.value?.months ?? [], months.loaded.value, metrics.value),
);

const total = computed(() => timeline.value?.total ?? 0);
const showScrubber = computed(() => layout.value.totalHeight > viewportHeight.value);

// Read at most once per frame: the scrubber and the month loader both follow it, and scroll
// events can fire far more often than the screen repaints.
const scrollTop = ref(0);
let scrollFrame: number | null = null;

const onScroll = () => {
  if (scrollFrame !== null) return;

  scrollFrame = requestAnimationFrame(() => {
    scrollFrame = null;
    scrollTop.value = scroller.value?.el?.scrollTop ?? 0;
  });
};

onBeforeUnmount(() => {
  if (scrollFrame !== null) cancelAnimationFrame(scrollFrame);
});

const loadNearViewport = useDebounceFn(() => {
  // One viewport of margin on each side, so a slow scroll finds the next month ready.
  const margin = viewportHeight.value;
  const top = scrollTop.value - margin;
  const bottom = scrollTop.value + viewportHeight.value + margin;

  void months.load(sectionsBetween(layout.value.sections, top, bottom));
}, LOAD_DEBOUNCE_MS);

watch([scrollTop, viewportHeight, layout], () => void loadNearViewport());

// A month that loads above the viewport replaces an estimated height with its real one. Keep
// the month at the top of the screen where it was, so that correction never moves the view.
watch(
  layout,
  (next, previous) => {
    const element = scroller.value?.el;
    if (!element || !previous) return;

    const top = element.scrollTop;
    if (top <= 0) return;

    const anchor = sectionAt(previous.sections, top);
    const moved = anchor && next.sections.find((section) => section.key === anchor.key);
    if (!anchor || !moved) return;

    const target = moved.start + (top - anchor.start);
    if (Math.abs(target - top) <= 1) return;

    // The scroller re-renders on its next frame; without this, a shift larger than its buffer
    // paints one frame of empty space before the rows catch up.
    element.scrollTop = target;
    scrollTop.value = target;
    scroller.value?.updateVisibleItems(false);
  },
  { flush: "post" },
);

watch(sort, () => {
  selection.clear();
  months.reset();
  scroller.value?.scrollToPosition(0);
});

const seek = (offset: number) => {
  scroller.value?.scrollToPosition(offset);
};

const scrollBy = (delta: number) => {
  scroller.value?.el?.scrollBy({ top: delta });
};

// The open photo lives in the URL, so the back button and the phone's back gesture close the
// viewer instead of leaving the gallery. Turning pages replaces the entry, so one back is
// always enough.
const route = useRoute();
const router = useRouter();

const viewedId = computed(() => {
  const value = route.query.photo;

  return typeof value === "string" ? value : null;
});

const viewedPhoto = computed(() => {
  if (!viewedId.value) return null;

  return findPhoto(timeline.value?.months ?? [], months.loaded.value, viewedId.value);
});

// Whether the open viewer added a history entry, which closing should pop rather than stack.
let pushedViewer = false;
// Set until the URL has let go of the photo, so a second close in the meantime (a trashed photo
// vanishing mid-close, say) does not navigate twice.
let closingViewer = false;

const queryWith = (photo: string | undefined) => {
  const { photo: _previous, ...rest } = route.query;

  return photo === undefined ? rest : { ...rest, photo };
};

const viewPhoto = (photo: GalleryPhoto) => {
  pushedViewer = true;
  void router.push({ query: queryWith(photo.fileId) });
};

const showPhoto = (photo: GalleryPhoto) => {
  void router.replace({ query: queryWith(photo.fileId) });
};

// Leaves the last photo seen on screen, so closing after paging lands where the viewer ended.
const revealPhoto = (fileId: string) => {
  const span = rowSpanOf(layout.value, fileId);
  if (!span) return;

  const top = scrollTop.value;
  const visible = span.start >= top && span.end <= top + viewportHeight.value;
  if (visible) return;

  scroller.value?.scrollToPosition(Math.max(0, span.start - viewportHeight.value / 3));
};

const closeViewer = () => {
  if (closingViewer) return;

  closingViewer = true;
  if (viewedId.value) revealPhoto(viewedId.value);

  if (pushedViewer) {
    pushedViewer = false;
    router.back();
    return;
  }

  void router.replace({ query: queryWith(undefined) });
};

const loadViewerMonth = (month: GalleryMonth) => months.load([month]);

// Closing clears the URL straight away, which on its own would unmount the viewer mid-fade.
// It keeps its last photo until its closing animation has played out.
const lingeringPhoto = shallowRef<GalleryPhoto | null>(null);

watch(viewedPhoto, (photo) => {
  if (photo) lingeringPhoto.value = photo;
});

const viewerPhoto = computed(() => viewedPhoto.value ?? lingeringPhoto.value);
const viewerOpen = computed(() => viewedPhoto.value !== null);

const onViewerClosed = () => {
  if (!viewedPhoto.value) lingeringPhoto.value = null;
};

// A link or reload can name a photo whose month is far off and not loaded, and a trashed photo
// leaves the timeline altogether. Either way there is nothing to show, so the URL lets go.
watch(
  [viewedId, viewedPhoto, isPending],
  ([id, photo, pending]) => {
    if (!id) {
      pushedViewer = false;
      closingViewer = false;
      return;
    }

    if (!photo && !pending) closeViewer();
  },
  { immediate: true },
);

// The anchor is the last photo picked on its own. A range reaches from it to the clicked photo
// across months, as long as both ends are loaded; otherwise the click picks just that photo.
const PICKS: Record<PhotoPick, (photo: GalleryPhoto, range: GalleryPhoto[]) => void> = {
  "add-range": (photo, range) => {
    if (range.length > 0) selection.setMany(range, true);
    else selection.toggle(photo);
  },
  only: (photo) => selection.selectOnly(photo),
  range: (photo, range) => {
    if (range.length > 0) selection.replaceWith(range);
    else selection.toggle(photo);
  },
  toggle: (photo) => selection.toggle(photo),
};

const onPick = (photo: GalleryPhoto, how: PhotoPick) => {
  const anchorId = selection.anchorId();
  const range = anchorId
    ? photoRange(timelineMonths.value, months.loaded.value, anchorId, photo.fileId)
    : [];

  PICKS[how](photo, range);
};

type MonthPick = "all" | "some" | "none";

const monthPick = (key: string): MonthPick => {
  const photos = months.loaded.value.get(key) ?? [];
  const picked = photos.filter((photo) => isSelected(photo.fileId)).length;

  if (picked === 0) return "none";

  return picked === photos.length ? "all" : "some";
};

const MONTH_PICK_LABELS: Record<MonthPick, string> = {
  all: "Deselect every photo in",
  none: "Select every photo in",
  some: "Select every photo in",
};

// A month scrolled into view only as a placeholder loads first, so "every photo" means every
// photo and not just the ones that happened to be on screen.
const toggleMonth = async (key: string) => {
  const month = timelineMonths.value.find((entry) => monthKey(entry.year, entry.month) === key);
  if (!month) return;

  const wasAll = monthPick(key) === "all";
  await months.load([month]);

  selection.setMany(months.loaded.value.get(key) ?? [], !wasAll);
};

useEventListener(window, "keydown", (event: KeyboardEvent) => {
  if (event.key === "Escape" && selecting.value && !viewerOpen.value) selection.clear();
});

const COUNT_FORMAT = new Intl.NumberFormat();
const selectedLabel = computed(() => `${COUNT_FORMAT.format(selectedCount.value)} selected`);

const trashing = ref(false);

const trashSelected = async () => {
  trashing.value = true;
  await actions.trash(selectedPhotos.value);
  trashing.value = false;
};

const shareSelected = () => {
  const [only] = selectedPhotos.value;
  if (only && selectedCount.value === 1) actions.share(only);
};

// The viewer moves on to the next photo, or back one at the end of the timeline, and closes
// when the trashed photo was the only one left.
const neighbourOf = (photo: GalleryPhoto) => {
  const position = locatePhoto(timelineMonths.value, months.loaded.value, photo.fileId);
  if (!position) return null;

  for (const direction of [1, -1] as const) {
    const step = stepFrom(timelineMonths.value, months.loaded.value, position, direction);
    if (step.kind === "photo") return step.photo;
  }

  return null;
};

const trashViewed = async (photo: GalleryPhoto) => {
  const neighbour = neighbourOf(photo);

  // Leaves the photo before it disappears from the timeline, so the viewer never sees itself
  // pointing at nothing and closes on its own.
  await actions.trash([photo], async () => {
    if (neighbour) await router.replace({ query: queryWith(neighbour.fileId) });
    else closeViewer();
  });
};
</script>

<template>
  <div class="flex h-full flex-col gap-4 overflow-hidden p-3">
    <div
      class="flex min-h-13 shrink-0 items-center gap-3 rounded-2xl border border-gray-200/70 px-3 py-2 frosted-glass glass-surface sm:px-4 dark:border-gray-700/70"
    >
      <!-- While photos are picked the toolbar becomes the selection bar, in the same place and at
           the same height, so the grid below never shifts. -->
      <template v-if="selecting">
        <UButton
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          size="sm"
          aria-label="Clear selection"
          title="Clear selection (Esc)"
          @click="selection.clear()"
        />

        <span
          class="text-sm font-semibold whitespace-nowrap text-gray-900 tabular-nums dark:text-gray-100"
        >
          {{ selectedLabel }}
        </span>

        <div class="flex-1" />

        <UButton
          icon="i-lucide-share-2"
          color="neutral"
          variant="ghost"
          size="sm"
          :disabled="selectedCount !== 1"
          :title="selectedCount === 1 ? 'Share' : 'Share works on one photo at a time'"
          aria-label="Share"
          @click="shareSelected"
        >
          <span class="max-sm:hidden">Share</span>
        </UButton>

        <UButton
          icon="i-lucide-download"
          color="neutral"
          variant="ghost"
          size="sm"
          aria-label="Download"
          :title="selectedCount === 1 ? 'Download' : 'Download as a zip'"
          @click="actions.download(selectedPhotos)"
        >
          <span class="max-sm:hidden">Download</span>
        </UButton>

        <UButton
          icon="i-lucide-trash-2"
          color="error"
          variant="ghost"
          size="sm"
          aria-label="Move to trash"
          :loading="trashing"
          @click="trashSelected"
        >
          <span class="max-sm:hidden">Trash</span>
        </UButton>
      </template>

      <template v-else>
        <h2
          class="m-0 text-xs font-semibold tracking-widest text-gray-600 uppercase dark:text-gray-400"
        >
          Photos
        </h2>
        <span
          v-if="timeline"
          class="text-xs whitespace-nowrap text-gray-600 tabular-nums dark:text-gray-400"
        >
          {{ total }} photo{{ total === 1 ? "" : "s" }}
        </span>

        <div class="flex-1" />

        <div
          role="radiogroup"
          aria-label="Order photos by"
          class="inline-flex items-center gap-0.5 rounded-lg bg-black/[0.04] p-0.5 dark:bg-white/[0.05]"
        >
          <button
            v-for="option in SORT_OPTIONS"
            :key="option.value"
            type="button"
            role="radio"
            :aria-checked="sort === option.value"
            class="h-8 rounded-md px-3 text-xs font-medium transition-all duration-150"
            :class="
              sort === option.value
                ? 'bg-white text-gray-900 shadow-sm dark:bg-white/10 dark:text-gray-100'
                : 'text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100'
            "
            :title="option.hint"
            @click="sort = option.value"
          >
            <span class="sm:hidden">{{ option.shortLabel }}</span>
            <span class="max-sm:hidden">{{ option.label }}</span>
          </button>
        </div>
      </template>
    </div>

    <div v-if="isPending" class="flex min-h-0 flex-1 items-center justify-center">
      <BlockSpinner :size="24" />
    </div>

    <div v-else-if="timelineError" class="flex min-h-0 flex-1 overflow-y-auto">
      <div
        class="m-auto flex flex-col items-center gap-2.5 rounded-2xl border border-gray-200/70 px-8 py-6 text-center frosted-glass glass-surface-strong dark:border-gray-700/70"
      >
        <UIcon name="i-lucide-image-off" class="size-9 text-red-400" />
        <p class="m-0 text-sm text-gray-900 dark:text-gray-100">The timeline did not load</p>
        <UButton color="neutral" variant="outline" size="sm" @click="refetchTimeline()">
          Try again
        </UButton>
      </div>
    </div>

    <EmptyStatePanel v-else-if="total === 0">
      <template #ghost>
        <GalleryGhost />
      </template>

      <GalleryEmpty class="m-auto" />
    </EmptyStatePanel>

    <div v-else ref="viewport" class="relative min-h-0 flex-1">
      <!-- The scrubber replaces the native scrollbar. The hiding utilities are important because
           main.css styles every scrollbar outside a cascade layer, which beats any utility.
           The right padding keeps photos clear of the scrubber's hidden hover strip, so the
           strip never steals clicks from the edge of a photo. -->
      <RecycleScroller
        ref="scroller"
        class="h-full w-full [overflow-anchor:none] [scrollbar-width:none]! [&::-webkit-scrollbar]:hidden!"
        :class="{ 'pr-6': showScrubber }"
        :items="layout.items"
        :item-size="null"
        size-field="size"
        type-field="kind"
        key-field="id"
        :buffer="600"
        @scroll.passive="onScroll"
      >
        <template #before>
          <div ref="widthProbe" class="h-0 w-full" aria-hidden="true" />
        </template>

        <template #default="{ item }">
          <div
            v-if="item.kind === 'year'"
            class="flex items-end pb-4"
            :style="{ height: `${item.size}px` }"
          >
            <h2
              class="m-0 flex w-full items-center justify-between gap-3 rounded-2xl border border-gray-200/70 px-4 py-1.5 shadow-sm frosted-glass glass-surface-strong dark:border-gray-700/70"
            >
              <span
                class="text-2xl font-bold tracking-tight text-gray-900 tabular-nums sm:text-3xl dark:text-gray-100"
              >
                {{ item.year }}
              </span>

              <span
                class="flex items-center gap-1.5 text-sm text-gray-600 tabular-nums dark:text-gray-400"
              >
                <UIcon name="i-lucide-images" class="size-4 shrink-0" />
                {{ photoCountLabel(item.count) }}
              </span>
            </h2>
          </div>

          <div
            v-else-if="item.kind === 'header'"
            class="flex items-end pb-3"
            :style="{ height: `${item.size}px` }"
          >
            <h3
              class="group/month m-0 flex w-full items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-3 py-1 frosted-glass glass-surface-strong dark:border-gray-700/70"
            >
              <span class="text-base font-semibold text-gray-900 sm:text-lg dark:text-gray-100">
                {{ item.label }}
              </span>

              <span class="flex items-center gap-3">
                <span
                  class="flex items-center gap-1.5 text-sm text-gray-600 tabular-nums dark:text-gray-400"
                >
                  <UIcon name="i-lucide-images" class="size-4 shrink-0" />
                  {{ photoCountLabel(item.count) }}
                </span>

                <!-- Its space is kept while hidden, so the count never jumps sideways on hover. -->
                <button
                  type="button"
                  class="flex size-6 items-center justify-center rounded-xl border-2 transition-opacity duration-150 motion-reduce:transition-none"
                  :class="[
                    monthPick(item.monthKey) === 'none'
                      ? 'border-gray-400 text-transparent hover:text-gray-500 dark:border-gray-500'
                      : 'border-primary bg-primary text-white',
                    selecting
                      ? 'opacity-100'
                      : 'opacity-0 group-hover/month:opacity-100 focus-visible:opacity-100 pointer-coarse:invisible',
                  ]"
                  :aria-label="`${MONTH_PICK_LABELS[monthPick(item.monthKey)]} ${item.label}`"
                  :aria-pressed="monthPick(item.monthKey) === 'all'"
                  @click="toggleMonth(item.monthKey)"
                >
                  <UIcon
                    :name="
                      monthPick(item.monthKey) === 'some' ? 'i-lucide-minus' : 'i-lucide-check'
                    "
                    class="size-3.5"
                  />
                </button>
              </span>
            </h3>
          </div>

          <div
            v-else-if="item.kind === 'row'"
            class="flex items-start overflow-hidden"
            :style="{ gap: `${tileGap}px`, height: `${item.size}px` }"
          >
            <GalleryTile
              v-for="tile in item.row.tiles"
              :key="tile.item.fileId"
              :photo="tile.item"
              :width="tile.width"
              :height="item.row.height"
              :selected="isSelected(tile.item.fileId)"
              :selecting="selecting"
              @open="viewPhoto"
              @pick="onPick"
            />
          </div>

          <div v-else :style="{ height: `${item.size}px`, paddingBottom: `${tileGap}px` }">
            <div class="h-full rounded-md bg-black/[0.03] dark:bg-white/[0.04]" />
          </div>
        </template>
      </RecycleScroller>

      <GalleryScrubber
        v-if="showScrubber"
        :layout="layout"
        :scroll-top="scrollTop"
        :viewport-height="viewportHeight"
        @seek="seek"
        @scroll-by="scrollBy"
      />
    </div>

    <GalleryViewer
      v-if="viewerPhoto && timeline"
      :open="viewerOpen"
      :photo="viewerPhoto"
      :months="timeline.months"
      :loaded="months.loaded.value"
      :sort="sort"
      :load-month="loadViewerMonth"
      @navigate="showPhoto"
      @close="closeViewer"
      @closed="onViewerClosed"
      @share="actions.share"
      @show-in-folder="actions.showInFolder"
      @trash="trashViewed"
    />
  </div>
</template>
