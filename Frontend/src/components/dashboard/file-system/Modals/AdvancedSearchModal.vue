<template>
  <UModal
    :close="{ onClick: () => emit('close', 'close') }"
    title="Advanced Search"
    fullscreen
    :scrollable="true"
    :ui="{ body: 'p-0' }"
  >
    <template #body>
      <UForm
        :schema="unifiedSearchUiSchema"
        :state="state"
        @submit="onSubmit"
        ref="form"
        class="h-full flex flex-col"
      >
        <!-- Sticky Mode Switcher -->
        <div
          class="sticky top-0 bg-elevated/80 backdrop-blur-md border-b border-default z-20 px-4 py-3 sm:px-6 sm:py-4"
        >
          <div
            class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="flex items-center gap-2">
              <UButton
                :color="searchMode === 'both' ? 'primary' : 'neutral'"
                :variant="searchMode === 'both' ? 'solid' : 'outline'"
                icon="i-lucide-search"
                label="Both"
                @click="switchMode('both')"
                size="sm"
              />
              <UButton
                :color="searchMode === 'directories' ? 'primary' : 'neutral'"
                :variant="searchMode === 'directories' ? 'solid' : 'outline'"
                icon="i-lucide-folder"
                label="Directories"
                @click="switchMode('directories')"
                size="sm"
              />
              <UButton
                :color="searchMode === 'files' ? 'primary' : 'neutral'"
                :variant="searchMode === 'files' ? 'solid' : 'outline'"
                icon="i-lucide-file"
                label="Files"
                @click="switchMode('files')"
                size="sm"
              />
            </div>
            <p class="text-sm text-muted hidden sm:block">
              {{ searchModeDescription }}
            </p>
          </div>
        </div>

        <!-- Main Content: Form + Results -->
        <div class="flex-1 overflow-hidden flex flex-col lg:flex-row">
          <div
            class="w-full lg:w-132.5 xl:w-150 lg:min-w-137.5 border-b lg:border-b-0 lg:border-r border-default overflow-y-auto"
          >
            <div class="p-4 sm:p-6 space-y-5">
              <!-- Section: General -->
              <fieldset class="space-y-3">
                <legend
                  class="text-xs font-semibold uppercase tracking-wider text-muted mb-2"
                >
                  General
                </legend>

                <UFormField
                  label="Name contains"
                  name="nameContains"
                  class="w-full"
                >
                  <UInput
                    v-model="state.nameContains"
                    placeholder="Search by name..."
                    icon="i-lucide-text-cursor-input"
                    class="w-full"
                  />
                </UFormField>

                <UFormField
                  label="Parent directory"
                  name="parentDirectoryId"
                  class="w-full"
                >
                  <USelectMenu
                    :loading="isParentDirLoading"
                    :items="parentDirData?.items"
                    placeholder="All directories"
                    searchable
                    :debounce="300"
                    value-key="id"
                    display-key="name"
                    v-model="state.parentDirectoryId"
                    clear
                    clear-icon="i-lucide-trash"
                    class="w-full"
                  >
                    <template #default="{ modelValue }">
                      <span v-if="modelValue">
                        {{
                          parentDirData?.items.find((i) => i.id === modelValue)
                            ?.name
                        }}
                      </span>
                      <span v-else class="text-muted">All directories</span>
                    </template>
                  </USelectMenu>
                </UFormField>

                <div class="flex flex-wrap gap-x-6 gap-y-2 pt-1">
                  <UFormField name="onlyDeleted">
                    <UCheckbox
                      v-model="state.onlyDeleted"
                      label="Only deleted"
                    />
                  </UFormField>
                  <UFormField name="isShared">
                    <UCheckbox v-model="state.isShared" label="Is shared" />
                  </UFormField>
                </div>
              </fieldset>

              <USeparator />

              <!-- Section: File-specific Filters (shown when mode includes files) -->
              <fieldset v-if="searchMode !== 'directories'" class="space-y-3">
                <legend
                  class="text-xs font-semibold uppercase tracking-wider text-muted mb-2"
                >
                  File Filters
                </legend>

                <UFormField
                  label="File extension"
                  name="mime-type"
                  class="w-full"
                >
                  <USelectMenu
                    class="w-full"
                    :items="mimeTypeOptions"
                    v-model="state.mimeType"
                    placeholder="Any type"
                    value-key="value"
                    icon="mdi-light:file"
                    clear
                  >
                  </USelectMenu>
                </UFormField>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <UFormField label="Min size" name="min-size" class="w-full">
                    <div class="flex gap-2">
                      <UInputNumber
                        class="flex-1"
                        v-model="minSizeDisplay"
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

                  <UFormField label="Max size" name="max-size" class="w-full">
                    <div class="flex gap-2">
                      <UInputNumber
                        class="flex-1"
                        v-model="maxSizeDisplay"
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
                </div>
              </fieldset>

              <!-- Section: Directory-specific Filters (shown when mode includes directories) -->
              <fieldset v-if="searchMode !== 'files'" class="space-y-3">
                <legend
                  class="text-xs font-semibold uppercase tracking-wider text-muted mb-2"
                >
                  Directory Filters
                </legend>

                <div class="flex flex-wrap gap-x-6 gap-y-2">
                  <UFormField name="has-files">
                    <UCheckbox v-model="state.hasFiles" label="Has sub-files" />
                  </UFormField>
                  <UFormField name="has-subdirectories">
                    <UCheckbox
                      v-model="state.hasSubdirectories"
                      label="Has sub-directories"
                    />
                  </UFormField>
                </div>
              </fieldset>
              <USeparator />
              <!-- Section: Sorting & Pagination -->

              <fieldset class="space-y-3">
                <legend
                  class="text-xs font-semibold uppercase tracking-wider text-muted mb-2"
                >
                  Sorting & Pagination
                </legend>

                <div class="grid md:grid-cols-2 grid-cols-1 gap-3">
                  <UFormField label="Sort by" name="sort-by" class="w-full">
                    <USelect
                      :items="sortByOptions"
                      v-model="state.sortBy"
                      class="w-full"
                    />
                  </UFormField>
                  <UFormField
                    label="Direction"
                    name="sort-direction"
                    class="w-full"
                  >
                    <USelect
                      :items="sortDirectionOptions"
                      v-model="state.sortDirection"
                      class="w-full"
                    />
                  </UFormField>
                </div>

                <UFormField label="Page size" name="page-size" class="w-full">
                  <USelect
                    :items="pageSizeOptions"
                    v-model="state.pageSize"
                    class="w-full"
                  />
                </UFormField>
              </fieldset>

              <USeparator />

              <!-- Section: Date Filters (collapsible) -->
              <UCollapsible>
                <UButton
                  variant="ghost"
                  color="neutral"
                  block
                  class="justify-between"
                  trailing-icon="i-lucide-chevron-down"
                >
                  <span class="text-xs font-semibold uppercase tracking-wider"
                    >Date Filters</span
                  >
                </UButton>

                <template #content>
                  <div class="space-y-3 pt-3">
                    <div
                      class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-1 xl:grid-cols-2 gap-3"
                    >
                      <UFormField
                        label="Created after"
                        name="createdAfter"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="createdAfter"
                          v-model="state.createdAfter"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="createdAfter?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Select a date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar v-model="state.createdAfter" />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>

                      <UFormField
                        label="Created before"
                        name="createdBefore"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="createdBefore"
                          v-model="state.createdBefore"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="createdBefore?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Select a date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar v-model="state.createdBefore" />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>

                      <UFormField
                        label="Updated after"
                        name="updatedAfter"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="updatedAfter"
                          v-model="state.updatedAfter"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="updatedAfter?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Select a date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar v-model="state.updatedAfter" />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>

                      <UFormField
                        label="Updated before"
                        name="updatedBefore"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="updatedBefore"
                          v-model="state.updatedBefore"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="updatedBefore?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Select a date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar v-model="state.updatedBefore" />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                      <UFormField
                        label="Deleted at"
                        name="deletedBefore"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="deletedBefore"
                          v-model="state.deletedBefore"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="deletedBefore?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Select a date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar v-model="state.deletedBefore" />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                      <UFormField
                        label="Deleted at"
                        name="deletedAfter"
                        class="w-full"
                      >
                        <!-- @vue-ignore -->
                        <UInputDate
                          ref="deletedAfter"
                          v-model="state.deletedAfter"
                          class="w-full"
                        >
                          <template #trailing>
                            <UPopover
                              :reference="deletedAfter?.inputsRef[3]?.$el"
                            >
                              <UButton
                                color="neutral"
                                variant="link"
                                size="sm"
                                icon="i-lucide-calendar"
                                aria-label="Select a date"
                                class="px-0"
                              />
                              <template #content>
                                <!-- @vue-ignore -->
                                <UCalendar v-model="state.deletedAfter" />
                              </template>
                            </UPopover>
                          </template>
                        </UInputDate>
                      </UFormField>
                    </div>
                  </div>
                </template>
              </UCollapsible>
            </div>
          </div>

          <!-- ═══════════════ RIGHT PANEL: Results ═══════════════ -->
          <div class="flex-1 flex flex-col overflow-hidden min-h-0">
            <!-- Results Header -->
            <div
              class="shrink-0 border-b border-default px-4 py-3 sm:px-6 sm:py-4 flex items-center justify-between"
            >
              <div>
                <h3 class="text-sm font-semibold">Results</h3>
                <p
                  v-if="hasSearched && !isSearching"
                  class="text-xs text-muted mt-0.5"
                >
                  {{ totalResultsText }}
                </p>
                <p v-else-if="isSearching" class="text-xs text-muted mt-0.5">
                  Searching...
                </p>
                <p v-else class="text-xs text-muted mt-0.5">
                  Configure filters and search
                </p>
              </div>
              <UButton
                v-if="hasResults"
                size="xs"
                color="neutral"
                variant="ghost"
                icon="i-lucide-x"
                label="Clear"
                @click="clearResults"
              />
            </div>

            <!-- Results Body (scrollable) -->
            <div class="flex-1 overflow-y-auto">
              <!-- Loading State -->
              <div
                v-if="isSearching"
                class="flex flex-col items-center justify-center py-20"
              >
                <UIcon
                  name="i-lucide-loader-circle"
                  class="size-8 animate-spin text-muted mb-3"
                />
                <p class="text-sm text-muted">Searching...</p>
              </div>

              <!-- Empty State - No Search Yet -->
              <div
                v-else-if="!hasSearched"
                class="flex flex-col items-center justify-center py-20 text-center px-4"
              >
                <div
                  class="rounded-full p-5 mb-4 bg-elevated border border-default"
                >
                  <UIcon name="i-lucide-search" class="size-10 text-muted" />
                </div>
                <h4 class="text-sm font-medium mb-1">Ready to Search</h4>
                <p class="text-xs text-muted max-w-xs">
                  Configure your search parameters, then click Search to find
                  matching items.
                </p>
              </div>

              <!-- No Results State -->
              <div
                v-else-if="
                  directoryResults.length === 0 && fileResults.length === 0
                "
                class="flex flex-col items-center justify-center py-20 text-center px-4"
              >
                <div
                  class="rounded-full p-5 mb-4 bg-elevated border border-default"
                >
                  <UIcon name="i-lucide-folder-x" class="size-10 text-muted" />
                </div>
                <h4 class="text-sm font-medium mb-1">No Results Found</h4>
                <p class="text-xs text-muted max-w-xs mb-4">
                  No items match your current search criteria. Try adjusting the
                  filters.
                </p>
                <UButton
                  size="sm"
                  color="primary"
                  variant="soft"
                  label="Reset Filters"
                  icon="i-lucide-rotate-ccw"
                  @click="reset"
                />
              </div>

              <!-- Results List -->
              <div v-else class="p-4 sm:p-6 space-y-6">
                <!-- Directory Results -->
                <div v-if="directoryResults.length > 0">
                  <div class="flex items-center gap-2 mb-3">
                    <UIcon name="i-lucide-folder" class="size-4 text-muted" />
                    <h4
                      class="text-xs font-semibold uppercase tracking-wider text-muted"
                    >
                      Directories ({{ directoryResults.length }})
                    </h4>
                  </div>
                  <div class="space-y-1.5">
                    <DirectoryItem
                      v-for="dir in directoryResults"
                      @navigate="handleNavigate(dir.id)"
                      @click="handleNavigate(dir.id)"
                      :key="dir.id"
                      :data="dir"
                      view-mode="list"
                      :is-selected="false"
                    />
                  </div>
                </div>

                <!-- File Results -->
                <div v-if="fileResults.length > 0">
                  <div class="flex items-center gap-2 mb-3">
                    <UIcon name="i-lucide-file" class="size-4 text-muted" />
                    <h4
                      class="text-xs font-semibold uppercase tracking-wider text-muted"
                    >
                      Files ({{ fileResults.length }})
                    </h4>
                  </div>
                  <div class="space-y-1.5">
                    <FileItem
                      v-for="file in fileResults"
                      :key="file.fileId"
                      :is-selected="false"
                      :tags="file.tags"
                      @click="handleNavigate(file.directoryId)"
                      :data="file"
                      view-mode="list"
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </UForm>
    </template>

    <template #footer>
      <div
        class="flex flex-col-reverse sm:flex-row justify-between w-full gap-2"
      >
        <UButton
          color="neutral"
          variant="ghost"
          label="Reset All Filters"
          icon="i-lucide-rotate-ccw"
          @click="reset"
          :disabled="isSearching"
        />
        <div class="flex gap-2 justify-end">
          <UButton
            color="neutral"
            variant="outline"
            label="Cancel"
            @click="emit('close', 'close')"
          />
          <UButton
            color="primary"
            label="Search"
            icon="i-lucide-search"
            @click="handleSubmit"
            :loading="isSearching"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { searchDirectory } from "@/queries/directories";
