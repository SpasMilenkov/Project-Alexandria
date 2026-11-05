<script setup lang="ts">
import { computed } from "vue";

import MediaArtwork from "@/components/streaming/overview/MediaArtwork.vue";
import MediaPolicyHint from "@/components/streaming/overview/MediaPolicyHint.vue";

const { mediaType } = defineProps<{ mediaType: "video" | "audio" }>();

const COPY = {
  audio: {
    eyebrow: "Your music",
    title: "Nothing to play yet",
    description: "Songs show up here as soon as they are ready to stream.",
    noun: "audio",
    icon: "i-mdi-music-note-outline",
  },
  video: {
    eyebrow: "Your videos",
    title: "No videos yet",
    description: "Videos show up here as soon as they are ready to stream.",
    noun: "video",
    icon: "i-mdi-film-open-outline",
  },
} as const;

const FRAME = {
  audio: "aspect-square w-40 sm:w-48",
  video: "aspect-[4/3] w-48 sm:w-56",
} as const;

const CARD = {
  audio: "aspect-square w-[66%] rounded-[20px]",
  video: "aspect-video w-[72%] rounded-2xl",
} as const;

const POSES = [
  "left-0 top-[20%] -rotate-12 group-hover:-translate-x-1.5 group-hover:-rotate-[18deg]",
  "left-[17%] top-[6%] -rotate-2 z-10 group-hover:-translate-y-1.5",
  "left-[34%] top-[22%] rotate-10 z-20 group-hover:translate-x-1.5 group-hover:rotate-[16deg]",
] as const;

const copy = computed(() => COPY[mediaType]);

const cards = computed(() =>
  POSES.map((pose, index) => ({
    seed: `library-empty-${mediaType}-${index}`,
    classes: [CARD[mediaType], pose],
    front: index === POSES.length - 1,
  })),
);

const steps = computed(() => [
  {
    icon: "i-mdi-upload-outline",
    title: "Upload",
    description: `Add ${copy.value.noun} files to a folder in Files.`,
  },
  {
    icon: "i-mdi-tune-variant",
    title: "Convert",
    description: "A Transcode rule prepares each file in the background.",
  },
  {
    icon: "i-mdi-play-circle-outline",
    title: "Play",
    description: "Finished files land here, ready to stream.",
  },
]);
</script>

<template>
  <section
    aria-labelledby="media-library-empty-title"
    class="group frosted-glass glass-surface-strong flex w-full max-w-4xl flex-col gap-8 rounded-3xl border border-gray-200/70 p-6 shadow-xl sm:p-8 dark:border-gray-700/70"
  >
    <div class="flex flex-wrap items-center justify-center gap-8 sm:gap-10">
      <span class="relative shrink-0" :class="FRAME[mediaType]" aria-hidden="true">
        <MediaArtwork
          v-for="card in cards"
          :key="card.seed"
          :seed="card.seed"
          floating
          class="border-[3px] border-white shadow-2xl transition-transform duration-200 ease-out motion-reduce:transition-none dark:border-neutral-800"
          :class="card.classes"
        >
          <span v-if="card.front" class="absolute inset-0 flex items-center justify-center">
            <UIcon :name="copy.icon" class="size-10 text-white drop-shadow-md" />
          </span>
        </MediaArtwork>
      </span>

      <div class="flex min-w-0 flex-[1_1_320px] flex-col gap-3">
        <p class="text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400">
          {{ copy.eyebrow }}
        </p>

        <h2
          id="media-library-empty-title"
          class="text-2xl font-bold leading-tight tracking-tight text-gray-900 sm:text-3xl dark:text-gray-100"
        >
          {{ copy.title }}
        </h2>

        <p class="max-w-lg text-base leading-relaxed text-gray-600 dark:text-gray-400">
          {{ copy.description }} Drop {{ copy.noun }} files into a folder with a
          <MediaPolicyHint /> and they are converted automatically.
        </p>

        <div class="mt-2 flex flex-wrap gap-2">
          <UButton to="/dashboard" color="primary" size="lg" class="min-h-11 rounded-full px-6">
            Open files
          </UButton>

          <UButton
            to="/streaming/jobs"
            color="neutral"
            variant="outline"
            size="lg"
            class="min-h-11 rounded-full px-5"
          >
            View conversions
          </UButton>
        </div>
      </div>
    </div>

    <ol class="grid gap-3 sm:grid-cols-3">
      <li
        v-for="(step, index) in steps"
        :key="step.title"
        class="flex items-start gap-3 rounded-2xl bg-gray-500/10 p-4"
      >
        <span
          class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-white text-gray-900 shadow-sm dark:bg-neutral-800 dark:text-gray-100"
        >
          <UIcon :name="step.icon" class="size-5" aria-hidden="true" />
        </span>

        <span class="min-w-0">
          <span class="block text-sm font-semibold text-gray-900 dark:text-gray-100">
            {{ index + 1 }}. {{ step.title }}
          </span>

          <span class="mt-0.5 block text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {{ step.description }}
          </span>
        </span>
      </li>
    </ol>
  </section>
</template>
