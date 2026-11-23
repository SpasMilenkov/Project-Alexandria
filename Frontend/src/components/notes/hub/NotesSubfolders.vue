<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import { FOLDER_PAGE } from "@/features/notes/notes-page";
import { subDirectories } from "@/queries/directories";

// The folders directly inside the one being viewed. Opening one narrows the notes below to it,
// and the breadcrumb leads back up, so a deep vault is browsed one level at a time.
const { folderId } = defineProps<{ folderId: string }>();

const { data, isPlaceholderData } = useQuery(() =>
  subDirectories({ id: folderId, params: FOLDER_PAGE }),
);

// The previous folder's children stand in while a new folder loads; showing them would offer
// folders that aren't in this one.
const folders = computed(() => {
  if (isPlaceholderData.value) return [];

  return data.value?.items ?? [];
});

const folderRoute = (id: string) => ({ name: "notes", query: { folder: id } });
</script>

<template>
  <section
    v-if="folders.length > 0"
    aria-labelledby="notes-folders-title"
    class="flex flex-col gap-2"
  >
    <h2
      id="notes-folders-title"
      class="px-1 text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
    >
      Folders
    </h2>

    <ul class="grid grid-cols-[repeat(auto-fill,minmax(11rem,1fr))] gap-2">
      <li v-for="folder in folders" :key="folder.id" class="min-w-0">
        <RouterLink
          :to="folderRoute(folder.id)"
          class="frosted-glass glass-surface flex items-center gap-2.5 rounded-xl border border-gray-200/70 px-3 py-2.5 text-sm font-medium text-gray-900 transition-colors duration-150 hover:border-gray-300 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:border-gray-700/70 dark:text-gray-100 dark:hover:border-gray-600"
        >
          <UIcon
            name="i-lucide-folder"
            class="h-4 w-4 shrink-0 text-gray-600 dark:text-gray-400"
            aria-hidden="true"
          />
          <span class="truncate" :title="folder.name">{{ folder.name }}</span>
        </RouterLink>
      </li>
    </ul>
  </section>
</template>
