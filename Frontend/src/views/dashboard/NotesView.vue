<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { refDebounced } from "@vueuse/core";
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import NotesListPage from "@/components/notes/hub/NotesListPage.vue";
import NotesSubfolders from "@/components/notes/hub/NotesSubfolders.vue";
import { useCreateNote } from "@/composables/useCreateNote";
import { type NotesPageLoaded, notesTrail } from "@/features/notes/notes-page";
import { directoryPath } from "@/queries/directories";
import { getNotesFolder } from "@/queries/notes";
import { formatCount } from "@/utils/count";

const SEARCH_PAUSE_MS = 300;

const route = useRoute();

const { data: notesFolder, isPending: isFolderPending } = useQuery(getNotesFolder);

// The folder lives in the address, so a reload or the back button returns to it.
const selectedId = computed(() => {
  const fromRoute = route.query.folder;

  if (typeof fromRoute === "string" && fromRoute !== "") return fromRoute;

  return notesFolder.value?.id ?? "";
});

const isRoot = computed(() => selectedId.value === notesFolder.value?.id);

const { data: path, isPlaceholderData: isPathStale } = useQuery(() => ({
  ...directoryPath(selectedId.value),
  enabled: selectedId.value !== "" && !isRoot.value,
}));

// A stand-in path belongs to the folder viewed before, so it would title the page and place a
// new note in the wrong folder until this one's path arrives.
const pathParts = computed(() => {
  if (isRoot.value && notesFolder.value) return [notesFolder.value];
  if (isPathStale.value) return [];

  return path.value?.pathParts ?? [];
});

const trail = computed(() => notesTrail(pathParts.value, notesFolder.value?.id));
const ancestors = computed(() => trail.value.slice(0, -1));
const title = computed(() => trail.value.at(-1)?.name ?? "");

const folderRoute = (id: string) => {
  if (id === notesFolder.value?.id) return { name: "notes" };

  return { name: "notes", query: { folder: id } };
};

// "New note" goes into the folder being viewed, or Notes itself at the top.
const { createAndOpen, isCreating } = useCreateNote({ path: pathParts });

const searchText = ref("");
const search = refDebounced(searchText, SEARCH_PAUSE_MS);
const isSearching = computed(() => search.value.trim() !== "");

// Pages load one after another as "Load more" asks; a new folder or search starts over.
const pageCount = ref(1);
const lastPage = ref<NotesPageLoaded | null>(null);
const pageEndGroups = ref<(string | null)[]>([]);
const now = ref(Date.now());

watch([selectedId, search], () => {
  pageCount.value = 1;
  lastPage.value = null;
  pageEndGroups.value = [];
  now.value = Date.now();
});

const onPageLoaded = (page: number, result: NotesPageLoaded) => {
  pageEndGroups.value[page] = result.lastGroupKey;

  if (page === pageCount.value - 1) lastPage.value = result;
};

const total = computed(() => lastPage.value?.total ?? null);
const isEmpty = computed(() => total.value === 0);

const summaryLine = computed(() => {
  if (total.value === null) return "";

  const count = formatCount(total.value, "note");

  if (isSearching.value) return `${count} matching “${search.value.trim()}”`;
  if (isRoot.value) return count;

  return `${count} in this folder and the ones inside it`;
});

const EMPTY_SEARCH = {
  icon: "i-lucide-search-x",
  text: "Nothing in this folder has those words in its name or text.",
  title: "No notes match",
};

const EMPTY_FOLDER = {
  icon: "i-lucide-notebook",
  text: "Notes you write in this folder, or the folders inside it, show up here.",
  title: "No notes here yet",
};

