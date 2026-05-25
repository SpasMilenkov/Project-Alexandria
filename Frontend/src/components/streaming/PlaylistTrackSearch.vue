<template>
  <div class="space-y-3">
    <UInput
      v-model="searchTerm"
      placeholder="Search tracks..."
      icon="i-heroicons-magnifying-glass"
      class="w-full"
      autofocus
    />

    <div v-if="query.status.value === 'pending'" class="flex justify-center py-6">
      <UIcon name="mdi:loading" class="w-5 h-5 animate-spin text-muted" />
    </div>

    <div
      v-else-if="results.length"
      class="flex flex-col divide-y divide-gray-100/50 dark:divide-gray-800/50 border border-gray-200/70 dark:border-gray-700/70 rounded-xl overflow-hidden max-h-80 overflow-y-auto"
    >
      <div
        v-for="file in results"
        :key="file.fileId"
        class="flex items-center gap-3 px-3 py-2.5 bg-white/40 dark:bg-white/3"
      >
        <div
          class="w-9 h-9 rounded-md bg-gray-100/60 dark:bg-gray-800/40 flex items-center justify-center overflow-hidden shrink-0"
        >
          <UIcon name="mdi:music-note" class="w-4 h-4 text-muted" />
        </div>
        <div class="flex-1 min-w-0">
          <p class="text-sm font-medium text-highlighted truncate">
            {{ file.title ?? file.fileName }}
          </p>
          <p v-if="file.artist" class="text-xs text-muted truncate">{{ file.artist }}</p>
        </div>
        <UButton
          :icon="addedIds.has(file.transpilationJobId) ? 'mdi:check' : 'i-heroicons-plus'"
          color="primary"
          variant="ghost"
          size="xs"
          :loading="addingIds.has(file.transpilationJobId)"
          :disabled="addedIds.has(file.transpilationJobId)"
          class="shrink-0"
          @click="emit('add', file.transpilationJobId)"
        />
      </div>
    </div>

    <p
      v-else-if="searchTerm.length > 0 && !query.isLoading"
      class="text-sm text-muted text-center py-4"
    >
      No tracks found.
    </p>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useDebounceFn } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import { streamingApi } from "@/api/streaming";
import { STREAMING_QUERY_KEYS } from "@/queries/streaming";

const props = defineProps<{
  addingIds: Set<string>;
  addedIds: Set<string>;
}>();

const emit = defineEmits<{
  add: [transpilationJobId: string];
}>();

const searchTerm = ref("");
const debouncedQuery = ref("");

const setDebounced = useDebounceFn((val: string) => {
  debouncedQuery.value = val;
}, 250);

watch(searchTerm, (val) => setDebounced(val));

const query = useQuery({
  key: () => [...STREAMING_QUERY_KEYS.root, "files-for-streaming", debouncedQuery.value, 1, 20],
  query: () =>
    streamingApi.getFilesForStreaming({
      query: debouncedQuery.value || null,
      page: 1,
      pageSize: 20,
    }),
  staleTime: 15_000,
});

const results = computed(() => query.data.value?.items ?? []);
</script>
