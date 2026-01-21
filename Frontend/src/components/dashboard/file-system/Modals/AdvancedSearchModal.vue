<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Advanced Search"
    fullscreen
    :ui="{ body: 'p-0' }"
  >
    <template #body>
      <div class="grid md:grid-cols-2 grid-cols-1 h-full">
        <!-- Left Side - Search Form -->
        <div class="border-r overflow-y-auto p-8">
          <UForm
            ref="form"
            :state="state"
            :schema="directorySearchQuerySchema"
            @submit="onSubmit"
            class="space-y-8"
          >
            <!-- Text Search Section -->
            <div class="space-y-5">
              <h3 class="text-base font-semibold">Search Criteria</h3>

              <UFormField label="Directory Name" name="nameContains">
                <UInput
                  v-model="state.nameContains"
                  placeholder="Search by name..."
                  icon="i-lucide-search"
                  size="lg"
                  class="w-full"
                />
              </UFormField>

              <div class="grid lg:grid-cols-2 grid-cols-1 gap-4">
                <UFormField label="Directory ID" name="directoryId">
                  <UInput
                    v-model="state.directoryId"
                    placeholder="Enter directory ID"
                    size="lg"
                    class="w-full"
                    disabled
                  />
                </UFormField>

                <UFormField label="Parent Directory" name="parentDirectoryId">
                  <USelectMenu
                    v-model="state.parentDirectoryId"
                    :items="parentDirectoryOptions"
                    :loading="isLoadingParentDirs"
                    @update:search-term="searchParentDirectory"
                    placeholder="Search for parent directory..."
                    value-key="id"
                    display-key="label"
                    searchable
                    :debounce="300"
                    class="w-full"
                  >
                    <template #default="{ modelValue }">
                      <span v-if="modelValue">
                        {{
                          parentDirectoryOptions.find(
                            (i) => i.id === modelValue,
                          )?.label
                        }}
                      </span>
                      <span v-else class="text-muted">
                        Search for parent directory...
                      </span>
                    </template>
                  </USelectMenu>
                </UFormField>
              </div>
            </div>

            <!-- Ownership & Sharing Section -->
            <div class="space-y-5">
              <h3 class="text-base font-semibold">Ownership & Sharing</h3>

              <UFormField label="Owner ID" name="ownerId">
                <UInput
                  v-model="state.ownerId"
                  placeholder="Enter owner ID"
                  icon="i-lucide-user"
                  size="lg"
                  class="w-full"
                />
              </UFormField>

              <div class="flex flex-col gap-3">
                <UFormField name="isShared">
                  <UCheckbox
                    v-model="state.isShared"
                    label="Shared directories only"
                  />
                </UFormField>

                <UFormField name="isStarred">
                  <UCheckbox
                    v-model="state.isStarred"
                    label="Starred directories only"
                  />
                </UFormField>
              </div>
            </div>

            <!-- Date Filters Section -->
            <div class="space-y-5">
              <h3 class="text-base font-semibold">Date Filters</h3>

              <div class="grid lg:grid-cols-2 grid-cols-1 gap-4">
                <UFormField label="Created After" name="createdAfter">
                  <UInputDate
                    ref="createdAfterRef"
                    v-model="state.createdAfter"
                    icon="i-lucide-calendar"
                    size="lg"
                    class="w-full"
                  >
                    <template #trailing>
                      <UPopover :reference="createdAfterRef?.inputsRef[3]?.$el">
                        <UButton
                          color="neutral"
                          variant="link"
                          size="sm"
                          icon="i-lucide-calendar"
                          aria-label="Select a date"
                          class="px-0"
                        />
                        <template #content>
                          <UCalendar v-model="state.createdAfter" class="p-2" />
                        </template>
                      </UPopover>
                    </template>
                  </UInputDate>
                </UFormField>

                <UFormField label="Created Before" name="createdBefore">
                  <UInputDate
                    ref="createdBeforeRef"
                    v-model="state.createdBefore"
                    icon="i-lucide-calendar"
                    size="lg"
                    class="w-full"
                  >
                    <template #trailing>
                      <UPopover
                        :reference="createdBeforeRef?.inputsRef[3]?.$el"
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
                          <UCalendar
                            v-model="state.createdBefore"
                            class="p-2"
                          />
                        </template>
                      </UPopover>
                    </template>
                  </UInputDate>
                </UFormField>
              </div>

              <div class="grid lg:grid-cols-2 grid-cols-1 gap-4">
                <UFormField label="Updated After" name="updatedAfter">
                  <UInputDate
                    ref="updatedAfterRef"
                    v-model="state.updatedAfter"
                    icon="i-lucide-calendar"
                    size="lg"
                    class="w-full"
                  >
                    <template #trailing>
                      <UPopover :reference="updatedAfterRef?.inputsRef[3]?.$el">
                        <UButton
                          color="neutral"
                          variant="link"
                          size="sm"
                          icon="i-lucide-calendar"
                          aria-label="Select a date"
                          class="px-0"
                        />
                        <template #content>
                          <UCalendar v-model="state.updatedAfter" class="p-2" />
                        </template>
                      </UPopover>
                    </template>
                  </UInputDate>
                </UFormField>

                <UFormField label="Deleted At" name="deletedAt">
                  <UInputDate
                    ref="deletedAtRef"
                    v-model="state.deletedAt"
                    icon="i-lucide-calendar"
                    size="lg"
                    class="w-full"
                  >
                    <template #trailing>
                      <UPopover :reference="deletedAtRef?.inputsRef[3]?.$el">
                        <UButton
                          color="neutral"
                          variant="link"
                          size="sm"
                          icon="i-lucide-calendar"
                          aria-label="Select a date"
                          class="px-0"
                        />
                        <template #content>
                          <UCalendar v-model="state.deletedAt" class="p-2" />
                        </template>
                      </UPopover>
                    </template>
                  </UInputDate>
                </UFormField>
              </div>
            </div>

            <!-- Contents Section -->
            <div class="space-y-5">
              <h3 class="text-base font-semibold">Contents</h3>

              <div class="flex flex-col gap-3">
                <UFormField name="hasFiles">
                  <UCheckbox v-model="state.hasFiles" label="Has files" />
                </UFormField>

                <UFormField name="hasSubdirectories">
                  <UCheckbox
                    v-model="state.hasSubdirectories"
                    label="Has subdirectories"
                  />
                </UFormField>

                <UFormField name="isDeleted">
                  <UCheckbox
                    v-model="state.isDeleted"
                    label="Deleted directories"
                  />
                </UFormField>
              </div>
            </div>

            <!-- Sorting & Pagination Section -->
            <div class="space-y-5">
              <h3 class="text-base font-semibold">Sorting & Pagination</h3>

              <div class="grid grid-cols-2 gap-4">
                <UFormField label="Sort By" name="sortBy">
                  <USelect
                    v-model="state.sortBy"
                    :items="sortByOptions"
                    placeholder="Select sort field"
                    size="lg"
                    class="w-full"
                  />
                </UFormField>

                <UFormField label="Sort Direction" name="sortDirection">
                  <USelect
                    v-model="state.sortDirection"
                    :items="sortDirectionOptions"
                    placeholder="Select direction"
                    size="lg"
                    class="w-full"
                  />
                </UFormField>
              </div>

              <div class="grid grid-cols-1 gap-4">
                <UFormField
                  label="Page Number"
                  name="currentPage"
                  class="hidden"
                >
                  <UInput
                    v-model.number="state.currentPage"
                    type="number"
                    min="0"
                    placeholder="0"
                    size="lg"
                    class="w-full"
                  />
                </UFormField>

                <UFormField label="Results per page" name="pageSize">
                  <USelect
                    v-model="state.pageSize"
                    :items="pageSizeOptions"
                    size="lg"
                    class="w-full"
                  />
                </UFormField>
              </div>
            </div>
          </UForm>
        </div>

        <!-- Right Side - Search Results -->
        <div class="overflow-y-auto p-8">
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <h3 class="text-base font-semibold">Search Results</h3>
              <span v-if="searchResults.length > 0" class="text-sm text-muted">
                {{ searchResults.length }} result(s)
              </span>
            </div>

            <!-- Loading State -->
            <div
              v-if="isSearching"
              class="flex items-center justify-center py-12"
            >
              <UIcon
                name="i-lucide-loader-circle"
                class="size-8 animate-spin text-muted"
              />
            </div>

            <!-- Empty State -->
            <div
              v-else-if="searchResults.length === 0 && !hasSearched"
              class="flex flex-col items-center justify-center py-12 text-center"
            >
              <UIcon name="i-lucide-search" class="size-12 text-muted mb-4" />
              <p class="text-sm text-muted">
                Configure your search filters and click Search to see results
              </p>
            </div>

            <!-- No Results State -->
            <div
              v-else-if="searchResults.length === 0 && hasSearched"
              class="flex flex-col items-center justify-center py-12 text-center"
            >
              <UIcon name="i-lucide-folder-x" class="size-12 text-muted mb-4" />
              <p class="text-sm text-muted">
                No directories found matching your search criteria
              </p>
            </div>

            <!-- Results List -->
            <div v-else class="space-y-2">
              <UButton
                variant="outline"
                @click="handleNavigate(item.id)"
                v-for="item in searchResults"
                :key="item.id"
                class="p-4 w-full cursor-pointer"
              >
                <template #leading>
                  <UIcon
                    name="i-lucide-folder"
                    class="size-5 text-muted shrink-0"
                  />
                </template>
                <template #default>
                  <div class="flex gap-3 items-start">
                    <div class="flex-1 min-w-0">
                      <p class="font-medium truncate text-left">
                        {{ item.name }}
                      </p>
                      <p class="text-xs text-muted truncate">
                        ID: {{ item.id }}
                      </p>
                    </div>
                  </div>
                </template>
              </UButton>
              <UButton
                v-if="hasMoreResults"
                block
                color="neutral"
                variant="outline"
                label="Load More"
                :loading="isLoadingMore"
                @click="loadMoreResults"
              />
            </div>
          </div>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between w-full">
        <UButton
          color="neutral"
          variant="ghost"
          label="Reset"
          @click="resetFilters"
        />
        <div class="flex gap-2">
          <UButton
            color="neutral"
            variant="outline"
            label="Cancel"
            @click="emit('close', false)"
          />
          <UButton
            label="Search"
            icon="i-lucide-search"
            @click="handleSubmit"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { ref, reactive, useTemplateRef } from "vue";
