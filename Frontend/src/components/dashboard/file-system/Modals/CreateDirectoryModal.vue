<script setup lang="ts">
import { reactive } from "vue";
import { useDirectoryStore } from "@/stores/directory";
import {
  createDirectorySchema,
  type CreateDirectorySchema,
} from "@/schemas/directory";
import type { FormSubmitEvent } from "@nuxt/ui";

const props = defineProps<{
  parentId: string | null;
}>();

const directoryStore = useDirectoryStore();

const emit = defineEmits<{ close: [boolean] }>();

const state = reactive({
  name: "",
  parentId: props.parentId,
});

const createDirectory = async (
  event: FormSubmitEvent<CreateDirectorySchema>,
) => {
  const response = await directoryStore.createDirectory(event.data);
  emit("close", response.success);
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    :title="`Create new directory`"
  >
    <template #body>
      <UForm
        :schema="createDirectorySchema"
        :state="state"
        class="space-y-4 w-full"
        @submit="createDirectory"
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
