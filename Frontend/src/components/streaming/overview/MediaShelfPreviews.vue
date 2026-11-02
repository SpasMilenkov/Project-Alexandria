<script setup lang="ts">
import MediaCoverFan from "./MediaCoverFan.vue";

const PREVIEWS = [
  {
    title: "Fresh in your library",
    description: "New uploads show up the moment they are ready to play.",
    seed: "preview-fresh",
    wide: false,
  },
  {
    title: "Jump back in",
    description: "Songs you loved come back around after a month away.",
    seed: "preview-jump-back-in",
    wide: false,
  },
  {
    title: "Made from your library",
    description: "Collections by artist, album, genre and decade, built automatically.",
    seed: "preview-collections",
    wide: false,
  },
  {
    title: "Videos",
    description: "Every video remembers where you stopped.",
    seed: "preview-videos",
    wide: true,
  },
] as const;
</script>

<template>
  <section
    aria-labelledby="media-previews-title"
    class="frosted-glass glass-surface rounded-3xl border border-gray-200/70 p-5 sm:p-6 dark:border-gray-700/70"
  >
    <h2
      id="media-previews-title"
      class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100"
    >
      What you will find here
    </h2>

    <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
      Every shelf builds itself from your files. Nothing to set up.
    </p>

    <ul class="mt-5 grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4">
      <li
        v-for="preview in PREVIEWS"
        :key="preview.seed"
        class="group flex flex-col gap-3 rounded-2xl bg-gray-500/10 p-5"
      >
        <MediaCoverFan :fallback-seed="preview.seed" size="lg" :wide="preview.wide" />

        <p class="text-base font-semibold text-gray-900 dark:text-gray-100">{{ preview.title }}</p>

        <p class="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
          {{ preview.description }}
        </p>
      </li>
    </ul>
  </section>
</template>