import {
  directorySearchQuerySchema,
  type DirectorySearchQuerySchema,
} from "@/schemas/search";
import {
  type DirectorySummaryDto,
} from "@/api/directory";
import { useDirectoryStore } from "@/stores/directory";
import type { FormSubmitEvent } from "@nuxt/ui";
import type { DateValue } from "@internationalized/date";
import { useRouter } from "vue-router";
import { OrderBy } from "@/enums/OrderBy";
import { SortDirection } from "@/enums/SortDirection";

const emit = defineEmits<{ close: [DirectorySearchQuerySchema | false] }>();
//TODO: Rewrite it with a proper query once I figure out the proper file searching api
const directoryStore = useDirectoryStore();
const hasMoreResults = ref(false);
const isLoadingMore = ref(false);
const router = useRouter();
// Search results state
const searchResults = ref<DirectorySummaryDto[]>([]);
const isSearching = ref(false);
const hasSearched = ref(false); // Initialize state with default values
const form = useTemplateRef("form");
const deletedAtRef = useTemplateRef("deletedAtRef");
const updatedAfterRef = useTemplateRef("updatedAfterRef");
const createdBeforeRef = useTemplateRef("createdBeforeRef");
const createdAfterRef = useTemplateRef("createdAfterRef");

const state = reactive<{
  nameContains: string | null;
  directoryId: string | null;
  parentDirectoryId: string | null;
  ownerId: string | null;
  isShared: boolean | null;
  createdAfter: DateValue | null;
  createdBefore: DateValue | null;
  updatedAfter: DateValue | null;
  deletedAt: DateValue | null;
  hasFiles: boolean | null;
  hasSubdirectories: boolean | null;
  isDeleted: boolean | null;
  isStarred: boolean | null;
  currentPage: number;
  pageSize: number;
  sortBy: OrderBy | undefined;
  sortDirection: SortDirection | undefined;
}>({
  nameContains: null,
  directoryId: null,
  parentDirectoryId: null,
  ownerId: null,
  isShared: null,
  createdAfter: null,
  createdBefore: null,
  updatedAfter: null,
  deletedAt: null,
  hasFiles: null,
  hasSubdirectories: null,
  isDeleted: false,
  isStarred: false,
  currentPage: 0,
  pageSize: 20,
  sortBy: undefined,
  sortDirection: undefined,
});

