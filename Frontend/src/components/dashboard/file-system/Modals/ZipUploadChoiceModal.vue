<template>
  <UModal :title="'How would you like to upload this archive?'">
    <template #body>
      <div class="flex flex-col gap-3 pb-2">
        <button
          class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl border border-gray-200 dark:border-gray-700 hover:border-primary/50 hover:bg-primary/5 transition-colors text-left"
          @click="emit('close', 'archive')"
        >
          <div class="flex items-center justify-center w-10 h-10 rounded-xl bg-primary/10 shrink-0">
            <Icon icon="formkit:zip" class="w-5 h-5 text-primary" />
          </div>
          <div>
            <p class="text-sm font-medium">Upload as Archive</p>
            <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
              Extract the zip and recreate its folder structure
            </p>
          </div>
        </button>

        <button
          class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl border border-gray-200 dark:border-gray-700 hover:border-primary/50 hover:bg-primary/5 transition-colors text-left"
          @click="emit('close', 'file')"
        >
          <div class="flex items-center justify-center w-10 h-10 rounded-xl bg-primary/10 shrink-0">
            <Icon icon="mdi:file-outline" class="w-5 h-5 text-primary" />
          </div>
          <div>
            <p class="text-sm font-medium">Upload as File</p>
            <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
              Keep the zip intact and upload it as a single file
            </p>
          </div>
        </button>
      </div>
    </template>

    <template #footer>
      <UButton variant="ghost" color="neutral" @click="emit('close', null)">Cancel</UButton>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";

const emit = defineEmits<{
  close: [value: "archive" | "file" | null];
}>();
</script>
