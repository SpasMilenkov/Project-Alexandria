<script setup lang="ts">
import { computed, ref, watch } from "vue";

import { useParentDirectoryPicker } from "@/composables/useParentDirectoryPicker";

const HOME_LABEL = "Home";

const {
  initialId = "",
  initialName = "",
  disabled = false,
} = defineProps<{
  initialId?: string;
  initialName?: string;
  disabled?: boolean;
}>();

const model = defineModel<string | undefined>({ default: undefined });

// Readable name of the current destination, so callers can say where things are going.
const destinationName = defineModel<string>("destinationName", { default: HOME_LABEL });

const { parentDirectoryOptions, isLoadingParentDirs, searchParentDirectory } =
  useParentDirectoryPicker(initialId || undefined, initialName || undefined);

// A later search can drop the chosen folder from the options, so its name is remembered here.
const lastKnownLabel = ref(initialId ? initialName : undefined);

// Clearing the menu yields null; uploads treat a missing id as Home.
const selectedId = computed({
  get: () => model.value,
  set: (value: string | null | undefined) => {
    model.value = value ?? undefined;
  },
});

const selectedLabel = computed(() => {
  if (!model.value) return HOME_LABEL;

  const option = parentDirectoryOptions.value.find((item) => item.id === model.value);

  return option?.label ?? lastKnownLabel.value ?? "Selected folder";
});

watch(
  selectedLabel,
  (label) => {
    if (model.value) lastKnownLabel.value = label;
    destinationName.value = label;
  },
  { immediate: true },
);
</script>

<template>
  <UFormField label="Destination">
    <USelectMenu
      v-model="selectedId"
      :items="parentDirectoryOptions"
      :loading="isLoadingParentDirs"
      :disabled="disabled"
      :clear="Boolean(model) && !disabled"
      placeholder="Search folders…"
      value-key="id"
      label-key="label"
      :debounce="300"
      class="w-full"
      @update:search-term="searchParentDirectory"
    >
      <template #default>
        <span class="flex min-w-0 items-center gap-2">
          <UIcon
            :name="model ? 'i-lucide-folder' : 'i-lucide-house'"
            class="size-4 shrink-0 text-gray-500 dark:text-gray-500"
          />
          <span class="truncate text-gray-900 dark:text-gray-100">{{ selectedLabel }}</span>
        </span>
      </template>
    </USelectMenu>
  </UFormField>
</template>
