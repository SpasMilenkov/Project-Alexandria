<template>
  <UForm
    :schema="trackCoverSchema"
    :state="state"
    class="flex min-h-0 flex-1 flex-col"
    @submit="onSubmit"
  >
    <div class="flex min-h-0 flex-col gap-6 overflow-y-auto overscroll-contain p-6 md:p-8">
      <div v-if="file.coverUpdatedAt" class="flex flex-wrap items-center justify-between gap-2">
        <p class="m-0 text-sm text-gray-600 dark:text-gray-400">
          This version is using a custom cover.
        </p>

        <UButton
          type="button"
          color="error"
          variant="outline"
          size="sm"
          :loading="isRemoving"
          :disabled="isUploading"
          @click="onRemove"
        >
          Remove custom cover
        </UButton>
      </div>

      <UFormField
        label="Cover artwork"
        name="coverFile"
        description="Applies to the current version of this track. Other versions keep their own artwork."
        :error="coverError"
      >
        <div
          ref="dropZone"
          class="mt-2 flex items-center gap-4 rounded-xl sm:gap-6"
          :class="{ 'outline-2 outline-offset-4 outline-primary': isOverDropZone && !isBusy }"
        >
          <div
            class="relative size-24 shrink-0 overflow-hidden rounded-xl border border-gray-200/70 bg-elevated sm:size-40 dark:border-gray-700/70"
          >
            <img
              v-if="selectedCoverUrl && !previewFailed"
              :src="selectedCoverUrl"
              alt="Selected cover preview"
              class="size-full object-cover"
              @error="previewFailed = true"
            />
            <MediaArtwork
              v-else
              :file-id="file.fileId"
              :version-id="file.currentVersionId"
              :mime-type="file.mimeType"
              :cover-updated-at="file.coverUpdatedAt"
              :seed="coverSeed(file)"
              class="size-full"
            />
          </div>

          <div class="flex min-w-0 flex-1 flex-col items-start gap-2">
            <input
              ref="fileInput"
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif"
              aria-label="Choose cover image"
              class="hidden"
              :disabled="isBusy"
              @change="onFileChange"
            />

            <UButton
              type="button"
              color="neutral"
              variant="outline"
              size="lg"
              :disabled="isBusy"
              @click="fileInput?.click()"
            >
              {{ state.coverFile ? "Change image" : "Choose image" }}
            </UButton>

            <p class="m-0 text-xs leading-5 text-gray-600 dark:text-gray-400">
              JPG, PNG, WebP or GIF · Max 4 MB
              <span class="hidden sm:block">Or drop an image here</span>
            </p>

            <template v-if="state.coverFile">
              <p
                class="m-0 w-full truncate text-xs text-gray-600 dark:text-gray-400"
                :title="state.coverFile.name"
              >
                {{ state.coverFile.name }}
              </p>

              <UButton
                type="button"
                color="neutral"
                variant="link"
                size="xs"
                class="px-0"
                :disabled="isBusy"
                @click="clearSelection"
              >
                Clear selection
              </UButton>
            </template>
          </div>
        </div>
      </UFormField>
    </div>

    <div
      class="flex shrink-0 justify-end gap-2 border-t border-gray-200/70 bg-default p-4 dark:border-gray-700/70 sm:px-6 md:px-8"
    >
      <UButton
        type="button"
        color="neutral"
        variant="outline"
        size="lg"
        class="justify-center"
        :disabled="isBusy"
        @click="emit('cancel')"
      >
        Cancel
      </UButton>

      <UButton
        type="submit"
        color="primary"
        variant="solid"
        size="lg"
        class="flex-1 justify-center sm:flex-none"
        :loading="isUploading"
        :disabled="!canSave"
      >
        Save cover
      </UButton>
    </div>
  </UForm>
</template>

<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { useDropZone, useObjectUrl } from "@vueuse/core";
import { computed, reactive, ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import MediaArtwork from "@/components/streaming/overview/MediaArtwork.vue";
import { useAppToast } from "@/composables/useAppToast";
import { coverSeed } from "@/features/media/generated-cover";
import { quoted } from "@/features/toast/format";
import { removeTrackCover, uploadTrackCover } from "@/mutations/files";
import { type TrackCoverSchema, trackCoverSchema } from "@/schemas/file";

const { file } = defineProps<{ file: MediaFileDto }>();

const emit = defineEmits<{ saved: []; cancel: [] }>();

const appToast = useAppToast("metadata");
const { mutateAsync: upload, isLoading: isUploading } = uploadTrackCover();
const { mutateAsync: remove, isLoading: isRemoving } = removeTrackCover();

const state = reactive({ coverFile: undefined as File | undefined });

const fileInput = ref<HTMLInputElement | null>(null);
const dropZone = ref<HTMLElement | null>(null);
const previewFailed = ref(false);

const isBusy = computed(() => isUploading.value || isRemoving.value);
const trackLabel = computed(() => (file.title ? quoted(file.title) : "this track"));

const coverValidation = computed(() => {
  if (!state.coverFile) return null;

  return trackCoverSchema.shape.coverFile.safeParse(state.coverFile);
});

const coverError = computed(() => {
  if (coverValidation.value && !coverValidation.value.success) {
    return coverValidation.value.error.issues[0]?.message;
  }
  if (previewFailed.value) return "This image could not be previewed. Try another image.";

  return undefined;
});

const selectedCoverUrl = useObjectUrl(() => {
  if (!coverValidation.value?.success) return undefined;

  return state.coverFile;
});

const canSave = computed(
  () => Boolean(coverValidation.value?.success) && !previewFailed.value && !isRemoving.value,
);

const selectFile = (selected?: File) => {
  if (isBusy.value || !selected) return;

  state.coverFile = selected;
};

const onFileChange = (event: Event) => {
  selectFile((event.target as HTMLInputElement).files?.[0]);
};

const clearSelection = () => {
  state.coverFile = undefined;
  if (fileInput.value) fileInput.value.value = "";
};

const { isOverDropZone } = useDropZone(dropZone, {
  onDrop: (files) => selectFile(files?.[0]),
  multiple: false,
});

watch(
  () => state.coverFile,
  () => {
    previewFailed.value = false;
  },
);

const onSubmit = async (event: FormSubmitEvent<TrackCoverSchema>) => {
  try {
    await upload({
      fileId: file.fileId,
      versionId: file.currentVersionId,
      file: event.data.coverFile,
    });
  } catch (error) {
    appToast.error(`Couldn't update the cover for ${trackLabel.value}`, error);

    return;
  }

  appToast.success(`Updated the cover for ${trackLabel.value}`);
  emit("saved");
};

const onRemove = async () => {
  try {
    await remove({ fileId: file.fileId, versionId: file.currentVersionId });
  } catch (error) {
    appToast.error(`Couldn't remove the cover for ${trackLabel.value}`, error);

    return;
  }

  appToast.success(`Removed the custom cover for ${trackLabel.value}`);
  emit("saved");
};
</script>
