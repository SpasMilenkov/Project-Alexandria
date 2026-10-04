<template>
  <UModal
    fullscreen
    :ui="{
      body: 'p-0 flex-1 min-h-0 overflow-hidden flex flex-col',
      header: 'border-b border-gray-200/70 dark:border-gray-700/70',
      content: glassModalContent,
    }"
  >
    <template #header>
      <div class="flex flex-wrap items-center gap-4 flex-1 min-w-0">
        <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">Advanced search</h2>
        <div class="flex-1" />

        <div
          class="order-last md:order-none w-full md:w-auto inline-flex gap-1 p-0.5 rounded-lg bg-gray-100 dark:bg-gray-800"
          role="group"
          aria-label="Search in"
        >
          <UButton
            v-for="option in modeOptions"
            :key="option.id"
            size="sm"
            class="flex-1 md:flex-none justify-center"
            :icon="option.icon"
            :color="option.color"
            :variant="option.variant"
            :aria-pressed="option.isActive"
            @click="switchMode(option.id)"
          >
            {{ option.label }}
          </UButton>
        </div>

        <UButton
          icon="i-heroicons-x-mark"
          size="sm"
          variant="ghost"
          color="neutral"
          aria-label="Close"
          @click="emit('close', 'close')"
        />
      </div>
    </template>

    <template #body>
      <!-- @vue-ignore -->
      <UForm
        ref="form"
        :schema="unifiedSearchUiSchema"
        :state="state"
        class="flex flex-col md:flex-row flex-1 min-h-0"
        @submit="onSubmit"
      >
        <!-- Filters -->
        <aside
          class="w-full md:w-96 shrink-0 min-h-0 flex-col max-h-[60%] md:max-h-none border-b md:border-b-0 md:border-r border-gray-200/70 dark:border-gray-700/70"
          :class="asideClass"
        >
          <div class="flex-1 overflow-y-auto p-4 md:p-6 space-y-4">
            <div class="flex items-center justify-between">
              <h3
                class="flex items-center gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
              >
                <UIcon name="i-lucide-filter" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
                Filters
                <UBadge v-if="appliedFilters.length > 0" color="primary" variant="subtle" size="sm">
                  {{ appliedFilters.length }}
                </UBadge>
              </h3>
              <UButton
                size="xs"
                color="error"
                variant="outline"
                icon="i-lucide-rotate-ccw"
                :disabled="isSearching"
                @click="reset"
              >
                Reset
              </UButton>
            </div>

            <UFormField label="Name contains" name="nameContains" class="w-full">
              <UInput
                v-model="state.nameContains"
                placeholder="Search by name"
                icon="i-lucide-search"
                class="w-full"
                @keydown.enter.prevent="handleSubmit"
              />
            </UFormField>

            <UFormField label="Location" name="parentDirectoryId" class="w-full">
              <USelectMenu
                v-model="state.parentDirectoryId"
                :loading="isParentDirLoading"
                :items="parentDirData?.items"
                placeholder="All folders"
                icon="i-lucide-folder"
                value-key="id"
                label-key="name"
                clear
                class="w-full"
              />
            </UFormField>

            <UFormField label="Include" class="w-full">
              <div class="flex flex-wrap gap-2">
                <UButton
                  v-for="flag in flagOptions"
                  :key="flag.key"
                  size="xs"
                  class="rounded-full"
                  :icon="flag.icon"
                  :color="flag.color"
                  :variant="flag.variant"
                  :aria-pressed="flag.isActive"
                  @click="toggleFlag(flag.key)"
                >
                  {{ flag.label }}
                </UButton>
              </div>
            </UFormField>

            <template v-if="searchMode !== 'directories'">
              <UFormField label="File type" name="mimeType" class="w-full">
                <USelectMenu
                  v-model="state.mimeType"
                  :items="mimeTypeOptions"
                  placeholder="Any type"
                  icon="i-lucide-file"
                  value-key="value"
                  clear
                  class="w-full"
                />
              </UFormField>

              <UFormField label="Min size" name="minSize" class="w-full">
                <div class="flex gap-2">
                  <UInputNumber
                    v-model="minSizeDisplay"
                    class="flex-1"
                    :min="0"
                    placeholder="0"
                    @update:model-value="updateMinSize"
                  />
                  <USelect
                    v-model="minSizeUnit"
                    :items="sizeUnitOptions"
                    class="w-24"
                    @update:model-value="updateMinSize"
                  />
                </div>
              </UFormField>

              <UFormField label="Max size" name="maxSize" class="w-full">
                <div class="flex gap-2">
                  <UInputNumber
                    v-model="maxSizeDisplay"
                    class="flex-1"
                    :min="0"
                    placeholder="No limit"
                    @update:model-value="updateMaxSize"
                  />
                  <USelect
                    v-model="maxSizeUnit"
                    :items="sizeUnitOptions"
                    class="w-24"
                    @update:model-value="updateMaxSize"
                  />
                </div>
              </UFormField>
            </template>

            <UCollapsible>
              <UButton
                variant="ghost"
                color="neutral"
                block
                class="group justify-between"
                icon="i-lucide-calendar"
                trailing-icon="i-lucide-chevron-down"
                :ui="{
                  trailingIcon:
                    'transition-transform duration-200 group-data-[state=open]:rotate-180',
                }"
              >
                Dates
              </UButton>

              <template #content>
                <div class="space-y-4 pt-4">
                  <div v-for="group in visibleDateGroups" :key="group.id" class="space-y-2">
                    <p class="text-sm font-medium text-gray-600 dark:text-gray-400">
                      {{ group.label }}
                    </p>
                    <div class="grid grid-cols-2 gap-2">
                      <UFormField v-for="field in group.fields" :key="field.key" :name="field.key">
                        <!-- @vue-ignore -->
                        <UInputDate v-model="state[field.key]" class="w-full">
                          <template #trailing>
                            <UPopover>
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                :aria-label="`${group.label} ${field.label.toLowerCase()}`"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar v-model="state[field.key]" />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                        <p class="mt-1 text-xs text-gray-500 dark:text-gray-500">
                          {{ field.label }}
                        </p>
                      </UFormField>
                    </div>
                  </div>
                </div>
              </template>
            </UCollapsible>
          </div>

          <div class="shrink-0 p-4 md:px-6 border-t border-gray-200/70 dark:border-gray-700/70">
            <UButton
              block
              size="lg"
              color="primary"
              icon="i-lucide-search"
              :loading="isSearching"
              @click="handleSubmit"
            >
              Search
              <UKbd class="hidden md:inline-flex" value="enter" />
            </UButton>
          </div>
        </aside>

        <!-- Results -->
        <section class="flex-1 min-w-0 min-h-0 flex flex-col">
          <div class="shrink-0 flex items-center gap-2 p-4 md:px-6">
            <UButton
              class="md:hidden"
              size="xs"
              color="neutral"
              variant="outline"
              icon="i-lucide-sliders-horizontal"
              @click="filtersOpen = !filtersOpen"
            >
              Filters
              <UBadge v-if="appliedFilters.length > 0" color="primary" variant="subtle" size="sm">
                {{ appliedFilters.length }}
              </UBadge>
            </UButton>

            <div class="flex-1 min-w-0">
              <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Results</h3>
              <p class="text-xs text-gray-500 dark:text-gray-500">
                {{ resultsStatus }}
              </p>
            </div>

            <UButton
              v-if="hasResults"
              size="xs"
              color="neutral"
              variant="ghost"
              icon="i-lucide-x"
              @click="clearResults"
            >
              Clear
            </UButton>
            <USelect
              v-model="state.sortBy"
              :items="sortByOptions"
              size="sm"
              class="w-40"
              aria-label="Sort by"
              @update:model-value="resubmitIfSearched"
            />
            <UButton
              size="sm"
              color="neutral"
              variant="ghost"
              :icon="sortDirectionIcon"
              aria-label="Toggle sort direction"
              @click="toggleSortDirection"
            />
          </div>

          <div
            v-if="appliedFilters.length > 0"
            class="shrink-0 flex flex-wrap gap-2 px-4 pb-4 md:px-6"
          >
            <UButton
              v-for="chip in appliedFilters"
              :key="chip.id"
              size="xs"
              color="neutral"
              variant="subtle"
              trailing-icon="i-lucide-x"
              class="rounded-full"
              @click="chip.clear"
            >
              {{ chip.label }}
            </UButton>
          </div>

          <div
            class="flex-1 min-h-0 overflow-y-auto border-t border-gray-200/70 dark:border-gray-700/70"
          >
            <div v-if="isSearching" class="p-4 md:p-6 space-y-2">
              <USkeleton v-for="n in 4" :key="n" class="h-14 w-full rounded-lg" />
            </div>

            <div
              v-else-if="!hasSearched"
              class="flex flex-col items-center justify-center h-full gap-2 p-6 text-center"
            >
              <UIcon name="i-lucide-search" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
              <h4 class="text-base font-semibold text-gray-900 dark:text-gray-100">
                Ready to search
              </h4>
              <p class="max-w-xs text-sm text-gray-600 dark:text-gray-400">
                Pick some filters, then press Search to see matching items.
              </p>
            </div>

            <div
              v-else-if="!hasResults"
              class="flex flex-col items-center justify-center h-full gap-2 p-6 text-center"
            >
              <UIcon name="i-lucide-folder-x" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
              <h4 class="text-base font-semibold text-gray-900 dark:text-gray-100">
                No results found
              </h4>
              <p class="max-w-xs text-sm text-gray-600 dark:text-gray-400">
                Nothing matches these filters. Loosen a filter or start over.
              </p>
              <UButton class="mt-2" color="neutral" variant="outline" size="sm" @click="reset">
                Reset filters
              </UButton>
            </div>

            <div v-else class="p-4 md:p-6 space-y-6">
              <div v-if="directoryResults.length > 0">
                <h4
                  class="flex items-center gap-2 mb-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
                >
                  <UIcon name="i-lucide-folder" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
                  Folders ({{ directoryResults.length }})
                </h4>
                <div class="space-y-2">
                  <DirectoryItem
                    v-for="dir in directoryResults"
                    :key="dir.id"
                    :data="dir"
                    view-mode="list"
                    :is-selected="false"
                    @navigate="handleNavigate(dir.id)"
                    @click="handleNavigate(dir.id)"
                  />
                </div>
              </div>

              <div v-if="fileResults.length > 0">
                <h4
                  class="flex items-center gap-2 mb-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
                >
                  <UIcon name="i-lucide-file" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
                  Files ({{ fileResults.length }})
                </h4>
                <div class="space-y-2">
                  <FileItem
                    v-for="file in fileResults"
                    :key="file.fileId"
                    :data="file"
                    :tags="file.tags"
                    view-mode="list"
                    :is-selected="false"
                    @click="handleNavigate(file.directoryId)"
                  />
                </div>
              </div>
            </div>
          </div>
        </section>
      </UForm>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { useQuery } from "@pinia/colada";