const handleNavigate = async (directoryId: string) => {
  router.push({
    name: "dashboard",
    params: { dirId: directoryId },
  });
  emit("close", false);
};

// Sort options based on DirectorySortBy enum
const sortByOptions = [
  { label: "Name", value: OrderBy.Name },
  { label: "Created At", value: OrderBy.CreatedAt },
  { label: "Updated At", value: OrderBy.UpdatedAt },
];

// Sort direction options based on SortDirection enum
const sortDirectionOptions = [
  { label: "Ascending", value:  SortDirection.Asc },
  { label: "Descending", value: SortDirection.Desc },
];

const onSubmit = async (event: FormSubmitEvent<DirectorySearchQuerySchema>) => {
  // Perform search
  isSearching.value = true;
  hasSearched.value = true;
  console.log(event.data);
  try {
    const response = await directoryStore.searchDirectory(event.data);

    if (response.success && response.data) {
      searchResults.value = response.data?.items;
      console.log("response data", response.data);

      hasMoreResults.value = response.data.hasNext;
    }
  } catch (error) {
    console.error("Search error:", error);
  } finally {
    isSearching.value = false;
  }
};

const loadMoreResults = async () => {
  isLoadingMore.value = true;
  state.currentPage++;

  try {
    const response = await directoryStore.searchDirectory(state);

    if (response.success && response.data) {
      searchResults.value.push(...response.data.items);
      hasMoreResults.value = response.data.hasNext;
    }
  } finally {
    isLoadingMore.value = false;
  }
};

