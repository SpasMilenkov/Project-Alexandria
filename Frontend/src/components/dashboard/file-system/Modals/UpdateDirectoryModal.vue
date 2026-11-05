<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { reactive } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { quoted } from "@/features/toast/format";
import { updateDirectory } from "@/mutations/directories";
import { type UpdateDirectorySchema, updateDirectorySchema } from "@/schemas/directory";
import { logger } from "@/utils/logger";
import { glassModalContent } from "@/utils/modal-ui";

const { directoryId, currentName } = defineProps<{
  directoryId: string;
  currentName: string;
}>();

const { mutateAsync, state: mutationState } = updateDirectory();

const emit = defineEmits<{ close: [boolean] }>();

const appToast = useAppToast("folders");

useModalBackGuard(() => emit("close", false));

const state = reactive({
  directoryId,
  name: currentName,
});

const onSubmit = async (event: FormSubmitEvent<UpdateDirectorySchema>) => {
  logger.log("are we getting here?");
  try {
    await mutateAsync(event.data);
  } catch (err) {
    appToast.error(`Couldn't rename ${quoted(currentName)}`, err);
    return;
  }
  logger.log("directory-updated");
  if (!mutationState.value.error) {
    appToast.success(`Renamed ${quoted(currentName)} to ${quoted(event.data.name)}`);
    emit("close", true);
  }
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    :title="`Update directory`"
    :ui="{ content: glassModalContent }"
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
          <UButton color="neutral" label="Close" @click="emit('close', false)" />
          <UButton type="submit"> Submit </UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>
