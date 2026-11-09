<script setup lang="ts">
const POSES = [
  "left-0 top-[18%] -rotate-12 group-hover:-translate-x-1.5 group-hover:-rotate-[18deg]",
  "left-[17%] top-[6%] -rotate-2 z-10 group-hover:-translate-y-1.5",
  "left-[34%] top-[20%] rotate-10 z-20 group-hover:translate-x-1.5 group-hover:rotate-[16deg]",
] as const;

const SHADES = [
  "bg-gray-300 dark:bg-neutral-700",
  "bg-gray-200 dark:bg-neutral-600",
  "bg-gray-100 dark:bg-neutral-500",
] as const;

const frames = POSES.map((pose, index) => ({
  classes: [pose, SHADES[index]],
  front: index === POSES.length - 1,
  key: `gallery-empty-${index}`,
}));
</script>

<template>
  <section
    aria-labelledby="gallery-empty-title"
    class="group flex w-full max-w-3xl flex-col gap-8 rounded-3xl border border-gray-200/70 p-6 shadow-xl frosted-glass glass-surface-strong sm:p-8 dark:border-gray-700/70"
  >
    <div class="flex flex-wrap items-center justify-center gap-8 sm:gap-10">
      <span class="relative aspect-[4/3] w-48 shrink-0 sm:w-56" aria-hidden="true">
        <span
          v-for="frame in frames"
          :key="frame.key"
          class="absolute aspect-[4/3] w-[66%] rounded-2xl border-[3px] border-white shadow-2xl transition-transform duration-200 ease-out motion-reduce:transition-none dark:border-neutral-800"
          :class="frame.classes"
        >
          <span v-if="frame.front" class="absolute inset-0 flex items-center justify-center">
            <UIcon name="i-lucide-images" class="size-10 text-gray-600 dark:text-gray-200" />
          </span>
        </span>
      </span>

      <div class="flex min-w-0 flex-[1_1_320px] flex-col gap-3">
        <p class="text-xs font-semibold tracking-widest text-gray-600 uppercase dark:text-gray-400">
          Your photos
        </p>

        <h2
          id="gallery-empty-title"
          class="text-2xl leading-tight font-bold tracking-tight text-gray-900 sm:text-3xl dark:text-gray-100"
        >
          No photos yet
        </h2>

        <p class="max-w-lg text-base leading-relaxed text-gray-600 dark:text-gray-400">
          Images you upload appear here as a timeline. Photos from a phone or camera are placed by
          the day they were taken, everything else by the day it was uploaded.
        </p>

        <div class="mt-2 flex flex-wrap gap-2">
          <UButton to="/dashboard" color="primary" size="lg" class="min-h-11 rounded-full px-6">
            Open files
          </UButton>
        </div>
      </div>
    </div>
  </section>
</template>
