<script setup lang="ts">
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import { useHomeStore } from "@/stores/home";
import { homeLibraryLinks } from "@/utils/home-library";

import HomeLibraryDisplay from "./HomeLibraryDisplay.vue";

const { widget } = defineProps<{ widget: HomeWidget }>();
const home = useHomeStore();
const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

const links = computed(() => homeLibraryLinks(widget.options, streamingEnabled));
</script>

<template>
  <HomeLibraryDisplay
    v-if="links.length"
    :links="links"
    :library-style="widget.options.libraryStyle"
    :featured-destination="widget.options.featuredDestination"
    :disabled="home.isEditing"
  />

  <div v-else class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center">
    <UIcon
      name="i-heroicons-musical-note"
      class="size-8 shrink-0 text-gray-400 dark:text-gray-600"
    />
    <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Streaming is unavailable</p>
    <p class="library-unavailable-detail text-xs text-gray-600 dark:text-gray-400">
      Choose file shortcuts while streaming is disabled.
    </p>

    <UButton
      class="library-unavailable-detail"
      to="/dashboard"
      color="neutral"
      variant="outline"
      size="sm"
      :disabled="home.isEditing"
      >Open File Explorer</UButton
    >
  </div>
</template>

<style scoped>
@container home-widget (max-height: 200px) {
  .library-unavailable-detail {
    display: none;
  }
}
</style>
