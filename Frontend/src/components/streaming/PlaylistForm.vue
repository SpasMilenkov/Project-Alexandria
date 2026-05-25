<template>
  <UForm :schema="schema" :state="state" class="space-y-4 w-full" @submit="onSubmit">
    <UFormField label="Name" name="name">
      <UInput v-model="state.name" placeholder="My Playlist" class="w-full" />
    </UFormField>

    <UFormField label="Description" name="description">
      <UTextarea
        v-model="state.description"
        placeholder="Optional description"
        :rows="3"
        class="w-full"
      />
    </UFormField>

    <div class="flex gap-2 w-full justify-end">
      <UButton color="neutral" label="Cancel" @click="emit('cancel')" />
      <UButton type="submit" color="primary" :loading="loading">
        {{ isEdit ? "Save" : "Create" }}
      </UButton>
    </div>
  </UForm>
</template>

<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { reactive, computed } from "vue";

import type { PlaylistResponse } from "@/api/playlist";

import {
  createPlaylistSchema,
  updatePlaylistSchema,
  type CreatePlaylistSchema,
} from "@/schemas/playlist";

const props = defineProps<{
  initial?: PlaylistResponse;
  loading?: boolean;
}>();

const emit = defineEmits<{
  submit: [payload: CreatePlaylistSchema];
  cancel: [];
}>();

const isEdit = computed(() => !!props.initial);
const schema = computed(() => (isEdit.value ? updatePlaylistSchema : createPlaylistSchema));

const state = reactive({
  name: props.initial?.name ?? "",
  description: props.initial?.description ?? "",
});

const onSubmit = (event: FormSubmitEvent<CreatePlaylistSchema>) => {
  emit("submit", event.data);
};
</script>
