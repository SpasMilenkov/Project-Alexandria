<script setup lang="ts">
import LyricsGhost from "./LyricsGhost.vue";

const {
  icon = undefined,
  title,
  description = undefined,
  ghost = true,
} = defineProps<{
  icon?: string;
  title: string;
  description?: string;
  ghost?: boolean;
}>();
</script>

<template>
  <div class="relative flex min-h-0 flex-1 overflow-hidden">
    <!-- The ghost fades out around the message so it hints at lyrics without competing with the text. -->
    <div
      v-if="ghost"
      class="lyrics-ghost-mask pointer-events-none absolute inset-0 select-none"
      aria-hidden="true"
    >
      <LyricsGhost />
    </div>

    <div
      class="relative m-auto flex max-w-xs flex-col items-center gap-3 px-6 py-8 text-center"
      role="status"
    >
      <span
        class="flex size-12 items-center justify-center rounded-2xl bg-gray-500/15 text-gray-900 dark:text-gray-100"
        aria-hidden="true"
      >
        <slot name="icon">
          <UIcon v-if="icon" :name="icon" class="size-6" />
        </slot>
      </span>

      <h3 class="text-base font-semibold tracking-tight text-gray-900 dark:text-gray-100">
        {{ title }}
      </h3>

      <p
        v-if="description || $slots.description"
        class="text-sm leading-relaxed text-gray-600 dark:text-gray-400"
      >
        <slot name="description">{{ description }}</slot>
      </p>

      <div v-if="$slots.default" class="mt-1 flex flex-wrap justify-center gap-2">
        <slot />
      </div>
    </div>
  </div>
</template>

<style scoped>
.lyrics-ghost-mask {
  mask-image:
    radial-gradient(ellipse 70% 45% at center, transparent 35%, black 100%),
    linear-gradient(to bottom, black 40%, transparent 95%);
  mask-composite: intersect;
}
</style>
