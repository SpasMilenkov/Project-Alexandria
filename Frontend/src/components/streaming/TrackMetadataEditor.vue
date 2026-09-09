<template>
  <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-6' }">
    <template #header>
      <div class="flex items-center justify-between gap-2">
        <span class="font-semibold text-sm text-gray-700 dark:text-gray-300">Details</span>
        <div class="flex items-center gap-2">
          <UButton
            v-if="isEditing"
            color="error"
            variant="outline"
            size="xs"
            label="Reset"
            :disabled="!isDirty || isSaving"
            @click="resetForm"
          />
          <UButton
            :icon="isEditing ? 'mdi:close' : 'mdi:pencil-outline'"
            color="neutral"
            variant="ghost"
            size="xs"
            :aria-label="isEditing ? 'Cancel editing' : 'Edit details'"
            @click="toggleEditing"
          />
        </div>
      </div>
    </template>

    <!-- VIEW MODE -->
    <div v-if="!isEditing" class="flex flex-col gap-4">
      <div>
        <p class="text-xs text-gray-500 dark:text-gray-500 mb-1">Title</p>
        <p class="text-xl font-semibold text-gray-900 dark:text-gray-100">
          {{ file.title || "Untitled" }}
        </p>
      </div>

      <div>
        <p class="text-xs text-gray-500 dark:text-gray-500 mb-1">Artist</p>
        <p class="text-base font-medium text-gray-800 dark:text-gray-200">
          {{ file.artist || "Unknown artist" }}
        </p>
      </div>

      <div class="grid grid-cols-2 gap-4">
        <div>
          <p class="text-xs text-gray-500 dark:text-gray-500 mb-1">Album</p>
          <p class="text-sm text-gray-700 dark:text-gray-300">
            {{ file.album || "Unknown album" }}
          </p>
        </div>
        <div>
          <p class="text-xs text-gray-500 dark:text-gray-500 mb-1">Year</p>
          <p class="text-sm text-gray-700 dark:text-gray-300">
            {{ file.year || "Unknown year" }}
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2 pt-1">
        <span class="text-xs text-gray-500 dark:text-gray-500">Genre</span>
        <UBadge color="neutral" variant="subtle" :label="file.genre ?? 'Unknown genre'" />
        <UTooltip text="Set automatically from audio analysis, this can't be edited">
          <UIcon
            name="mdi:information-outline"
            class="w-3.5 h-3.5 text-gray-400 dark:text-gray-600"
          />
        </UTooltip>
      </div>
    </div>

    <!-- EDIT MODE -->
    <UForm
      v-else
      :schema="updateFileMetadataSchema"
      :state="form"
      class="flex flex-col gap-4"
      @submit="onSubmit"
    >
      <UFormField
        name="title"
        label="Title"
        :ui="{ label: 'text-xs text-gray-500 dark:text-gray-500 font-normal mb-1' }"
      >
        <UInput
          v-model="form.title"
          placeholder="Untitled"
          class="w-full"
          :ui="{
            base: 'bg-transparent border-0 border-b border-gray-200/70 dark:border-gray-700/70 rounded-none ring-0 focus:ring-0 focus-visible:ring-0 px-0 pb-2 text-xl font-semibold text-gray-900 dark:text-gray-100 focus:border-primary transition-colors',
          }"
        />
      </UFormField>

      <UFormField
        name="artist"
        label="Artist"
        :ui="{ label: 'text-xs text-gray-500 dark:text-gray-500 font-normal mb-1' }"
      >
        <UInput
          v-model="form.artist"
          placeholder="Unknown artist"
          class="w-full"
          :ui="{
            base: 'bg-transparent border-0 border-b border-gray-200/70 dark:border-gray-700/70 rounded-none ring-0 focus:ring-0 focus-visible:ring-0 px-0 pb-2 text-base font-medium text-gray-800 dark:text-gray-200 focus:border-primary transition-colors',
          }"
        />
      </UFormField>

      <div class="grid grid-cols-2 gap-4">
        <UFormField
          name="album"
          label="Album"
          :ui="{ label: 'text-xs text-gray-500 dark:text-gray-500 font-normal mb-1' }"
        >
          <UInput
            v-model="form.album"
            placeholder="Unknown album"
            class="w-full"
            :ui="{
              base: 'bg-transparent border-0 border-b border-gray-200/70 dark:border-gray-700/70 rounded-none ring-0 focus:ring-0 focus-visible:ring-0 px-0 pb-1.5 text-sm text-gray-700 dark:text-gray-300 focus:border-primary transition-colors',
            }"
          />
        </UFormField>

        <UFormField
          name="year"
          label="Year"
          :ui="{ label: 'text-xs text-gray-500 dark:text-gray-500 font-normal mb-1' }"
        >
          <UInput
            v-model="form.year"
            placeholder="Unknown year"
            class="w-full"
            :ui="{
              base: 'bg-transparent border-0 border-b border-gray-200/70 dark:border-gray-700/70 rounded-none ring-0 focus:ring-0 focus-visible:ring-0 px-0 pb-1.5 text-sm text-gray-700 dark:text-gray-300 focus:border-primary transition-colors',
            }"
          />
        </UFormField>
      </div>

      <div class="flex items-center gap-2 pt-1">
        <span class="text-xs text-gray-500 dark:text-gray-500">Genre</span>
        <UBadge color="neutral" variant="subtle" :label="file.genre ?? 'Unknown genre'" />
        <UTooltip text="Set automatically from audio analysis, this can't be edited">
          <UIcon
            name="mdi:information-outline"
            class="w-3.5 h-3.5 text-gray-400 dark:text-gray-600"
          />
        </UTooltip>
      </div>

      <div class="flex justify-end pt-3 border-t border-gray-200/60 dark:border-gray-700/60">
        <UButton
          type="submit"
          color="primary"
          label="Save"
          :loading="isSaving"
          :disabled="!isDirty"
        />
      </div>
    </UForm>
  </UCard>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useAppToast } from "@/composables/useAppToast";