import { breakpointsTailwind, useBreakpoints } from "@vueuse/core";
import { computed, nextTick, reactive, ref } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";

import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { searchDirectory } from "@/queries/directories";
import {
  type UnifiedSearchUiState,
  directorySearchApiSchema,
  fileSearchApiSchema,
  unifiedSearchUiSchema,
} from "@/schemas/search";
import { useDirectoryStore } from "@/stores/directory";
import { useFileStore } from "@/stores/file";
import { logger } from "@/utils/logger";
import { glassModalContent } from "@/utils/modalUi";

type SearchMode = "both" | "directories" | "files";
type FlagKey = "hasFiles" | "hasSubdirectories" | "isShared" | "onlyDeleted";
type DateKey =
  | "createdAfter"
  | "createdBefore"
  | "deletedAfter"
  | "deletedBefore"
  | "updatedAfter"
  | "updatedBefore";
interface AppliedFilter {
  clear: () => void;
  id: string;
  label: string;
}

defineShortcuts({
  enter: () => handleSubmit(),
});

const emit = defineEmits<{ close: [string | "root" | "close"] }>();

useModalBackGuard(() => emit("close", "close"));

const directoryStore = useDirectoryStore();
const fileStore = useFileStore();
const isMobile = useBreakpoints(breakpointsTailwind).smaller("md");

