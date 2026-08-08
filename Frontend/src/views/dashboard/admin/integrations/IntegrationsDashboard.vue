<template>
  <div class="flex flex-col min-h-screen px-6 py-10">
    <header class="max-w-7xl mx-auto w-full mb-8">
      <h1 class="font-playfair text-4xl font-bold tracking-tight leading-none mb-2">
        Integrations
      </h1>
      <p class="text-base text-gray-600 dark:text-gray-400">
        Shipped services and their admin monitoring pages
      </p>
    </header>

    <main class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 max-w-7xl mx-auto w-full">
      <UCard
        v-for="integration in integrations"
        :key="integration.id"
        :as="integration.route ? 'RouterLink' : 'div'"
        @click="integration.route && router.push(integration.route)"
        :ui="{
          root: 'nav-card group relative overflow-hidden rounded-sm border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm hover:bg-white/75 dark:hover:bg-white/10 transition-all duration-200 no-underline text-inherit cursor-pointer',
          body: 'flex flex-col gap-4 p-5',
        }"
      >
        <span
          class="absolute top-0 left-0 right-0 h-0.5 bg-black/10 dark:bg-white/10 group-hover:bg-black/20 dark:group-hover:bg-white/20 transition-colors"
        />

        <div class="flex items-start justify-between gap-2 mt-1">
          <div class="p-2 rounded-sm border border-dashed border-black/15 dark:border-white/15">
            <UIcon :name="integration.icon" class="w-5 h-5 text-primary" />
          </div>
          <UBadge
            :color="integration.live ? 'success' : 'neutral'"
            :variant="integration.live ? 'subtle' : 'outline'"
            size="sm"
            class="rounded-sm text-[0.58rem] tracking-wider"
          >
            {{ integration.live ? "Live stats" : "Admin stats planned" }}
          </UBadge>
        </div>

        <div class="flex-1">
          <p class="text-[0.6rem] uppercase tracking-[0.15em] text-gray-500 dark:text-gray-400 mb-1">
            {{ integration.chapter }}
          </p>
          <h2 class="font-playfair text-xl font-semibold leading-snug mb-2">
            {{ integration.title }}
          </h2>
          <p class="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {{ integration.description }}
          </p>
        </div>

        <USeparator :ui="{ root: 'border-dashed opacity-40' }" />
        <div class="flex items-center gap-1">
          <span
            v-for="(tag, i) in integration.tags"
            :key="tag"
            class="text-[0.62rem] italic text-gray-500 dark:text-gray-400"
            >{{ tag }}{{ i < integration.tags.length - 1 ? " ·" : "" }}</span
          >
          <UIcon
            v-if="integration.route"
            name="mdi:arrow-right-thin"
            class="w-4 h-4 ml-auto text-gray-400 dark:text-gray-500 group-hover:translate-x-0.5 transition-all"
          />
        </div>
      </UCard>
    </main>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from "vue-router";

const router = useRouter();

const integrations = [
  {
    chapter: "Signal Analysis",
    description:
      "Queue depth, failure rates, durations, and batch history for the audio analysis worker.",
    icon: "mdi:waveform",
    id: "audio-analysis",
    live: true,
    route: "/dashboard/admin/integrations/audio-analysis",
    tags: ["Queue", "Failure rate", "Batches"],
    title: "Audio Analysis",
  },
  {
    chapter: "Media Processing",
    description: "Inspect and manage transcoding jobs across audio and video rungs.",
    icon: "mdi:file-cog-outline",
    id: "transpilations",
    live: false,
    route: "/streaming/jobs",
    tags: ["Rungs", "Status", "Retries"],
    title: "Transpilations",
  },
  {
    chapter: "Classification",
    description: "Auto-derived genre and mood tags from audio analysis output (audio only).",
    icon: "mdi:tag-multiple-outline",
    id: "auto-tagging",
    live: false,
    route: "",
    tags: ["Genre", "Mood", "Confidence"],
    title: "Auto-tagging",
  },
  {
    chapter: "Transcription",
    description: "Tracked lyrics and synchronized transcription for streamed media.",
    icon: "mdi:music-note-text",
    id: "lyrics",
    live: false,
    route: "",
    tags: ["Lyrics", "Timing", "Synced"],
    title: "Lyrics",
  },
] as const;
</script>

<style scoped>
.nav-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 16px color-mix(in srgb, currentColor 5%, transparent);
}
.nav-card:active {
  transform: translateY(0);
}
</style>
