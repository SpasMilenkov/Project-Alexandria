<script setup lang="ts">
import MediaArtwork from "@/components/streaming/overview/MediaArtwork.vue";

const STACK = [
  { seed: "listening-welcome-0", pose: "left-0 top-[20%] -rotate-12" },
  { seed: "listening-welcome-1", pose: "left-[17%] top-[6%] -rotate-2 z-10" },
  { seed: "listening-welcome-2", pose: "left-[34%] top-[22%] rotate-10 z-20" },
] as const;

const STEPS = [
  {
    title: "Listen",
    description:
      "Play music from your library. A song counts once you have listened for 30 seconds.",
  },
  {
    title: "Look back",
    description: "Your timeline fills in day by day, with streaks and busiest days.",
  },
  {
    title: "Relive",
    description: "Each month gets a recap, and each year ends on its own Wrapped record.",
  },
] as const;
</script>

<template>
  <section
    aria-labelledby="listening-welcome-title"
    class="frosted-glass glass-surface-strong flex flex-wrap items-center gap-8 rounded-3xl border border-gray-200/70 p-6 sm:gap-10 sm:p-8 dark:border-gray-700/70"
  >
    <span
      class="relative aspect-square w-48 shrink-0 sm:w-[clamp(220px,13vw,280px)]"
      aria-hidden="true"
    >
      <MediaArtwork
        v-for="card in STACK"
        :key="card.seed"
        :seed="card.seed"
        floating
        class="aspect-square w-[66%] rounded-[20px] border-[3px] border-white shadow-2xl dark:border-neutral-800"
        :class="card.pose"
      >
        <span
          v-if="card.seed === 'listening-welcome-2'"
          class="absolute inset-0 flex items-center justify-center"
        >
          <UIcon name="i-mdi-music-note-outline" class="size-10 text-white drop-shadow-md" />
        </span>
      </MediaArtwork>
    </span>

    <div class="flex min-w-0 flex-[1_1_340px] flex-col gap-3">
      <h1 class="text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400">
        Your listening
      </h1>

      <h2
        id="listening-welcome-title"
        class="text-3xl font-bold leading-tight tracking-tight text-gray-900 xl:text-4xl dark:text-gray-100"
      >
        Your stats start with the first song
      </h2>

      <p class="max-w-xl text-base leading-relaxed text-gray-600 dark:text-gray-400">
        Nothing has been played yet. Press play on anything in your library and this page turns into
        your year in music: hours listened, favorite tracks, recaps and a day-by-day timeline.
      </p>

      <div class="mt-2 flex flex-wrap gap-2">
        <UButton to="/streaming/music" color="primary" size="lg" class="min-h-11 rounded-full px-6">
          Browse music
        </UButton>

        <UButton
          to="/media"
          color="neutral"
          variant="outline"
          size="lg"
          class="min-h-11 rounded-full px-5"
        >
          Open media hub
        </UButton>
      </div>
    </div>

    <ol
      class="flex min-w-0 max-w-md flex-[1_1_300px] flex-col gap-4 rounded-2xl bg-gray-500/10 p-5"
    >
      <li v-for="(step, index) in STEPS" :key="step.title" class="flex items-start gap-3.5">
        <span
          class="flex size-8 shrink-0 items-center justify-center rounded-full border border-gray-300 text-sm font-bold text-gray-900 dark:border-gray-600 dark:text-gray-100"
        >
          {{ index + 1 }}
        </span>

        <span class="min-w-0">
          <span class="block text-sm font-semibold text-gray-900 dark:text-gray-100">
            {{ step.title }}
          </span>

          <span class="mt-0.5 block text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {{ step.description }}
          </span>
        </span>
      </li>
    </ol>
  </section>
</template>
