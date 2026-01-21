<script setup lang="ts">
import { reactive } from "vue";
import {
  updateDirectorySchema,
  type UpdateDirectorySchema,
} from "@/schemas/directory";
import type { FormSubmitEvent } from "@nuxt/ui";
import { updateDirectory } from "@/mutations/directories";

const props = defineProps<{
  directoryId: string;
}>();

const { mutateAsync, state: mutationState } = updateDirectory();

const emit = defineEmits<{ close: [boolean] }>();

const state = reactive({
  name: "",
  directoryId: props.directoryId,
});

const onSubmit = async (event: FormSubmitEvent<UpdateDirectorySchema>) => {
  console.log("are we getting here?");
  await mutateAsync(event.data);
  console.log("directory-updated");
  if (!mutationState.value.error) emit("close", true);
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    :title="`Update directory`"
  >
    <template #body>
      <UForm
        :schema="updateDirectorySchema"
        :state="state"
        class="space-y-4 w-full"
        @submit="onSubmit"
      >
        <UFormField label="Name" name="name">
          <UInput v-model="state.name" class="w-full" />
        </UFormField>

        <div class="flex gap-2 w-full justify-end">
          <UButton
            color="neutral"
            label="Close"
            @click="emit('close', false)"
          />
          <UButton type="submit"> Submit </UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>
