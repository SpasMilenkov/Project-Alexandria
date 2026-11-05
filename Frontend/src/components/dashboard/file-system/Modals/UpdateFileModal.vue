<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { reactive } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { quoted } from "@/features/toast/format";
import { updateFileMetadata } from "@/mutations/files";
import { type UpdateFileMetadataSchema, updateFileMetadataSchema } from "@/schemas/file";
import { glassModalContent } from "@/utils/modal-ui";

const { fileId, originalName } = defineProps<{
  fileId: string;
  originalName: string;
}>();

const { mutateAsync, state: mutationState } = updateFileMetadata();
const emit = defineEmits<{ close: [boolean] }>();

const appToast = useAppToast("files");
useModalBackGuard(() => emit("close", false));

// Leading-dot files (.gitignore, .env) have no real extension
const getExtension = (filename: string): string => {
  const lastDot = filename.lastIndexOf(".");
  return lastDot > 0 ? filename.slice(lastDot) : "";
};

const extension = getExtension(originalName);
const baseName = extension ? originalName.slice(0, -extension.length) : originalName;

const state = reactive({
  id: fileId,
  name: baseName,
});

const onSubmit = async (event: FormSubmitEvent<UpdateFileMetadataSchema>) => {
  const composed = state.name.trim() + extension;
  try {
    await mutateAsync({ ...event.data, name: composed });
  } catch (err) {
    appToast.error(`Couldn't rename ${quoted(originalName)}`, err);
    return;
  }
  if (!mutationState.value.error) {
    appToast.success(`Renamed ${quoted(originalName)} to ${quoted(composed)}`);
    emit("close", true);
  }
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Update file"
    :ui="{ content: glassModalContent }"
  >
    <template #body>
      <UForm
        :schema="updateFileMetadataSchema"
        :state="state"
        class="space-y-4 w-full"
        @submit="onSubmit"
      >
        <UFormField label="Name" name="name">
          <UInput v-model="state.name" class="w-full">
            <template v-if="extension" #trailing>
              <span class="text-muted text-sm select-none">{{ extension }}</span>
            </template>
          </UInput>
        </UFormField>
        <div class="flex gap-2 w-full justify-end">
          <UButton color="neutral" label="Close" @click="emit('close', false)" />
          <UButton type="submit">Submit</UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>