const DEFAULT_SIZE_UNIT = 1024 * 1024;

const createSearchState = () => unifiedSearchUiSchema.parse({});

const state = reactive(createSearchState());
const form = ref();
const searchMode = ref<SearchMode>("both");
const filtersOpen = ref(true);
const isSearching = ref(false);
const hasSearched = ref(false);
const fileResults = ref<FileResult[]>([]);
const directoryResults = ref<DirectorySummaryDto[]>([]);
const minSizeDisplay = ref<number | null>(null);
const minSizeUnit = ref(DEFAULT_SIZE_UNIT);
const maxSizeDisplay = ref<number | null>(null);
const maxSizeUnit = ref(DEFAULT_SIZE_UNIT);
const searchTerm = ref("");

const { data: parentDirData, isLoading: isParentDirLoading } = useQuery(
  searchDirectory({
    hasSubdirectories: true,
    isDeleted: false,
    isStarred: false,
    nameContains: searchTerm.value,
    page: 0,
    pageSize: 20,
  }),
);

// Options
const sortByOptions = [
  { label: "Name", value: SortBy.Name },
  { label: "Created date", value: SortBy.CreatedAt },
  { label: "Updated date", value: SortBy.UpdatedAt },
];

const mimeTypeOptions = [
  { icon: "i-lucide-file-text", label: "PDF", value: "application/pdf" },
  {
    icon: "i-lucide-file-text",
    label: "Word document",
    value: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  },
  {
    icon: "i-lucide-file-spreadsheet",
    label: "Excel spreadsheet",
    value: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  },
  {
    icon: "i-lucide-presentation",
    label: "PowerPoint",
    value: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  },
  { icon: "i-lucide-image", label: "Image (JPEG)", value: "image/jpeg" },
  { icon: "i-lucide-image", label: "Image (PNG)", value: "image/png" },
  { icon: "i-lucide-image", label: "Image (GIF)", value: "image/gif" },
  { icon: "i-lucide-video", label: "Video (MP4)", value: "video/mp4" },
  { icon: "i-lucide-music", label: "Audio (MP3)", value: "audio/mpeg" },
  { icon: "i-lucide-align-left", label: "Text", value: "text/plain" },
  { icon: "i-lucide-archive", label: "ZIP archive", value: "application/zip" },
];

