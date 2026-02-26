<template>
  <UModal
    :close="{ onClick: () => emit('close', 'close') }"
    title="Search"
    :ui="{ body: 'sm:p-2 ', header: 'hidden' }"
  >
    <template #body>
      <!-- @vue-expect-error -->
      <UForm
        :schema="unifiedSearchUiSchema"
        :state="state"
        ref="form"
        class="h-full w-full flex flex-col"
      >
        <UFormField class="w-full" name="nameContains">
          <UInput
            v-model="state.nameContains"
            @update:model-value="handleSubmit"
            :autofocus="true"
            placeholder="Search by name..."
            icon="i-lucide-text-cursor-input"
            size="xl"
            class="w-full"
          >
            <template #trailing>
              <UButton
                icon="mdi:arrow-expand"
                class="h-8 w-8"
                @click="emit('close', 'advanced')"
                variant="soft"
              />
            </template>
          </UInput>
        </UFormField>
      </UForm>
      <div class="flex-1 flex flex-col overflow-hidden h-80">
        <!-- Results Body (scrollable) -->
        <div class="flex-1 overflow-y-auto">
          <!-- Loading State -->
          <div v-if="isSearching" class="flex flex-col items-center justify-center py-15">
            <UIcon name="i-lucide-loader-circle" class="size-8 animate-spin text-muted mb-3" />
            <p class="text-sm text-muted">Searching...</p>
          </div>
          <!-- No Results State -->
          <div
            v-else-if="directoryResults.length === 0 && fileResults.length === 0"
            class="flex flex-col items-center justify-center py-15 text-center h-full"
          />

          <!-- Results List -->
          <div v-else class="py-4 px-0 space-y-6">
            <!-- Directory Results -->
            <div v-if="directoryResults.length > 0">
              <div class="flex items-center gap-2 mb-3 px-4">
                <UIcon name="i-lucide-folder" class="size-4 text-muted" />
                <h4 class="text-xs font-semibold uppercase tracking-wider text-muted">
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
              <div class="flex items-center gap-2 mb-3 px-4">
                <UIcon name="i-lucide-file" class="size-4 text-muted" />
                <h4 class="text-xs font-semibold uppercase tracking-wider text-muted">
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
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import {
  directorySearchApiSchema,
  fileSearchApiSchema,
  unifiedSearchUiSchema,
} from "@/schemas/search";
import { reactive, ref } from "vue";
import { useFileStore } from "@/stores/file";
import { useDirectoryStore } from "@/stores/directory";
import { useDebounceFn } from "@vueuse/core";
import { logger } from "@/utils/logger";

const directoryStore = useDirectoryStore();
const fileStore = useFileStore();

const fileResults = ref<FileResult[]>([]);
const directoryResults = ref<DirectorySummaryDto[]>([]);
const emit = defineEmits<{ close: [string | "root" | "advanced" | "close"] }>();
const createSearchState = () => unifiedSearchUiSchema.parse({});

const state = reactive(createSearchState());
const isSearching = ref(false);
const hasSearched = ref(false);
const form = ref();

const searchMode = ref<"both" | "files" | "directories">("both");

const handleSubmit = useDebounceFn(async () => {
  isSearching.value = true;
  hasSearched.value = true;
  state.currentPage = 0;
  try {
    if (searchMode.value === "both" || searchMode.value === "files") {
      const filesQuery = fileSearchApiSchema.parse(state);
      const result = await fileStore.searchFiles(filesQuery);
      fileResults.value = result.data?.items ?? [];
    }

    if (searchMode.value === "both" || searchMode.value === "directories") {
      const directoriesQuery = directorySearchApiSchema.parse(state);
      const result = await directoryStore.searchDirectory(directoriesQuery);
      directoryResults.value = result.data?.items ?? [];
    }
  } catch (error) {
    logger.error(error);
  } finally {
    isSearching.value = false;
  }
}, 200);

const handleNavigate = (id: string | null) => emit("close", id ?? "root");
</script>

<style scoped></style>
