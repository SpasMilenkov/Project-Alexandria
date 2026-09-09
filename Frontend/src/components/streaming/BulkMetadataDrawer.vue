<template>
  <UDrawer
    :open="open"
    title="Edit metadata"
    :description="description"
    :direction="isMobile ? 'bottom' : 'right'"
    :ui="{ content: glassDrawerContent }"
    @update:open="onOpenChange"
  >
    <template #body>
      <div class="flex flex-col gap-4 p-1">
        <UFormField
          v-for="field in fields"
          :key="field.key"
          :label="field.label"
          :name="`bulk-${field.key}`"
        >
          <UInput v-model="form[field.key]" class="w-full" :placeholder="field.placeholder" />
        </UFormField>

        <p class="text-xs text-gray-500 dark:text-gray-500 m-0">
          Only changed fields are applied to every selected file. Blank fields are left untouched.
        </p>

        <div class="flex gap-2 justify-end">
          <UButton color="neutral" variant="outline" label="Cancel" @click="emit('close')" />
          <UButton
            color="primary"
            label="Apply"
            :loading="isSaving"
            :disabled="!isSubmittable || isSaving"
            @click="onSubmit"
          />
        </div>
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { breakpointsTailwind, useBreakpoints } from "@vueuse/core";
import { computed, reactive, ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { useAppToast } from "@/composables/useAppToast";
import { bulkUpdateFileMetadata } from "@/mutations/files";
import { chunkArray } from "@/utils/chunk";
import { glassDrawerContent } from "@/utils/modalUi";

const BULK_CHUNK_SIZE = 100;

type FieldKey = "artist" | "album" | "title";

const { open, files } = defineProps<{
  open: boolean;
  files: MediaFileDto[];
}>();

const emit = defineEmits<{
  close: [];
  applied: [];
}>();

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

const appToast = useAppToast();
const { mutateAsync: bulkMutate } = bulkUpdateFileMetadata();
const isSaving = ref(false);

const onOpenChange = (value: boolean) => {
  if (!value) emit("close");
};

const description = computed(() => {
  const count = files.length;
  return `${count} file${count === 1 ? "" : "s"} selected`;
});

const distinctValues = (pick: (file: MediaFileDto) => string | null): string[] => {
  const values = new Set<string>();
  for (const file of files) {
    const value = pick(file)?.trim();
    if (value) values.add(value);
  }
  return [...values];
};

const initialFor = (pick: (file: MediaFileDto) => string | null): string => {
  const values = distinctValues(pick);
  return values.length === 1 ? values[0] : "";
};

const placeholderFor = (pick: (file: MediaFileDto) => string | null): string => {
  const values = distinctValues(pick);
  if (values.length > 1) return "Mixed";
  return "Unchanged";
};

const form = reactive<Record<FieldKey, string>>({ artist: "", album: "", title: "" });
const initial = reactive<Record<FieldKey, string>>({ artist: "", album: "", title: "" });

const resetForm = () => {
  initial.artist = initialFor((file) => file.artist);
  initial.album = initialFor((file) => file.album);
  initial.title = initialFor((file) => file.title);
  form.artist = initial.artist;
  form.album = initial.album;
  form.title = initial.title;
};

watch(
  () => open,
  (isOpen) => {
    if (isOpen) resetForm();
  },
  { immediate: true },
);

const fields = computed(() => [
  {
    key: "artist" as FieldKey,
    label: "Artist",
    placeholder: placeholderFor((file) => file.artist),
  },
  { key: "album" as FieldKey, label: "Album", placeholder: placeholderFor((file) => file.album) },
  { key: "title" as FieldKey, label: "Title", placeholder: placeholderFor((file) => file.title) },
]);

const patchFor = (key: FieldKey): string | undefined => {
  const trimmed = form[key].trim();
  if (!trimmed || trimmed === initial[key]) return undefined;
  return trimmed;
};

const isSubmittable = computed(
  () =>
    patchFor("artist") !== undefined ||
    patchFor("album") !== undefined ||
    patchFor("title") !== undefined,
);

const onSubmit = async () => {
  if (!isSubmittable.value || isSaving.value) return;
  isSaving.value = true;
  try {
    const patch = {
      artist: patchFor("artist"),
      album: patchFor("album"),
      title: patchFor("title"),
    };
    let succeeded = 0;
    let failed = 0;
    const chunks = chunkArray(
      files.map((file) => file.fileId),
      BULK_CHUNK_SIZE,
    );
    for (const chunk of chunks) {
      try {
        const response = await bulkMutate({ fileIds: chunk, ...patch });
        for (const result of response.results) {
          if (result.success) succeeded += 1;
          else failed += 1;
        }
      } catch {
        failed += chunk.length;
      }
    }
    if (failed === 0) appToast.success(`Updated ${succeeded} file${succeeded === 1 ? "" : "s"}`);
    else if (succeeded === 0) appToast.error("Bulk update failed", `${failed} files not updated`);
    else appToast.warning("Partially updated", `${succeeded} updated, ${failed} failed`);
    emit("applied");
  } finally {
    isSaving.value = false;
  }
};
</script>
