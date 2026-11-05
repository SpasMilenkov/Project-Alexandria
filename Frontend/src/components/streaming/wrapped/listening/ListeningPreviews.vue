<script setup lang="ts">
import MediaCoverFan from "@/components/streaming/overview/MediaCoverFan.vue";

const PREVIEWS = [
  {
    title: "Your year at a glance",
    description: "Hours listened, plays, listening days and the song you keep coming back to.",
    seed: "listening-preview-year",
  },
  {
    title: "Monthly recaps",
    description: "This month and last, each with its top tracks and listening rhythm.",
    seed: "listening-preview-months",
  },
  {
    title: "Annual Wrapped",
    description: "When a year ends it is pressed onto one record you can replay and export.",
    seed: "listening-preview-wrapped",
  },
  {
    title: "Listening timeline",
    description: "A day-by-day map of the year, with your streaks and busiest days.",
    seed: "listening-preview-timeline",
  },
] as const;
</script>

<template>
  <section
    aria-labelledby="listening-previews-title"
    class="frosted-glass glass-surface rounded-3xl border border-gray-200/70 p-5 sm:p-6 dark:border-gray-700/70"
  >
    <h2
      id="listening-previews-title"
      class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100"
    >
      What you will find here
    </h2>

    <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
      Everything builds itself from what you play. Nothing to set up.
    </p>

    <ul class="mt-5 grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4">
      <li
        v-for="preview in PREVIEWS"
        :key="preview.seed"
        class="group flex flex-col gap-3 rounded-2xl bg-gray-500/10 p-5"
      >
        <MediaCoverFan :fallback-seed="preview.seed" size="lg" />

        <p class="text-base font-semibold text-gray-900 dark:text-gray-100">{{ preview.title }}</p>

        <p class="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
          {{ preview.description }}
        </p>
      </li>
    </ul>
  </section>
</template>
