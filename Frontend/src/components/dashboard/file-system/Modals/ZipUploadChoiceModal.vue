<template>
  <UModal
    title="Upload this ZIP"
    description="Unpack it into a folder, or keep it as a single file."
    :close="{ onClick: () => emit('close', null) }"
    :ui="{ body: glassModalBody, content: glassModalContent }"
  >
    <template #body>
      <ul
        class="divide-y divide-gray-200/70 overflow-hidden rounded-lg border border-gray-200/70 dark:divide-gray-700/70 dark:border-gray-700/70"
      >
        <li v-for="option in OPTIONS" :key="option.value">
          <button
            type="button"
            class="flex min-h-16 w-full items-center gap-3 px-4 py-3 text-left transition-colors duration-150 hover:bg-black/[0.03] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/60 dark:hover:bg-white/[0.04]"
            @click="emit('close', option.value)"
          >
            <span
              class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-gray-500/10 text-gray-600 dark:text-gray-400"
            >
              <UIcon :name="option.icon" class="size-5" />
            </span>

            <span class="min-w-0 flex-1">
              <span class="block text-sm font-medium text-gray-900 dark:text-gray-100">
                {{ option.label }}
              </span>
              <span class="mt-0.5 block text-xs text-gray-600 dark:text-gray-400">
                {{ option.description }}
              </span>
            </span>

            <UIcon
              name="i-lucide-chevron-right"
              class="size-4 shrink-0 text-gray-400 dark:text-gray-600"
            />
          </button>
        </li>
      </ul>
    </template>

    <template #footer>
      <div class="flex w-full justify-end">
        <UButton label="Cancel" variant="outline" color="neutral" @click="emit('close', null)" />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { glassModalBody, glassModalContent } from "@/utils/modal-ui";

type ZipChoice = "archive" | "file";

const OPTIONS: { description: string; icon: string; label: string; value: ZipChoice }[] = [
  {
    description: "Extract it and recreate its folders at the destination",
    icon: "i-lucide-package-open",
    label: "Unpack into a folder",
    value: "archive",
  },
  {
    description: "Upload the .zip unchanged, as one file",
    icon: "i-lucide-file-archive",
    label: "Keep as a single file",
    value: "file",
  },
];

const emit = defineEmits<{
  close: [value: ZipChoice | null];
}>();
</script>
