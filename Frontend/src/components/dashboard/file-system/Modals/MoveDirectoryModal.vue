<script setup lang="ts">
import { reactive } from "vue";
import { useDirectoryStore } from "@/stores/directory";
import {
  moveDirectorySchema,
  type MoveDirectorySchema,
} from "@/schemas/directory";
import type { FormSubmitEvent } from "@nuxt/ui";

const props = defineProps<{
  directoryId: string;
}>();

const directoryStore = useDirectoryStore();

const emit = defineEmits<{ close: [boolean] }>();

const state = reactive({
  directoryId: props.directoryId,
  destinationId: "",
});

const createDirectory = async (event: FormSubmitEvent<MoveDirectorySchema>) => {
  const response = await directoryStore.moveDirectory(event.data);
  emit("close", response.success);
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    :title="`Move directory`"
  >
    <template #body>
      <UForm
        :schema="moveDirectorySchema"
        :state="state"
        class="space-y-4 w-full"
        @submit="createDirectory"
      >


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
