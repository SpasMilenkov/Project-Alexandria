<template>
  <div class="flex flex-col items-center justify-center text-center px-6 py-16 min-h-[60vh]">
    <div
      class="w-16 h-16 rounded-full bg-gray-100/80 dark:bg-gray-800/60 flex items-center justify-center mb-4"
    >
      <Icon icon="mdi:folder-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
    </div>
    <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100 mb-1">Nothing here yet</h2>
    <p class="text-sm text-gray-600 dark:text-gray-400 max-w-sm mb-6">
      Upload files, create a folder, or drag items straight into this window.
    </p>
    <div class="flex items-center gap-2">
      <UButton color="primary" class="group" @click="emit('upload')">
        <span class="relative inline-block w-4 h-4 mr-2 align-middle">
          <Icon icon="mdi:cloud-upload-outline" class="w-4 h-4" />
        </span>
        Upload files
      </UButton>
      <UButton color="neutral" variant="outline" class="group" @click="emit('newFolder')">
        <span class="relative inline-block w-4 h-4 mr-2 align-middle">
          <Icon
            icon="mdi:folder-outline"
            class="absolute inset-0 w-4 h-4 transition-all duration-200 ease-out group-hover:opacity-0 group-hover:-translate-y-0.5 group-hover:scale-90"
          />
          <Icon
            icon="mdi:folder-open-outline"
            class="absolute inset-0 w-4 h-4 opacity-0 translate-y-0.5 scale-90 transition-all duration-200 ease-out group-hover:opacity-100 group-hover:translate-y-0 group-hover:scale-100"
          />
        </span>
        New folder
      </UButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";

const emit = defineEmits<{
  (e: "upload"): void;
  (e: "newFolder"): void;
}>();
</script>
