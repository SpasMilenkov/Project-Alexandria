<template>
  <div :class="variant === 'panel' ? 'flex flex-col gap-4' : 'grid gap-3 sm:gap-4 md:grid-cols-2'">
    <div
      v-if="variant === 'panel'"
      class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400"
      aria-hidden="true"
    >
      <UIcon name="i-lucide-house" class="h-3.5 w-3.5" />
      <span>Files</span>
      <UIcon name="i-lucide-chevron-right" class="h-3.5 w-3.5" />
      <span class="font-medium text-gray-900 dark:text-gray-100">Summer trip</span>
    </div>

    <div>
      <p class="mb-2 text-xs font-medium uppercase tracking-wide text-gray-600 dark:text-gray-400">
        Grid
      </p>
      <div :class="gridClass">
        <div
          v-for="item in items"
          :key="item.name"
          class="flex min-w-0 flex-col items-center gap-1.5 rounded-lg px-2 py-2"
          :class="{
            'bg-primary/10 outline outline-1 -outline-offset-1 outline-primary': item.selected,
          }"
        >
          <span
            v-if="item.kind === 'image' && thumbnailsEnabled"
            class="preview-thumb rounded-md"
            :style="gridThumbStyle"
          />
          <span
            v-else
            class="preview-size-anim grid place-items-center rounded-lg bg-gray-500/[0.07] text-gray-600 dark:bg-white/[0.06] dark:text-gray-400"
            :style="gridIconStyle"
          >
            <UIcon :name="item.icon" class="h-[55%] w-[55%]" />
          </span>
          <span class="max-w-24 truncate text-[11px] text-gray-600 dark:text-gray-400">
            {{ item.name }}
          </span>
        </div>
      </div>
    </div>

    <div>
      <p class="mb-2 text-xs font-medium uppercase tracking-wide text-gray-600 dark:text-gray-400">
        List
      </p>
      <div class="flex flex-col gap-1">
        <div
          v-for="item in items"
          :key="item.name"
          class="preview-row-anim flex items-center gap-2.5 rounded-lg px-2"
          :class="{ 'bg-primary/10': item.selected }"
          :style="listRowStyle"
        >
          <UIcon
            :name="item.icon"
            class="preview-size-anim shrink-0 text-gray-600 dark:text-gray-400"
            :style="listIconStyle"
          />
          <span class="min-w-0 flex-1 truncate text-xs text-gray-900 dark:text-gray-100">
            {{ item.name }}
          </span>
          <span class="flex-none text-[11px] text-gray-600 dark:text-gray-400">
            {{ item.size }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

import { useSettingsStore } from "@/stores/settings";

interface PreviewItem {
  name: string;
  icon: string;
  kind: "image" | "folder" | "document" | "audio";
  size: string;
  selected: boolean;
}

const { variant = "compact" } = defineProps<{
  variant?: "compact" | "panel";
}>();

// Fixed sample files: the preview demonstrates tile size, row size, thumbnails and the
// accent selection, so plain markup bound to the store beats a full file tile with menus.
const SAMPLE_ITEMS: PreviewItem[] = [
  {
    icon: "i-lucide-image",
    kind: "image",
    name: "sunset-harbor.jpg",
    selected: true,
    size: "2.4 MB",
  },
  {
    icon: "i-lucide-file-text",
    kind: "document",
    name: "project-specification.pdf",
    selected: false,
    size: "1.05 MB",
  },
  {
    icon: "i-lucide-folder",
    kind: "folder",
    name: "Ferry photos",
    selected: false,
    size: "18 items",
  },
  {
    icon: "i-lucide-music",
    kind: "audio",
    name: "harbor-ambience.flac",
    selected: false,
    size: "32 MB",
  },
];

const settingsStore = useSettingsStore();

const thumbnailsEnabled = computed(() => settingsStore.thumbnailsEnabled);

const items = computed(() => {
  if (variant === "panel") return SAMPLE_ITEMS;

  return SAMPLE_ITEMS.slice(0, 2);
});

const gridClass = computed(() => {
  if (variant === "panel")
    return "grid grid-cols-[repeat(auto-fill,minmax(6.5rem,1fr))] items-end gap-2";

  return "flex items-end justify-center gap-3";
});

const gridIconStyle = computed(() => ({
  height: `${settingsStore.gridIconSize}px`,
  width: `${settingsStore.gridIconSize}px`,
}));

const gridThumbStyle = computed(() => ({
  height: `${Math.round(settingsStore.gridIconSize * 1.125)}px`,
  width: `${Math.round(settingsStore.gridIconSize * 1.5)}px`,
}));

const listIconStyle = computed(() => ({
  height: `${settingsStore.listIconSize}px`,
  width: `${settingsStore.listIconSize}px`,
}));

const listRowStyle = computed(() => {
  const padding = 4 + Math.round(settingsStore.listIconSize * 0.25);

  return {
    paddingBottom: `${padding}px`,
    paddingTop: `${padding}px`,
  };
});
</script>

<style scoped>
.preview-thumb {
  display: block;
  transition:
    width 200ms ease-out,
    height 200ms ease-out;
  background:
    radial-gradient(circle at 74% 28%, #fde68a 0 12%, transparent 13%),
    linear-gradient(160deg, #38bdf8, #6366f1 55%, #f472b6);
}

.preview-size-anim {
  transition:
    width 150ms ease-out,
    height 150ms ease-out;
}

.preview-row-anim {
  transition: padding 150ms ease-out;
}

@media (prefers-reduced-motion: reduce) {
  .preview-thumb,
  .preview-size-anim,
  .preview-row-anim {
    transition: none;
  }
}
</style>
