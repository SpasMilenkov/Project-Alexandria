<script setup lang="ts">
import { computed } from "vue";
import type { HomeWidget } from "@/types/home";
import { HomeShortcutGroup } from "@/enums/home-widget";

const { widget } = defineProps<{ widget: HomeWidget }>();
const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

const fileLinks = [
  {
    label: "File Explorer",
    description: "Browse your files and folders",
    to: "/dashboard",
    icon: "i-heroicons-folder",
  },
  {
    label: "Tags and Categories",
    description: "Find a collection by its tags",
    to: "/dashboard/tags",
    icon: "i-heroicons-tag",
  },
  {
    label: "Access History",
    description: "See your recent file activity",
    to: "/access-history",
    icon: "i-heroicons-clock",
  },
  {
    label: "Your Storage",
    description: "Review your usage and quota",
    to: "/my-storage",
    icon: "i-heroicons-archive-box",
  },
];

const mediaLinks = [
  {
    label: "Music",
    description: "Listen to your music library",
    to: "/streaming/music",
    icon: "i-heroicons-musical-note",
  },
  {
    label: "Videos",
    description: "Watch your video library",
    to: "/streaming/videos",
    icon: "i-heroicons-film",
  },
  {
    label: "Playlists",
    description: "Explore your collections",
    to: "/streaming/playlists",
    icon: "i-heroicons-list-bullet",
  },
  {
    label: "Your listening",
    description: "Timeline, recaps and Wrapped",
    to: "/stats",
    icon: "i-heroicons-chart-bar",
  },
];

const links = computed(() => {
  let source = fileLinks;

  if (widget.options.shortcutGroup === HomeShortcutGroup.Media) {
    source = streamingEnabled ? mediaLinks : [];
  }

  return source;
});
</script>

<template>
  <div v-if="links.length" class="min-w-0 flex-1">
    <div class="shortcut-grid">
      <RouterLink
        v-for="link in links"
        :key="link.to"
        :to="link.to"
        :aria-label="link.label"
        :title="link.label"
        class="shortcut-link flex min-w-0 items-center gap-2 rounded-xl border border-gray-200/70 p-2 transition-colors hover:bg-gray-100/50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:border-gray-700/70 dark:hover:bg-gray-800/50 motion-reduce:transition-none"
      >
        <UIcon :name="link.icon" class="size-5 shrink-0 text-gray-600 dark:text-gray-400" />

        <div class="min-w-0">
          <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
            {{ link.label }}
          </p>
          <p class="shortcut-description mt-1 text-xs text-gray-600 dark:text-gray-400">
            {{ link.description }}
          </p>
        </div>
      </RouterLink>
    </div>
  </div>

  <div v-else class="flex flex-col items-center gap-2 py-6 text-center">
    <UIcon name="i-heroicons-musical-note" class="size-12 text-gray-400 dark:text-gray-600" />
    <p class="font-medium text-gray-900 dark:text-gray-100">Streaming is unavailable</p>
    <p class="text-sm text-gray-600 dark:text-gray-400">
      Choose file shortcuts while streaming is disabled.
    </p>

    <UButton to="/dashboard" color="neutral" variant="outline">Open File Explorer</UButton>
  </div>
</template>

<style scoped>
.shortcut-grid {
  display: grid;
  gap: 0.5rem;
}

.shortcut-link {
  min-height: 2rem;
}

.shortcut-description {
  display: none;
}

@container home-widget (min-width: 360px) {
  .shortcut-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@container home-widget (max-width: 359px) and (max-height: 224px) {
  .shortcut-link:nth-child(n + 3) {
    display: none;
  }
}

@container home-widget (min-width: 760px) and (max-height: 224px) {
  .shortcut-grid {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}

@container home-widget (min-height: 300px) {
  .shortcut-grid {
    gap: 1rem;
  }

  .shortcut-link {
    padding: 1rem;
  }
}

@container home-widget (min-width: 480px) and (min-height: 300px) {
  .shortcut-description {
    display: block;
  }
}
</style>
