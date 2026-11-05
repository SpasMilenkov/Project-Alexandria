<script setup lang="ts">
import { RouterLink } from "vue-router";
</script>

<template>
  <div class="absolute inset-0 flex items-center gap-4 px-4 md:gap-12 md:px-[6%]">
    <div class="min-w-0 max-w-xl flex-1">
      <p class="mb-1.5 text-sm text-gray-300">Your videos</p>

      <h2 class="text-[clamp(24px,3.4vw,40px)] leading-[1.1] font-bold tracking-tight text-white">
        Nothing to watch yet
      </h2>

      <p class="mt-2 mb-5 max-w-md text-sm leading-relaxed text-gray-300">
        Videos land here once they are converted for streaming. Add them to a folder with a
        Transcode rule and conversion starts on its own.
      </p>

      <div class="flex flex-wrap gap-2">
        <RouterLink
          to="/dashboard"
          class="inline-flex h-10 items-center gap-2 rounded-[var(--ui-radius)] bg-primary px-4 text-sm font-semibold text-white transition hover:brightness-110 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        >
          <UIcon name="i-mdi-folder-outline" class="size-4" aria-hidden="true" />
          Open files
        </RouterLink>

        <RouterLink
          to="/streaming/jobs"
          class="inline-flex h-10 items-center rounded-[10px] border border-white/25 px-4 text-sm font-semibold text-white transition-colors hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        >
          View conversions
        </RouterLink>
      </div>
    </div>

    <span class="hidden flex-[0_0_min(44%,520px)] sm:block" aria-hidden="true">
      <span
        class="flex aspect-video w-full items-center justify-center rounded-[14px] border border-white/15 bg-white/5"
      >
        <span
          class="flex size-16 items-center justify-center rounded-full border border-white/30 bg-black/45 text-white"
        >
          <UIcon name="i-mdi-film-open-outline" class="size-7" />
        </span>
      </span>
    </span>
  </div>
</template>
