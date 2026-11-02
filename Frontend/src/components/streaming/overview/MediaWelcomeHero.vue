<script setup lang="ts">
import { RouterLink } from "vue-router";

import MediaArtwork from "./MediaArtwork.vue";
import MediaPolicyHint from "./MediaPolicyHint.vue";

const STACK = [
  { seed: "welcome-0", pose: "left-0 top-[20%] -rotate-12" },
  { seed: "welcome-1", pose: "left-[17%] top-[6%] -rotate-2 z-10" },
  { seed: "welcome-2", pose: "left-[34%] top-[22%] rotate-10 z-20" },
] as const;
</script>

<template>
  <section
    aria-labelledby="media-welcome-title"
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
      />
    </span>

    <div class="flex min-w-0 flex-[1_1_340px] flex-col gap-3">
      <p class="text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400">
        Welcome to your media hub
      </p>

      <h2
        id="media-welcome-title"
        class="text-3xl font-bold leading-tight tracking-tight text-gray-900 xl:text-4xl dark:text-gray-100"
      >
        Your library starts here
      </h2>

      <p class="max-w-xl text-base leading-relaxed text-gray-600 dark:text-gray-400">
        Add music and videos to a folder with a transpilation policy. They are prepared for
        streaming in the background, and this page fills in as you listen.
      </p>

      <div class="mt-2 flex flex-wrap gap-2">
        <UButton to="/dashboard" color="primary" size="lg" class="min-h-11 rounded-full px-6"
          >Open files</UButton
        >

        <UButton
          to="/streaming/jobs"
          color="neutral"
          variant="outline"
          size="lg"
          class="min-h-11 rounded-full px-5"
          >View conversions</UButton
        >
      </div>
    </div>

    <ol
      class="flex min-w-0 max-w-md flex-[1_1_300px] flex-col gap-4 rounded-2xl bg-gray-500/10 p-5"
    >
      <li class="flex items-start gap-3.5">
        <span
          class="flex size-8 shrink-0 items-center justify-center rounded-full border border-gray-300 text-sm font-bold text-gray-900 dark:border-gray-600 dark:text-gray-100"
          >1</span
        >

        <span class="min-w-0">
          <span class="block text-sm font-semibold text-gray-900 dark:text-gray-100">Upload</span>

          <span class="mt-0.5 block text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            Drop audio or video files in a directory with a <MediaPolicyHint />.
          </span>
        </span>
      </li>

      <li class="flex items-start gap-3.5">
        <span
          class="flex size-8 shrink-0 items-center justify-center rounded-full border border-gray-300 text-sm font-bold text-gray-900 dark:border-gray-600 dark:text-gray-100"
          >2</span
        >

        <span class="min-w-0">
          <span class="block text-sm font-semibold text-gray-900 dark:text-gray-100"
            >Conversion</span
          >

          <span class="mt-0.5 block text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            Each file is prepared for smooth streaming. Follow along under
            <RouterLink
              to="/streaming/jobs"
              class="font-semibold text-gray-900 underline underline-offset-4 dark:text-gray-100"
              >Conversions</RouterLink
            >.
          </span>
        </span>
      </li>

      <li class="flex items-start gap-3.5">
        <span
          class="flex size-8 shrink-0 items-center justify-center rounded-full border border-gray-300 text-sm font-bold text-gray-900 dark:border-gray-600 dark:text-gray-100"
          >3</span
        >

        <span class="min-w-0">
          <span class="block text-sm font-semibold text-gray-900 dark:text-gray-100">Play</span>

          <span class="mt-0.5 block text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            Your favorites, history and collections start appearing here.
          </span>
        </span>
      </li>
    </ol>
  </section>
</template>