const sizeUnitOptions = [
  { label: "B", value: 1 },
  { label: "KB", value: 1024 },
  { label: "MB", value: 1024 * 1024 },
  { label: "GB", value: 1024 * 1024 * 1024 },
  { label: "TB", value: 1024 * 1024 * 1024 * 1024 },
];

const modeDefs: { icon: string; id: SearchMode; label: string }[] = [
  { icon: "i-lucide-layers", id: "both", label: "Everything" },
  { icon: "i-lucide-folder", id: "directories", label: "Folders" },
  { icon: "i-lucide-file", id: "files", label: "Files" },
];

const flagDefs: { dirOnly?: boolean; icon: string; key: FlagKey; label: string }[] = [
  { icon: "i-lucide-trash-2", key: "onlyDeleted", label: "Deleted only" },
  { icon: "i-lucide-share-2", key: "isShared", label: "Shared" },
  { dirOnly: true, icon: "i-lucide-file", key: "hasFiles", label: "Has files" },
  {
    dirOnly: true,
    icon: "i-lucide-folder-tree",
    key: "hasSubdirectories",
    label: "Has subfolders",
  },
];

const dateGroups: {
  deletedOnly?: boolean;
  fields: { key: DateKey; label: string }[];
  id: string;
  label: string;
}[] = [
  {
    fields: [
      { key: "createdAfter", label: "From" },
      { key: "createdBefore", label: "Until" },
    ],
    id: "created",
    label: "Created",
  },
  {
    fields: [
      { key: "updatedAfter", label: "From" },
      { key: "updatedBefore", label: "Until" },
    ],
    id: "updated",
    label: "Updated",
  },
  {
    deletedOnly: true,
    fields: [
      { key: "deletedAfter", label: "From" },
      { key: "deletedBefore", label: "Until" },
    ],
    id: "deleted",
    label: "Deleted",
  },
];

