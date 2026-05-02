<script setup lang="ts">
import { useParentDirectoryPicker } from "@/composables/useParentDirectoryPicker";

const { initialId, initialName, disabled } = defineProps<{
  initialId?: string;
  initialName?: string;
  disabled?: boolean;
}>();

const model = defineModel<string | undefined>({ default: undefined });

const { parentDirectoryOptions, isLoadingParentDirs, searchParentDirectory } =
  useParentDirectoryPicker(initialId, initialName);
</script>

<template>
  <div class="space-y-1.5">
    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
      Upload to Directory
    </label>
    <USelectMenu
      v-model="model"
      :items="parentDirectoryOptions"
      :loading="isLoadingParentDirs"
      :disabled="disabled"
      placeholder="Search for directory..."
      value-key="id"
      display-key="label"
      searchable
      :debounce="300"
      class="w-full"
      @update:search-term="searchParentDirectory"
    >
      <template #default>
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-folder" class="size-4 text-muted" />
          <span v-if="model">
            {{ parentDirectoryOptions.find((i) => i.id === model)?.label }}
          </span>
          <span v-else class="text-muted">Root directory</span>
        </div>
      </template>
    </USelectMenu>
    <p class="text-xs text-muted">Leave empty to upload to the root directory</p>
  </div>
</template>
