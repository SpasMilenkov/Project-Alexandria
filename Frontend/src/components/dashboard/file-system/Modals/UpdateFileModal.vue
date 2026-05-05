<script setup lang="ts">
import { reactive } from "vue";
import { type UpdateFileMetadataSchema, updateFileMetadataSchema } from "@/schemas/file";
import type { FormSubmitEvent } from "@nuxt/ui";
import { updateFileMetadata } from "@/mutations/files";
import { useModalBackGuard } from "@/composables/useModalBackGuard";

const { fileId, originalName } = defineProps<{
  fileId: string;
  originalName: string;
}>();

const { mutateAsync, state: mutationState } = updateFileMetadata();
const emit = defineEmits<{ close: [boolean] }>();
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
  await mutateAsync({ ...event.data, name: composed });
  if (!mutationState.value.error) {
    emit("close", true);
  }
};
</script>

<template>
  <UModal :close="{ onClick: () => emit('close', false) }" title="Update file">
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
