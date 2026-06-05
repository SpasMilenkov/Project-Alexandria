<template>
  <UForm :schema="schema" :state="state" class="p-6" @submit="onSubmit">
    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
      <div class="flex flex-col gap-4">
        <UFormField label="Name" name="name">
          <UInput v-model="state.name" placeholder="My Playlist" class="w-full" />
        </UFormField>
        <UFormField label="Description" name="description" class="flex-1 flex flex-col">
          <UTextarea
            v-model="state.description"
            placeholder="Optional description"
            :rows="5"
            class="w-full"
          />
        </UFormField>
        <div class="flex gap-2 justify-end mt-auto pt-2">
          <UButton color="neutral" label="Cancel" @click="emit('cancel')" />
          <UButton type="submit" color="primary" :loading="loading">
            {{ isEdit ? "Save" : "Create" }}
          </UButton>
        </div>
      </div>

      <UFormField label="Cover" name="coverFile" class="flex flex-col">
        <UFileUpload
          v-model="state.coverFile"
          accept="image/jpeg,image/png,image/webp,image/gif"
          class="h-full min-h-64"
        />
      </UFormField>
    </div>
  </UForm>
</template>

<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { reactive, computed, ref, watch } from "vue";

import type { PlaylistResponse } from "@/api/playlist";

import {
  createPlaylistSchema,
  updatePlaylistSchema,
  type CreatePlaylistSchema,
} from "@/schemas/playlist";

export interface PlaylistFormPayload extends CreatePlaylistSchema {
  ambientTheme?: string;
}

const props = defineProps<{
  initial?: PlaylistResponse;
  loading?: boolean;
}>();

const emit = defineEmits<{
  submit: [payload: PlaylistFormPayload];
  cancel: [];
  ambientChange: [color: string | null];
}>();

const isEdit = computed(() => !!props.initial);
const schema = computed(() => (isEdit.value ? updatePlaylistSchema : createPlaylistSchema));

const state = reactive({
  name: props.initial?.name ?? "",
  description: props.initial?.description ?? "",
  coverFile: undefined as File | undefined,
});

const ambientColor = ref<string | null>(props.initial?.ambientTheme ?? null);

const getDominantColor = (file: File): Promise<string> =>
  new Promise((resolve, reject) => {
    const img = new Image();
    const url = URL.createObjectURL(file);

    img.onload = () => {
      URL.revokeObjectURL(url);
      const canvas = document.createElement("canvas");
      canvas.width = 50;
      canvas.height = 50;
      const ctx = canvas.getContext("2d")!;
      ctx.drawImage(img, 0, 0, 50, 50);
      const { data } = ctx.getImageData(0, 0, 50, 50);
      const counts = new Map<string, number>();

      for (let i = 0; i < data.length; i += 4) {
        if (data[i + 3] < 128) continue;
        const r = Math.round(data[i] / 32) * 32;
        const g = Math.round(data[i + 1] / 32) * 32;
        const b = Math.round(data[i + 2] / 32) * 32;
        const key = `${r},${g},${b}`;
        counts.set(key, (counts.get(key) ?? 0) + 1);
      }

      const [dominant] = [...counts.entries()].sort((a, b) => b[1] - a[1]);
      const [r, g, b] = dominant[0].split(",").map(Number);
      resolve(
        `#${r.toString(16).padStart(2, "0")}${g.toString(16).padStart(2, "0")}${b.toString(16).padStart(2, "0")}`,
      );
    };

    img.onerror = reject;
    img.src = url;
  });

watch(
  () => state.coverFile,
  async (file) => {
    if (!file) {
      ambientColor.value = null;
      emit("ambientChange", null);
      return;
    }
    ambientColor.value = await getDominantColor(file);
    emit("ambientChange", ambientColor.value);
  },
);

const onSubmit = (event: FormSubmitEvent<CreatePlaylistSchema>) => {
  emit("submit", { ...event.data, ambientTheme: ambientColor.value ?? undefined });
};
</script>