const parentDirectoryOptions = ref<{ label: string; id: string }[]>([]);
const isLoadingParentDirs = ref(false);

const searchParentDirectory = async (query: string) => {
  if (!query.trim()) {
    return;
  }

  isLoadingParentDirs.value = true;
  try {
    const response = await directoryStore.searchDirectory({
      nameContains: query,
      hasSubdirectories: true,
      pageSize: 20,
      isDeleted: false,
    });

    if (response.success && response.data) {
      const newOptions = response.data.items.map((d) => ({
        label: d.name,
        id: d.id,
      }));

      const selectedId = state.parentDirectoryId;
      const selectedOption = parentDirectoryOptions.value.find(
        (o) => o.id === selectedId,
      );

      parentDirectoryOptions.value = selectedOption
        ? [selectedOption, ...newOptions.filter((o) => o.id !== selectedId)]
        : newOptions;
    }
  } finally {
    isLoadingParentDirs.value = false;
  }
};

const pageSizeOptions = [
  { label: "10 results", value: 10 },
  { label: "20 results", value: 20 },
  { label: "25 results", value: 25 },
  { label: "50 results", value: 50 },
  { label: "100 results", value: 100 },
];

// This is the best solution for what I want to do, if you don't believe me read this
// https://github.com/nuxt/ui/issues/668
const handleSubmit = () => {
  form.value?.submit();
};

const resetFilters = () => {
  Object.assign(state, {
    nameContains: null,
    parentDirectoryId: null,
    ownerId: null,
    isShared: null,
    createdAfter: null,
    createdBefore: null,
    updatedAfter: null,
    deletedAt: null,
    hasFiles: null,
    hasSubdirectories: null,
    isDeleted: false,
    isStarred: false,
    currentPage: 0,
    pageSize: 20,
    sortBy: undefined,
    sortDirection: undefined,
  });
  form.value?.clear();
  // Clear search results
  searchResults.value = [];
  hasSearched.value = false;
};
</script>