const emptyState = computed(() => {
  if (isSearching.value) return EMPTY_SEARCH;

  return EMPTY_FOLDER;
});
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <div v-if="isFolderPending" class="flex flex-1 items-center justify-center text-gray-500">
      <BlockSpinner :size="24" aria-label="Loading notes" />
    </div>

    <div v-else-if="!notesFolder" class="flex flex-1 items-center justify-center p-4">
      <GlassEmptyState
        icon="i-lucide-notebook"
        title="No notes yet"
        description="Notes are markdown files in your Notes folder. The first one creates the folder."
      >
        <UButton
          icon="i-lucide-notebook-pen"
          label="New note"
          color="primary"
          :loading="isCreating"
          @click="createAndOpen"
        />
      </GlassEmptyState>
    </div>

    <template v-else>
      <header
        class="frosted-glass glass-surface-strong flex shrink-0 flex-wrap items-end justify-between gap-4 rounded-xl border border-gray-200/70 px-4 py-4 md:px-6 md:py-5 dark:border-gray-700/70"
      >
        <div class="flex min-w-0 flex-col gap-1">
          <nav v-if="ancestors.length > 0" aria-label="Folder path" class="min-w-0">
            <ol class="flex min-w-0 flex-wrap items-center gap-1 text-sm">
              <li v-for="part in ancestors" :key="part.id" class="flex min-w-0 items-center gap-1">
                <RouterLink
                  :to="folderRoute(part.id)"
                  class="truncate rounded-sm text-gray-600 transition-colors duration-150 hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-400 dark:hover:text-gray-100"
                >
                  {{ part.name }}
                </RouterLink>

                <UIcon
                  name="i-lucide-chevron-right"
                  class="h-3.5 w-3.5 shrink-0 text-gray-500 dark:text-gray-500"
                  aria-hidden="true"
                />
              </li>
            </ol>
          </nav>

          <h1
            class="min-h-8 truncate text-2xl font-semibold tracking-tight text-gray-900 dark:text-gray-100"
          >
            {{ title }}
          </h1>

          <p class="min-h-5 text-sm text-gray-600 tabular-nums dark:text-gray-400">
            {{ summaryLine }}
          </p>
        </div>

        <div class="flex w-full min-w-0 items-center gap-2 sm:w-auto">
          <UInput
            id="notes-search"
            v-model="searchText"
            icon="i-lucide-search"
            placeholder="Search notes"
            aria-label="Search notes by name or text"
            class="min-w-0 flex-1 sm:w-80 sm:flex-none"
          >
            <template v-if="searchText" #trailing>
              <UButton
                variant="ghost"
                color="neutral"
                size="xs"
                icon="i-lucide-x"
                square
                class="justify-center"
                aria-label="Clear search"
                @click="searchText = ''"
              />
            </template>
          </UInput>

          <UButton
            icon="i-lucide-notebook-pen"
            label="New note"
            color="primary"
            class="shrink-0"
            :loading="isCreating"
            @click="createAndOpen"
          />
        </div>
      </header>

      <NotesSubfolders v-if="!isSearching" :folder-id="selectedId" />

      <section
        aria-labelledby="notes-list-title"
        class="frosted-glass glass-surface flex min-w-0 flex-col gap-3 rounded-xl border border-gray-200/70 p-3 md:p-4 dark:border-gray-700/70"
      >
        <h2 id="notes-list-title" class="sr-only">Notes in {{ title }}</h2>

        <ul v-show="!isEmpty" class="grid grid-cols-[repeat(auto-fill,minmax(16rem,1fr))] gap-3">
          <NotesListPage
            v-for="page in pageCount"
            :key="`${selectedId}:${search}:${page}`"
            :folder-id="selectedId"
            :search="search"
            :page="page - 1"
            :now="now"
            :previous-group-key="pageEndGroups[page - 2] ?? null"
            @loaded="(result) => onPageLoaded(page - 1, result)"
          />
        </ul>

        <div v-if="isEmpty" class="flex flex-col items-center gap-2 px-4 py-12 text-center">
          <UIcon
            :name="emptyState.icon"
            class="h-12 w-12 text-gray-400 dark:text-gray-600"
            aria-hidden="true"
          />

          <p class="font-medium text-gray-900 dark:text-gray-100">{{ emptyState.title }}</p>

          <p class="text-sm text-gray-600 dark:text-gray-400">{{ emptyState.text }}</p>

          <UButton
            v-if="!isSearching"
            icon="i-lucide-notebook-pen"
            label="New note"
            color="neutral"
            variant="outline"
            class="mt-2"
            :loading="isCreating"
            @click="createAndOpen"
          />
        </div>

        <div v-if="lastPage?.hasNext" class="flex justify-center pt-1">
          <UButton label="Load more" color="neutral" variant="outline" @click="pageCount += 1" />
        </div>
      </section>
    </template>
  </div>
</template>