import {
  directorySearchApiSchema,
  fileSearchApiSchema,
  unifiedSearchUiSchema,
  type UnifiedSearchUiState,
} from "@/schemas/search";
import type { FormSubmitEvent } from "@nuxt/ui";
import { useQuery } from "@pinia/colada";
import { reactive, ref, shallowRef, computed } from "vue";
import { useFileStore } from "@/stores/file";
import { useDirectoryStore } from "@/stores/directory";

defineShortcuts({
  enter: () => handleSubmit(),
});

const directoryStore = useDirectoryStore();
const fileStore = useFileStore();

const fileResults = ref<FileResult[]>([]);
const directoryResults = ref<DirectorySummaryDto[]>([]);
const emit = defineEmits<{ close: [string | "root" | "close"] }>();

const createSearchState = () => unifiedSearchUiSchema.parse({});

const createdBefore = shallowRef();
const createdAfter = shallowRef();
const updatedBefore = shallowRef();
const updatedAfter = shallowRef();
const deletedBefore = shallowRef();
const deletedAfter = shallowRef();

const reset = () => {
  Object.assign(state, createSearchState());
};

const state = reactive(createSearchState());
const isSearching = ref(false);
const hasSearched = ref(false);
const hasResults = ref(false);
const totalResults = ref(0);
const minSizeDisplay = ref<number | null>(null);
const minSizeUnit = ref(1024 * 1024);
const maxSizeDisplay = ref<number | null>(null);
const maxSizeUnit = ref(1024 * 1024);
const form = ref();

