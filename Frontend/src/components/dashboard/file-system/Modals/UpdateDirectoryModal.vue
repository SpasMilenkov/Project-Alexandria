<script setup lang="ts">
import { reactive } from "vue";
import { useDirectoryStore } from "@/stores/directory";
import {
  updateDirectorySchema,
  type UpdateDirectorySchema,
} from "@/schemas/directory";
import type { FormSubmitEvent } from "@nuxt/ui";

const props = defineProps<{
  directoryId: string;
}>();

const directoryStore = useDirectoryStore();

const emit = defineEmits<{ close: [boolean] }>();

const state = reactive({
  name: "",
  directoryId: props.directoryId,
});

const updateDirectory = async (
  event: FormSubmitEvent<UpdateDirectorySchema>,
) => {
  console.log("are we getting here?");
  const response = await directoryStore.updateDirectory(event.data);
  console.log("directory-updated");
  emit("close", response.success);
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
        @submit="updateDirectory"
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
