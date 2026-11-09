<script setup lang="ts">
import { useQueryCache } from "@pinia/colada";
import { useLocalStorage } from "@vueuse/core";
import { type ComponentPublicInstance, computed, ref, shallowRef, watch } from "vue";

import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import FileCaptureDetails from "@/components/dashboard/file-system/FileCaptureDetails.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useFileDownload } from "@/composables/useFileDownload";
import { GallerySort } from "@/enums/gallery-sort";
import { type ViewerStep, locatePhoto, photoDateLabel, stepFrom } from "@/features/gallery/viewer";
import { getPreviewByVersion } from "@/queries/files";

import GalleryViewerImage from "./GalleryViewerImage.vue";

const { open, photo, months, loaded, sort, loadMonth } = defineProps<{
  open: boolean;
  photo: GalleryPhoto;
  months: GalleryMonth[];
  loaded: ReadonlyMap<string, GalleryPhoto[]>;
  sort: GallerySort;
  loadMonth: (month: GalleryMonth) => Promise<void>;
}>();

const emit = defineEmits<{
  navigate: [photo: GalleryPhoto];
  close: [];
  closed: [];
  share: [photo: GalleryPhoto];
  trash: [photo: GalleryPhoto];
}>();

const ZOOM_STEP = 1.5;

const toast = useAppToast("files");
const queryCache = useQueryCache();
const { downloadVersion } = useFileDownload();

const infoOpen = useLocalStorage("gallery-viewer-info", false);

type ViewerImage = InstanceType<typeof GalleryViewerImage>;

// While photos crossfade, the outgoing one is still mounted and unmounts after the incoming
// one, so the ref keeps the newest instance and ignores the old one letting go.
const image = shallowRef<ViewerImage | null>(null);

const bindImage = (instance: Element | ComponentPublicInstance | null) => {
  if (instance) image.value = instance as ViewerImage;
};
const zoomed = ref(false);

// Each photo mounts its own image at fit, so the toolbar follows suit when the page turns.
watch(
  () => photo.fileId,
  () => {
    zoomed.value = false;
  },
);

const position = computed(() => locatePhoto(months, loaded, photo.fileId));

const stepToward = (direction: 1 | -1): ViewerStep => {
  if (!position.value) return { kind: "end" };

  return stepFrom(months, loaded, position.value, direction);
};

const previous = computed(() => stepToward(-1));
const next = computed(() => stepToward(1));

const dateLabel = computed(() => photoDateLabel(photo, sort));
const uploadedLabel = computed(() => photoDateLabel(photo, GallerySort.Uploaded));

// Neighbours are fetched ahead: a month at the edge starts loading, and a photo next door gets
// its preview resolved and decoded, so turning the page shows the sharp image straight away.
const prefetchedImages = new Set<string>();

const warmPreview = async (neighbour: GalleryPhoto) => {
  const entry = queryCache.ensure(
    getPreviewByVersion({
      fileId: neighbour.fileId,
      mimeType: neighbour.mimeType,
      versionId: neighbour.versionId,
    }),
  );
  const state = await queryCache.refresh(entry);
  const url = state.status === "success" ? state.data.previewUrl : null;

  if (!url || prefetchedImages.has(url)) return;

  prefetchedImages.add(url);
  const decoder = new Image();
  decoder.src = url;
};

const prepare = (step: ViewerStep) => {
  if (step.kind === "photo") void warmPreview(step.photo);
  if (step.kind === "load") void loadMonth(step.month);
};

watch([previous, next], ([before, after]) => [before, after].forEach(prepare), {
  immediate: true,
});

const turning = ref(false);

// The direction of the last page turn, which decides which side the photos slide toward.
const travel = ref<1 | -1>(1);

// A brief drift plus crossfade, so each turn reads as moving along the timeline without
// holding up someone flicking through quickly. Reduced motion keeps only the crossfade.
const PHOTO_MOTION =
  "transition-[opacity,translate] duration-120 ease-[cubic-bezier(0.2,0,0,1)] motion-reduce:transition-opacity";