const searchMode = ref<"both" | "files" | "directories">("both");
const searchModeDescription = ref("Search across files and directories");

const totalResultsText = computed(() => {
  const dirCount = directoryResults.value.length;
  const fileCount = fileResults.value.length;
  const total = dirCount + fileCount;

  if (searchMode.value === "both") {
    return `${total} result${total !== 1 ? "s" : ""} found (${dirCount} director${dirCount !== 1 ? "ies" : "y"}, ${fileCount} file${fileCount !== 1 ? "s" : ""})`;
  } else if (searchMode.value === "directories") {
    return `${dirCount} director${dirCount !== 1 ? "ies" : "y"} found`;
  } else {
    return `${fileCount} file${fileCount !== 1 ? "s" : ""} found`;
  }
});

const switchMode = (mode: "both" | "files" | "directories") => {
  searchMode.value = mode;
  switch (mode) {
    case "both":
      searchModeDescription.value = "Search across files and directories";
      break;
    case "directories":
      searchModeDescription.value = "Search directories only";
      break;
    case "files":
      searchModeDescription.value = "Search files only";
      break;
  }
  fileResults.value = [];
  directoryResults.value = [];
};

// Options
const sortByOptions = [
  { label: "Name", value: SortBy.Name },
  { label: "Created Date", value: SortBy.CreatedAt },
  { label: "Updated Date", value: SortBy.UpdatedAt },
];