const dateChipLabels: Record<DateKey, string> = {
  createdAfter: "Created from",
  createdBefore: "Created until",
  deletedAfter: "Deleted from",
  deletedBefore: "Deleted until",
  updatedAfter: "Updated from",
  updatedBefore: "Updated until",
};

// Derived UI state
const asideClass = computed(() => (filtersOpen.value ? "flex" : "hidden md:flex"));

const modeOptions = computed(() =>
  modeDefs.map((def) => {
    const isActive = searchMode.value === def.id;
    return {
      ...def,
      color: (isActive ? "primary" : "neutral") as "neutral" | "primary",
      isActive,
      variant: (isActive ? "subtle" : "ghost") as "ghost" | "subtle",
    };
  }),
);

const visibleFlags = computed(() =>
  flagDefs.filter((def) => !def.dirOnly || searchMode.value !== "files"),
);

const flagOptions = computed(() =>
  visibleFlags.value.map((def) => {
    const isActive = !!state[def.key];
    return {
      ...def,
      color: (isActive ? "primary" : "neutral") as "neutral" | "primary",
      isActive,
      variant: (isActive ? "subtle" : "outline") as "outline" | "subtle",
    };
  }),
);

const visibleDateGroups = computed(() =>
  dateGroups.filter((group) => !group.deletedOnly || state.onlyDeleted),
);

const sortDirectionIcon = computed(() =>
  state.sortDirection === SortDirection.Asc ? "i-lucide-arrow-up" : "i-lucide-arrow-down",
);

const hasResults = computed(() => directoryResults.value.length + fileResults.value.length > 0);

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

const resultsStatus = computed(() => {
  if (isSearching.value) {
    return "Searching";
  }
  if (!hasSearched.value) {
    return "Set your filters, then search";
  }
  const dirText = plural(directoryResults.value.length, "folder", "folders");
  const fileText = plural(fileResults.value.length, "file", "files");
  if (searchMode.value === "directories") {
    return `${dirText} found`;
  }
  if (searchMode.value === "files") {
    return `${fileText} found`;
  }
  return `${dirText} and ${fileText} found`;
});

// Filter state helpers
const resetKeys = (...keys: (keyof UnifiedSearchUiState)[]) => {
  const fresh = createSearchState();
  Object.assign(state, Object.fromEntries(keys.map((key) => [key, fresh[key]])));
};

const unitLabel = (unit: number) => sizeUnitOptions.find((o) => o.value === unit)?.label ?? "";

const parentDirectoryName = computed(() => {
  const match = parentDirData.value?.items.find((i) => i.id === state.parentDirectoryId);
  return match?.name ?? "selected folder";
});