const photoTransition = computed(() => {
  const ahead = "translate-x-3 motion-reduce:translate-x-0";
  const behind = "-translate-x-3 motion-reduce:translate-x-0";

  return {
    enterActive: PHOTO_MOTION,
    enterFrom: `opacity-0 ${travel.value === 1 ? ahead : behind}`,
    leaveActive: PHOTO_MOTION,
    leaveTo: `opacity-0 ${travel.value === 1 ? behind : ahead}`,
  };
});

const turn = async (direction: 1 | -1) => {
  if (turning.value) return;

  let step = stepToward(direction);

  if (step.kind === "load") {
    turning.value = true;
    await loadMonth(step.month);
    turning.value = false;

    step = stepToward(direction);
  }

  if (step.kind === "photo") {
    travel.value = direction;
    emit("navigate", step.photo);
    return;
  }

  // A swipe that was carrying the photo out of view brings it back when nothing replaces it.
  image.value?.settle();
  if (step.kind === "load") toast.error("Could not load the next photos");
};

const download = async () => {
  try {
    await downloadVersion(photo.versionId);
  } catch (error) {
    toast.error("Could not download the photo", error);
  }
};

const KEY_ACTIONS: Record<string, () => void> = {
  ArrowLeft: () => void turn(-1),
  ArrowRight: () => void turn(1),
  "+": () => image.value?.zoomBy(ZOOM_STEP),
  "-": () => image.value?.zoomBy(1 / ZOOM_STEP),
  "0": () => image.value?.reset(),
  "=": () => image.value?.zoomBy(ZOOM_STEP),
  Delete: () => emit("trash", photo),
  i: () => {
    infoOpen.value = !infoOpen.value;
  },
};

const onKeydown = (event: KeyboardEvent) => {
  if (event.altKey || event.ctrlKey || event.metaKey) return;

  const action = KEY_ACTIONS[event.key];
  if (!action) return;

  event.preventDefault();
  action();
};

const isOpen = computed({
  get: () => open,
  set: (wanted: boolean) => {
    if (!wanted) emit("close");
  },
});
</script>