const sortDirectionOptions = [
  { label: "Ascending", value: SortDirection.Asc },
  { label: "Descending", value: SortDirection.Desc },
];

const pageSizeOptions = [
  { label: "10 results", value: 10 },
  { label: "20 results", value: 20 },
  { label: "50 results", value: 50 },
  { label: "100 results", value: 100 },
];

const mimeTypeOptions = [
  { label: "PDF", value: "application/pdf" },
  {
    label: "Word Document",
    value:
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  },
  {
    label: "Excel Spreadsheet",
    value: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  },
  {
    label: "PowerPoint",
    value:
      "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  },
  { label: "Image (JPEG)", value: "image/jpeg" },
  { label: "Image (PNG)", value: "image/png" },
  { label: "Image (GIF)", value: "image/gif" },
  { label: "Video (MP4)", value: "video/mp4" },
  { label: "Audio (MP3)", value: "audio/mpeg" },
  { label: "Text", value: "text/plain" },
  { label: "ZIP Archive", value: "application/zip" },
];
const sizeUnitOptions = [
  { label: "B", value: 1 },
  { label: "KB", value: 1024 },
  { label: "MB", value: 1024 * 1024 },
  { label: "GB", value: 1024 * 1024 * 1024 },
  { label: "TB", value: 1024 * 1024 * 1024 * 1024 },
];

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

const searchTerm = ref("");

const { data: parentDirData, isLoading: isParentDirLoading } = useQuery(
  searchDirectory({
    nameContains: searchTerm.value,
    hasSubdirectories: true,
    pageSize: 20,
    isDeleted: false,
    page: 0,
    isStarred: false,
  }),
);

const clearResults = () => {
  fileResults.value = [];
  directoryResults.value = [];
  totalResults.value = 0;
  minSizeUnit.value = 1024 * 1024;
  maxSizeUnit.value = 1024 * 1024;
  hasResults.value = false;
};

const handleSubmit = () => {
  state.currentPage = 0;
  form.value?.submit();
};

const handleNavigate = (id: string | null) => emit("close", id ?? "root");

const onSubmit = async (event: FormSubmitEvent<UnifiedSearchUiState>) => {
  isSearching.value = true;
  hasSearched.value = true;
  console.log("searching");
  try {
    if (searchMode.value === "both" || searchMode.value === "files") {
      const filesQuery = fileSearchApiSchema.parse(event.data);
      const result = await fileStore.searchFiles(filesQuery);
      fileResults.value = result.data?.items ?? [];
    }

    if (searchMode.value === "both" || searchMode.value === "directories") {
      const directoriesQuery = directorySearchApiSchema.parse(event.data);
      const result = await directoryStore.searchDirectory(directoriesQuery);
      directoryResults.value = result.data?.items ?? [];
    }
  } catch (error) {
    console.error(error);
  } finally {
    isSearching.value = false;
  }
};
</script>

<style scoped></style>
