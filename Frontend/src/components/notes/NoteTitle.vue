<script setup lang="ts">
import { computed, nextTick, ref, useTemplateRef } from "vue";

import { isApiError } from "@/api/errors";
import { useAppToast } from "@/composables/useAppToast";
import { noteTitleOf } from "@/features/notes/note-name";
import { renameNote } from "@/mutations/notes";

// The note's name in the editor header. Clicking it edits the name in place; Enter or clicking
// away keeps the edit and Escape drops it. The extension isn't shown and can't be changed here.
const { fileId, fileName, directoryId } = defineProps<{
  fileId: string;
  fileName: string;
  directoryId: string | null;
}>();

const toast = useAppToast("files");
const { mutateAsync } = renameNote();

const isEditing = ref(false);
const draftName = ref("");
// Shown while the rename is on its way, so the header doesn't flick back to the old name.
const pendingName = ref<string | null>(null);

const input = useTemplateRef<{ inputRef?: HTMLInputElement | null }>("input");

const title = computed(() => noteTitleOf(fileName));
const shownTitle = computed(() => pendingName.value ?? title.value);

const beginEdit = async () => {
  draftName.value = title.value;
  isEditing.value = true;

  await nextTick();
  input.value?.inputRef?.select();
};

const cancel = () => {
  isEditing.value = false;
};

const failureMessage = (error: unknown, name: string): string => {
  if (isApiError(error) && error.status === 409) return `Another file in this folder is called "${name}"`;

  return "Couldn't rename the note";
};

// Enter and the blur that follows it both land here, so the first one wins and the second sees
// the editor already closed.
const commit = async () => {
  if (!isEditing.value) return;

  isEditing.value = false;

  const name = draftName.value.trim();

  if (name.length === 0 || name === title.value) return;

  pendingName.value = name;

  try {
    await mutateAsync({ directoryId, fileId, title: name });
  } catch (error) {
    toast.error(failureMessage(error, name), error);
  } finally {
    pendingName.value = null;
  }
};
</script>

<template>
  <h1 class="min-w-0 text-xl font-semibold text-gray-900 dark:text-gray-100">
    <UInput
      v-if="isEditing"
      ref="input"
      v-model="draftName"
      size="lg"
      aria-label="Note name"
      class="w-full min-w-48 sm:w-96"
      :ui="{ base: 'text-xl font-semibold' }"
      @keydown.enter.prevent="commit"
      @keydown.esc.prevent="cancel"
      @blur="commit"
    />

    <UTooltip v-else text="Rename">
      <button
        type="button"
        class="group -mx-1.5 flex max-w-full min-w-0 items-center gap-1.5 rounded-md px-1.5 py-0.5 text-left transition-colors duration-150 hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary motion-reduce:transition-none"
        :aria-label="`Rename ${shownTitle}`"
        @click="beginEdit"
      >
        <span class="truncate">{{ shownTitle }}</span>
        <UIcon
          name="i-lucide-pencil"
          class="h-4 w-4 shrink-0 text-gray-500 opacity-0 transition-opacity duration-150 group-hover:opacity-100 group-focus-visible:opacity-100 motion-reduce:transition-none dark:text-gray-500"
          aria-hidden="true"
        />
      </button>
    </UTooltip>
  </h1>
</template>
