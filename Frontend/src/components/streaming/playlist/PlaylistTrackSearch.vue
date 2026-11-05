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
      <PlaylistTrackSearchRow
        v-for="file in results"
        :key="file.fileId"
        :file="file"
        :is-adding="addingIds.has(file.transpilationJobId)"
        :is-added="addedIds.has(file.transpilationJobId)"
        @add="emit('add', $event)"
      />
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

import PlaylistTrackSearchRow from "./PlaylistTrackSearchRow.vue";

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
      isVideo: false,
    }),
  staleTime: 15_000,
});

const results = computed(() => query.data.value?.items ?? []);
</script>