<template>
  <UModal
    v-model:open="isOpen"
    @after:leave="emit('closed')"
    fullscreen
    :title="photo.name"
    :description="dateLabel"
    :ui="{ content: 'divide-y-0 bg-white text-gray-900 dark:bg-black dark:text-white' }"
  >
    <template #content>
      <div class="flex size-full min-h-0" @keydown="onKeydown">
        <div class="relative flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
          <header
            class="absolute inset-x-0 top-0 z-10 flex items-center gap-2 bg-linear-to-b from-white/80 to-transparent dark:from-black/80 px-2 pt-2 pb-8 sm:px-4"
          >
            <UButton
              icon="i-lucide-arrow-left"
              color="neutral"
              variant="ghost"
              aria-label="Back to photos"
              class="text-gray-900 hover:bg-gray-900/10 dark:text-white dark:hover:bg-white/10"
              @click="emit('close')"
            />

            <div class="flex min-w-0 flex-1 flex-col">
              <span class="truncate text-sm font-medium text-gray-900 dark:text-white">{{ photo.name }}</span>
              <span class="truncate text-xs text-gray-600 tabular-nums dark:text-gray-300">{{ dateLabel }}</span>
            </div>

            <UButton
              :icon="zoomed ? 'i-lucide-zoom-out' : 'i-lucide-zoom-in'"
              color="neutral"
              variant="ghost"
              :aria-label="zoomed ? 'Fit to screen' : 'Zoom in'"
              :aria-pressed="zoomed"
              class="text-gray-900 hover:bg-gray-900/10 dark:text-white dark:hover:bg-white/10"
              @click="image?.toggle()"
            />

            <UButton
              icon="i-lucide-info"
              color="neutral"
              variant="ghost"
              :aria-label="infoOpen ? 'Hide info' : 'Show info'"
              :aria-pressed="infoOpen"
              class="text-gray-900 hover:bg-gray-900/10 dark:text-white dark:hover:bg-white/10"
              :class="{ 'bg-gray-900/10 dark:bg-white/15': infoOpen }"
              @click="infoOpen = !infoOpen"
            />

            <UButton
              icon="i-lucide-share-2"
              color="neutral"
              variant="ghost"
              aria-label="Share"
              class="text-gray-900 hover:bg-gray-900/10 dark:text-white dark:hover:bg-white/10"
              @click="emit('share', photo)"
            />

            <UButton
              icon="i-lucide-download"
              color="neutral"
              variant="ghost"
              aria-label="Download"
              class="text-gray-900 hover:bg-gray-900/10 dark:text-white dark:hover:bg-white/10"
              @click="download"
            />

            <UButton
              icon="i-lucide-trash-2"
              color="neutral"
              variant="ghost"
              aria-label="Move to trash"
              title="Move to trash (Delete)"
              class="text-gray-900 hover:bg-gray-900/10 dark:text-white dark:hover:bg-white/10"
              @click="emit('trash', photo)"
            />
          </header>

          <div class="relative min-h-0 flex-1">
            <Transition
              :enter-active-class="photoTransition.enterActive"
              :enter-from-class="photoTransition.enterFrom"
              :leave-active-class="photoTransition.leaveActive"
              :leave-to-class="photoTransition.leaveTo"
            >
              <GalleryViewerImage
                :ref="bindImage"
                :key="photo.fileId"
                :photo="photo"
                :has-previous="previous.kind !== 'end'"
                :has-next="next.kind !== 'end'"
                @turn="turn"
                @close="emit('close')"
                @zoomed="zoomed = $event"
              />
            </Transition>
          </div>

          <!-- Swipes replace these on touch screens, where they would only cover the photo. -->
          <UButton
            v-if="previous.kind !== 'end'"
            icon="i-lucide-chevron-left"
            color="neutral"
            variant="ghost"
            size="xl"
            aria-label="Previous photo"
            class="absolute top-1/2 left-3 -translate-y-1/2 rounded-xl bg-white/70 text-gray-900 hover:bg-white/90 dark:bg-black/40 dark:text-white dark:hover:bg-black/70 pointer-coarse:hidden"
            :loading="turning"
            @click="turn(-1)"
          />

          <UButton
            v-if="next.kind !== 'end'"
            icon="i-lucide-chevron-right"
            color="neutral"
            variant="ghost"
            size="xl"
            aria-label="Next photo"
            class="absolute top-1/2 right-3 -translate-y-1/2 rounded-xl bg-white/70 text-gray-900 hover:bg-white/90 dark:bg-black/40 dark:text-white dark:hover:bg-black/70 pointer-coarse:hidden"
            :loading="turning"
            @click="turn(1)"
          />
        </div>

        <aside
          v-if="infoOpen"
          class="flex min-h-0 flex-col border-l border-gray-200/70 text-gray-900 frosted-glass glass-surface-strong max-md:absolute max-md:inset-x-0 max-md:top-16 max-md:bottom-0 max-md:z-20 max-md:border-t max-md:border-l-0 md:w-96 md:shrink-0 dark:border-gray-700/70 dark:text-gray-100"
          aria-label="Photo info"
        >
          <div class="flex items-center gap-2 px-4 py-3">
            <h2 class="m-0 flex-1 text-base font-semibold">Info</h2>

            <UButton
              icon="i-lucide-x"
              color="neutral"
              variant="ghost"
              aria-label="Hide info"
              @click="infoOpen = false"
            />
          </div>

          <div class="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 pb-6">
            <section class="flex flex-col gap-1">
              <p class="m-0 text-sm font-medium break-all">{{ photo.name }}</p>
              <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
                Uploaded {{ uploadedLabel }}
              </p>
            </section>

            <FileCaptureDetails
              :file-id="photo.fileId"
              :version-id="photo.versionId"
              :enabled="infoOpen"
            />
          </div>
        </aside>
      </div>
    </template>
  </UModal>
</template>