import { updateFileMetadata } from "@/mutations/files";
import { updateFileMetadataSchema } from "@/schemas/file";

const { file } = defineProps<{ file: MediaFileDto }>();

const appToast = useAppToast();
const { mutateAsync, state: mutationState, isLoading: isSaving } = updateFileMetadata();

const isEditing = ref(false);

const form = reactive({
  id: file.fileId,
  title: file.title ?? "",
  artist: file.artist ?? "",
  album: file.album ?? "",
  year: file.year ?? "",
});

const snapshot = () =>
  JSON.stringify({
    title: form.title,
    artist: form.artist,
    album: form.album,
    year: form.year,
  });

const pristine = ref(snapshot());
const isDirty = computed(() => snapshot() !== pristine.value);

const resetForm = () => {
  form.title = file.title ?? "";
  form.artist = file.artist ?? "";
  form.album = file.album ?? "";
  form.year = file.year ?? "";
  pristine.value = snapshot();
};

const toggleEditing = () => {
  if (isEditing.value && isDirty.value) {
    resetForm();
  }
  isEditing.value = !isEditing.value;
};

watch(
  () => file.fileId,
  () => {
    resetForm();
    isEditing.value = false;
  },
);

const onSubmit = async () => {
  await mutateAsync({
    id: file.fileId,
    title: form.title.trim() ? form.title.trim() : undefined,
    artist: form.artist.trim() ? form.artist.trim() : undefined,
    album: form.album.trim() ? form.album.trim() : undefined,
    year: form.year.trim() ? form.year.trim() : undefined,
  });
  if (mutationState.value.error) {
    appToast.error("Failed to save details");
    return;
  }
  pristine.value = snapshot();
  isEditing.value = false;
  appToast.success("Details saved");
};
</script>
