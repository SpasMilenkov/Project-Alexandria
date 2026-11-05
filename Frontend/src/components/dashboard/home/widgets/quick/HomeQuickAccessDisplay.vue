<script setup lang="ts">
import type { DirectorySummaryDto } from "@/api/directory";

import { HomeQuickAccessStyle } from "@/enums/home-widget";

defineProps<{
  folders: DirectorySummaryDto[];
  accessStyle: HomeQuickAccessStyle;
  disabled?: boolean;
}>();

const emit = defineEmits<{ open: [id: string, name: string] }>();
</script>

<template>
  <div class="access-body min-h-0 min-w-0 flex-1 overflow-auto" :data-access-style="accessStyle">
    <div
      class="access-items"
      :class="{
        'access-grid': accessStyle === HomeQuickAccessStyle.Tiles,
        'access-list': accessStyle === HomeQuickAccessStyle.List,
        'access-chips': accessStyle === HomeQuickAccessStyle.Chips,
      }"
    >
      <button
        v-for="folder in folders"
        :key="folder.id"
        type="button"
        :aria-label="`Open ${folder.name}`"
        :title="folder.name"
        :disabled="disabled"
        class="access-folder min-w-0 text-left text-gray-900 transition-colors focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-primary dark:text-gray-100 motion-reduce:transition-none"
        @click="emit('open', folder.id, folder.name)"
      >
        <UIcon
          name="i-heroicons-folder"
          class="folder-icon shrink-0 text-gray-600 dark:text-gray-400"
        />

        <span class="folder-name truncate text-sm font-medium">{{ folder.name }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.access-items {
  gap: 0.5rem;
}

.access-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 104px), 1fr));
  grid-auto-rows: 104px;
  align-content: start;
}

.access-folder {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  border-radius: 0.75rem;
}

.access-grid .access-folder {
  max-width: 176px;
  flex-direction: column;
  align-items: flex-start;
  justify-content: space-between;
  padding: 1rem;
  background: var(--ui-bg-elevated);
}

.folder-icon {
  width: 1.5rem;
  height: 1.5rem;
}

.folder-name {
  max-width: 100%;
}

.access-folder:hover {
  background: var(--ui-bg-accented);
}

.access-list {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.access-list .access-folder {
  min-height: 3rem;
  padding: 0.5rem;
}

.access-list .access-folder + .access-folder {
  border-top: 1px solid var(--ui-border-muted);
}

.access-chips {
  display: flex;
  flex-wrap: wrap;
  align-content: flex-start;
}

.access-chips .access-folder {
  max-width: 100%;
  padding: 0.5rem 1rem;
  border-radius: 999px;
  background: var(--ui-bg-elevated);
}

.access-chips .folder-icon {
  width: 1rem;
  height: 1rem;
}

@container home-widget (max-width: 220px) {
  .access-grid {
    grid-template-columns: repeat(auto-fill, minmax(min(100%, 72px), 1fr));
    grid-auto-rows: 80px;
  }

  .access-grid .access-folder {
    padding: 0.5rem;
  }

  .folder-name {
    font-size: 0.75rem;
  }
}

@container home-widget (min-width: 480px) and (min-height: 300px) {
  .access-grid {
    grid-template-columns: repeat(auto-fill, minmax(136px, 1fr));
    grid-auto-rows: 128px;
    gap: 1rem;
  }

  .access-grid .folder-icon {
    width: 2rem;
    height: 2rem;
  }
}
</style>
