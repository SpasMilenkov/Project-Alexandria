<script setup lang="ts">
import { type Component, onErrorCaptured, ref } from "vue";

import type { HomeWidget } from "@/types/home";

defineProps<{ component: Component; widget: HomeWidget }>();

const failed = ref(false);
const attempt = ref(0);

onErrorCaptured(() => {
  failed.value = true;

  return false;
});

const retry = () => {
  attempt.value += 1;
  failed.value = false;
};
</script>

<template>
  <div v-if="failed" class="flex flex-col items-center gap-4 py-6 text-center" role="alert">
    <p class="text-sm text-gray-600 dark:text-gray-400">This widget could not be loaded.</p>

    <UButton color="neutral" variant="outline" @click="retry">Try again</UButton>
  </div>

  <Suspense v-else :key="attempt">
    <component :is="component" :widget="widget" />

    <template #fallback>
      <div
        class="flex min-h-24 items-center justify-center"
        role="status"
        aria-label="Loading widget"
      >
        <UIcon name="i-mdi-loading" class="size-6 animate-spin text-gray-500 dark:text-gray-500" />
      </div>
    </template>
  </Suspense>
</template>
