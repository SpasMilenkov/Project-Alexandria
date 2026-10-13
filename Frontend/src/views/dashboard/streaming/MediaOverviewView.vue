<script setup lang="ts">
import { computed } from "vue";

import MediaOverviewContent from "@/components/streaming/overview/MediaOverviewContent.vue";
import { useAuthStore } from "@/stores/auth";

const auth = useAuthStore();
const userId = computed(() => auth.user?.user.id);
const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

const destinations = [
  { label: "Music", icon: "i-mdi-music-note-outline", to: "/streaming/music" },
  { label: "Videos", icon: "i-mdi-film-open-outline", to: "/streaming/videos" },
  { label: "Playlists", icon: "i-mdi-playlist-music", to: "/streaming/playlists" },
  { label: "Your listening", icon: "i-mdi-chart-line", to: "/stats" },
  { label: "History", icon: "i-mdi-history", to: "/streaming/history" },
  { label: "Jobs", icon: "i-mdi-luggage", to: "/streaming/jobs" },
];
</script>

<template>
  <main class="w-full space-y-8 px-4 py-6 md:px-6 md:py-8" aria-labelledby="media-title">
    <header class="flex flex-wrap items-start justify-between gap-4">
      <div>
        <p
          class="mb-2 text-xs font-semibold uppercase tracking-widest text-gray-500 dark:text-gray-500"
        >
          Your library, ready to play
        </p>
        <h1
          id="media-title"
          class="text-3xl font-bold tracking-tight text-gray-900 dark:text-gray-100"
        >
          Media
        </h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Recently added, collected, and played.
        </p>
      </div>

      <UButton
        v-if="streamingEnabled"
        to="/streaming/music"
        color="primary"
        size="lg"
        class="min-h-11"
        >Browse music</UButton
      >
    </header>

    <template v-if="streamingEnabled">
      <nav
        aria-label="Media destinations"
        class="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6"
      >
        <UButton
          v-for="item in destinations"
          :key="item.to"
          :to="item.to"
          :icon="item.icon"
          color="neutral"
          variant="outline"
          class="frosted-glass glass-surface min-h-12 justify-start"
          >{{ item.label }}</UButton
        >
      </nav>

      <MediaOverviewContent v-if="userId" :key="userId" :user-id="userId" />
    </template>

    <div
      v-else
      class="frosted-glass glass-surface flex min-h-64 flex-col items-center justify-center gap-4 rounded-2xl border border-gray-200/70 p-6 text-center dark:border-gray-700/70"
    >
      <UIcon name="i-mdi-music-note-off-outline" class="size-12 text-gray-400 dark:text-gray-600" />
      <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">
        Media streaming is unavailable
      </h2>
      <p class="text-sm text-gray-600 dark:text-gray-400">
        You can still browse and manage your files.
      </p>
      <UButton to="/dashboard" color="neutral" variant="outline" class="min-h-11"
        >Open files</UButton
      >
    </div>
  </main>
</template>
