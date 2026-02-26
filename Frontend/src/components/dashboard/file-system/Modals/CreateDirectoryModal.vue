<script setup lang="ts">
import { reactive } from "vue";
import { type CreateDirectorySchema, createDirectorySchema } from "@/schemas/directory";
import type { FormSubmitEvent } from "@nuxt/ui";
import { createDirectory } from "@/mutations/directories";
const props = defineProps<{
  parentId: string | null;
}>();

const { mutateAsync, state: mutationState } = createDirectory();

const emit = defineEmits<{ close: [boolean] }>();

const state = reactive({
  name: "",
  parentId: props.parentId,
});

const onSubmit = async (event: FormSubmitEvent<CreateDirectorySchema>) => {
  await mutateAsync(event.data);

  if (!mutationState.value.error) {
    emit("close", true);
  }
};
</script>

<template>
  <UModal :close="{ onClick: () => emit('close', false) }" :title="`Create new directory`">
    <template #body>
      <UForm
        :schema="createDirectorySchema"
        :state="state"
        class="space-y-4 w-full"
        @submit="onSubmit"
      >
        <UFormField label="Name" name="name">
          <UInput v-model="state.name" class="w-full" />
        </UFormField>

        <div class="flex gap-2 w-full justify-end">
          <UButton color="neutral" label="Close" @click="emit('close', false)" />
          <UButton type="submit"> Submit </UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>
