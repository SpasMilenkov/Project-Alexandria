<template>
  <UForm
    :schema="updateFileMetadataSchema"
    :state="form"
    class="flex flex-col gap-4"
    @submit="onSubmit"
    @keydown.esc="emit('cancel')"
  >
    <UFormField name="title" label="Title">
      <UInput
        v-model="form.title"
        placeholder="Untitled"
        size="xl"
        autofocus
        class="w-full"
        :ui="{ base: 'text-lg font-semibold' }"
      />
    </UFormField>

    <UFormField name="artist" label="Artist">
      <UInput v-model="form.artist" placeholder="Unknown artist" size="lg" class="w-full" />
    </UFormField>

    <div class="grid grid-cols-[minmax(0,1fr)_7rem] gap-4">
      <UFormField name="album" label="Album">
        <UInput v-model="form.album" placeholder="Unknown album" size="lg" class="w-full" />
      </UFormField>

      <UFormField name="year" label="Year">
        <UInput
          v-model="form.year"
          placeholder="Year"
          size="lg"
          inputmode="numeric"
          class="w-full"
          :ui="{ base: 'tabular-nums' }"
        />
      </UFormField>
    </div>

    <div class="flex items-center gap-2 pt-1">
      <UButton
        type="submit"
        color="primary"
        label="Save"
        :loading="isSaving"
        :disabled="!isDirty"
      />
      <UButton
        color="neutral"
        variant="ghost"
        label="Cancel"
        :disabled="isSaving"
        @click="emit('cancel')"
      />
    </div>
  </UForm>
</template>

<script setup lang="ts">
import { computed, reactive } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import { quoted } from "@/features/toast/format";
import { updateFileMetadata } from "@/mutations/files";
import { updateFileMetadataSchema } from "@/schemas/file";

interface EditableMediaMetadata {
  fileId: string;
  title?: string | null;
  artist?: string | null;
  album?: string | null;
  year?: string | number | null;
}

const { file } = defineProps<{ file: EditableMediaMetadata }>();

const emit = defineEmits<{ saved: []; cancel: [] }>();

const appToast = useAppToast("metadata");
const { mutateAsync, isLoading: isSaving } = updateFileMetadata();

const trackLabel = computed(() => (file.title ? quoted(file.title) : "this track"));

const yearToString = (year: EditableMediaMetadata["year"]) => {
  if (year === null || year === undefined) return "";

  return String(year);
};

const initial = {
  album: file.album ?? "",
  artist: file.artist ?? "",
  title: file.title ?? "",
  year: yearToString(file.year),
};

const form = reactive({ id: file.fileId, ...initial });

const isDirty = computed(
  () =>
    form.title !== initial.title ||
    form.artist !== initial.artist ||
    form.album !== initial.album ||
    form.year !== initial.year,
);

const trimmedOrUndefined = (value: string) => value.trim() || undefined;

const onSubmit = async () => {
  try {
    await mutateAsync({
      album: trimmedOrUndefined(form.album),
      artist: trimmedOrUndefined(form.artist),
      id: file.fileId,
      title: trimmedOrUndefined(form.title),
      year: trimmedOrUndefined(form.year),
    });
  } catch (error) {
    appToast.error(`Couldn't save details for ${trackLabel.value}`, error);

    return;
  }

  appToast.success(`Saved details for ${trackLabel.value}`);
  emit("saved");
};
</script>
