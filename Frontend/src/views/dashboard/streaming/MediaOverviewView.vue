<script setup lang="ts">
import { computed } from "vue";

import MediaOverviewContent from "@/components/streaming/overview/MediaOverviewContent.vue";
import { useAuthStore } from "@/stores/auth";

const auth = useAuthStore();
const userId = computed(() => auth.user?.user.id);
const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";
</script>

<template>
  <main class="w-full px-4 py-6 md:px-6 md:py-8" aria-labelledby="media-title">
    <template v-if="streamingEnabled">
      <MediaOverviewContent v-if="userId" :key="userId" :user-id="userId" />
    </template>

    <div v-else class="space-y-6">
      <h1
        id="media-title"
        class="text-3xl font-bold tracking-tight text-gray-900 dark:text-gray-100"
      >
        Media
      </h1>

      <div
        class="frosted-glass glass-surface flex min-h-64 flex-col items-center justify-center gap-4 rounded-3xl border border-gray-200/70 p-6 text-center dark:border-gray-700/70"
      >
        <UIcon
          name="i-mdi-music-note-off-outline"
          class="size-12 text-gray-400 dark:text-gray-600"
        />

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
    </div>
  </main>
</template>