const appliedFilters = computed(() => {
  const list: AppliedFilter[] = [];
  const showFiles = searchMode.value !== "directories";

  if (state.nameContains) {
    list.push({
      clear: () => resetKeys("nameContains"),
      id: "name",
      label: `Name: ${state.nameContains}`,
    });
  }
  if (state.parentDirectoryId) {
    list.push({
      clear: () => resetKeys("parentDirectoryId"),
      id: "parent",
      label: `In ${parentDirectoryName.value}`,
    });
  }
  visibleFlags.value.forEach((def) => {
    if (state[def.key]) {
      list.push({ clear: () => resetKeys(def.key), id: def.key, label: def.label });
    }
  });
  if (showFiles && state.mimeType) {
    const type = mimeTypeOptions.find((o) => o.value === state.mimeType);
    list.push({
      clear: () => resetKeys("mimeType"),
      id: "mime",
      label: type?.label ?? "File type",
    });
  }
  if (showFiles && state.minSize) {
    list.push({
      clear: () => {
        resetKeys("minSize");
        minSizeDisplay.value = null;
      },
      id: "min",
      label: `Min ${minSizeDisplay.value} ${unitLabel(minSizeUnit.value)}`,
    });
  }
  if (showFiles && state.maxSize) {
    list.push({
      clear: () => {
        resetKeys("maxSize");
        maxSizeDisplay.value = null;
      },
      id: "max",
      label: `Max ${maxSizeDisplay.value} ${unitLabel(maxSizeUnit.value)}`,
    });
  }
  visibleDateGroups.value.forEach((group) => {
    group.fields.forEach((field) => {
      if (state[field.key]) {
        list.push({
          clear: () => resetKeys(field.key),
          id: field.key,
          label: `${dateChipLabels[field.key]} ${state[field.key]}`,
        });
      }
    });
  });
  return list;
});

const toggleFlag = (key: FlagKey) => {
  state[key] = !state[key];
};

const updateMinSize = () => {
  if (minSizeDisplay.value === null || minSizeDisplay.value === 0) {
    state.minSize = null;
  } else {
    // Round down for minimum (be more inclusive)
    state.minSize = Math.floor(minSizeDisplay.value * minSizeUnit.value);
  }
};

const updateMaxSize = () => {
  if (maxSizeDisplay.value === null || maxSizeDisplay.value === 0) {
    state.maxSize = null;
  } else {
    // Round up for maximum (be more inclusive)
    state.maxSize = Math.ceil(maxSizeDisplay.value * maxSizeUnit.value);
  }
};

// Results and actions
const clearResults = () => {
  fileResults.value = [];
  directoryResults.value = [];
  hasSearched.value = false;
};

const reset = () => {
  Object.assign(state, createSearchState());
  minSizeDisplay.value = null;
  maxSizeDisplay.value = null;
  minSizeUnit.value = DEFAULT_SIZE_UNIT;
  maxSizeUnit.value = DEFAULT_SIZE_UNIT;
  clearResults();
};

const switchMode = (mode: SearchMode) => {
  searchMode.value = mode;
  clearResults();
};

const handleSubmit = () => {
  state.currentPage = 0;
  form.value?.submit();
};

const resubmitIfSearched = async () => {
  await nextTick();
  if (hasSearched.value) {
    handleSubmit();
  }
};

const toggleSortDirection = () => {
  state.sortDirection =
    state.sortDirection === SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;
  resubmitIfSearched();
};

const handleNavigate = (id: string | null) => emit("close", id ?? "root");

const onSubmit = async (event: FormSubmitEvent<UnifiedSearchUiState>) => {
  isSearching.value = true;
  hasSearched.value = true;
  if (isMobile.value) {
    filtersOpen.value = false;
  }
  try {
    if (searchMode.value === "both" || searchMode.value === "files") {
      const filesQuery = fileSearchApiSchema.parse(event.data);
      const result = await fileStore.searchFiles(filesQuery);
      fileResults.value = result.data?.items ?? [];
    } else {
      fileResults.value = [];
    }

    if (searchMode.value === "both" || searchMode.value === "directories") {
      const directoriesQuery = directorySearchApiSchema.parse(event.data);
      const result = await directoryStore.searchDirectory(directoriesQuery);
      directoryResults.value = result.data?.items ?? [];
    } else {
      directoryResults.value = [];
    }
  } catch (error) {
    logger.error(error);
  } finally {
    isSearching.value = false;
  }
};
</script>
