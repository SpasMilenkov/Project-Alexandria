<template>
  <UForm :schema="schema" :state="state" class="flex min-h-0 flex-1 flex-col" @submit="onSubmit">
    <div class="min-h-0 overflow-y-auto overscroll-contain p-6 md:p-8" :style="coverWash">
      <div class="grid grid-cols-1 gap-6 md:grid-cols-[13rem_minmax(0,1fr)] md:gap-8">
        <div class="flex min-w-0 flex-col gap-6 md:col-start-2 md:row-start-1">
          <UFormField label="Name" name="name" required>
            <UInput
              v-model="state.name"
              placeholder="Give your playlist a name"
              size="lg"
              class="w-full"
              :disabled="loading"
            />
          </UFormField>
          <UFormField label="Description" name="description" hint="Optional">
            <UTextarea
              v-model="state.description"
              placeholder="A mood, a moment, or a few words about this mix."
              :rows="4"
              :ui="{ base: 'md:h-52' }"
              size="lg"
              class="w-full"
              :disabled="loading"
            />
          </UFormField>
        </div>

        <UFormField
          label="Cover artwork"
          name="coverFile"
          hint="Optional"
          :error="coverError"
          class="min-w-0 md:col-start-1 md:row-start-1"
        >
          <div
            ref="coverDropZone"
            class="flex items-center gap-4 rounded-xl md:flex-col md:items-stretch"
            :class="{ 'outline-2 outline-offset-4 outline-primary': isOverDropZone && !loading }"
          >
            <div
              class="relative flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-gray-200/70 bg-elevated dark:border-gray-700/70 md:size-52"
            >
              <img
                v-if="coverPreview && !previewFailed"
                :src="coverPreview"
                alt="Playlist cover preview"
                class="size-full object-cover"
                @error="previewFailed = true"
              />
              <div v-else class="flex flex-col items-center gap-4 text-gray-600 dark:text-gray-400">
                <Icon icon="mdi:music-note-outline" class="size-8 md:size-12" />
                <span class="hidden text-sm md:block">Make it your own</span>
              </div>
            </div>
            <div
              class="flex min-w-0 flex-1 flex-col items-start gap-2 md:items-center md:text-center"
            >
              <input
                ref="coverInput"
                type="file"
                accept="image/jpeg,image/png,image/webp,image/gif"
                aria-label="Choose playlist cover"
                class="hidden"
                :disabled="loading"
                @change="onCoverChange"
              />
              <UButton
                type="button"
                color="neutral"
                variant="outline"
                size="lg"
                :disabled="loading"
                @click="coverInput?.click()"
              >
                {{ coverPreview ? "Change image" : "Choose image" }}
              </UButton>
              <p class="text-xs leading-5 text-gray-600 dark:text-gray-400">
                JPG, PNG, WebP or GIF · Max 4 MB
                <span class="hidden md:block">Or drop an image here</span>
              </p>
              <p
                v-if="state.coverFile"
                class="w-full truncate text-xs text-gray-600 dark:text-gray-400"
                :title="state.coverFile.name"
              >
                {{ state.coverFile.name }}
              </p>
              <UButton
                v-if="state.coverFile"
                type="button"
                color="neutral"
                variant="link"
                size="xs"
                :disabled="loading"
                @click="clearCoverSelection"
              >
                Clear selection
              </UButton>
            </div>
          </div>
        </UFormField>
      </div>
    </div>

    <div
      class="flex shrink-0 justify-end gap-2 border-t border-gray-200/70 bg-default p-4 dark:border-gray-700/70 sm:px-6 md:px-8"
    >
      <UButton
        type="button"
        color="neutral"
        variant="outline"
        size="lg"
        class="justify-center"
        :disabled="loading"
        @click="emit('cancel')"
      >
        Cancel
      </UButton>
      <UButton
        type="submit"
        color="primary"
        variant="solid"
        size="lg"
        class="flex-1 justify-center sm:flex-none"
        :loading="loading"
      >
        {{ isEdit ? "Save changes" : "Create playlist" }}
      </UButton>
    </div>
  </UForm>
</template>

<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { Icon } from "@iconify/vue";
import { useDropZone, useObjectUrl } from "@vueuse/core";
import { reactive, computed, ref, watch } from "vue";

import { playlistApi, type PlaylistResponse } from "@/api/playlist";
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
const coverInput = ref<HTMLInputElement | null>(null);
const coverDropZone = ref<HTMLElement | null>(null);
const previewFailed = ref(false);
const coverValidation = computed(() =>
  createPlaylistSchema.shape.coverFile.safeParse(state.coverFile),
);
const coverError = computed(() => {
  if (!coverValidation.value.success) return coverValidation.value.error.issues[0]?.message;
  if (previewFailed.value && state.coverFile)
    return "This image could not be previewed. Try another image.";
  return undefined;
});
const selectedCoverUrl = useObjectUrl(() => {
  if (!coverValidation.value.success) return undefined;
  return state.coverFile;
});
const coverPreview = computed(() => {
  if (state.coverFile) return selectedCoverUrl.value;
  if (props.initial?.hasCover) {
    return playlistApi.getPlaylistCoverUrl(props.initial.id, props.initial.updatedAt);
  }
  return undefined;
});
const coverWash = computed(() => {
  if (!ambientColor.value) return undefined;
  return { backgroundImage: `linear-gradient(135deg, ${ambientColor.value}28, transparent)` };
});

const selectCover = (file?: File) => {
  if (props.loading || !file) return;
  state.coverFile = file;
};

const onCoverChange = (event: Event) => {
  selectCover((event.target as HTMLInputElement).files?.[0]);
};

const clearCoverSelection = () => {
  state.coverFile = undefined;
  if (coverInput.value) coverInput.value.value = "";
};

const { isOverDropZone } = useDropZone(coverDropZone, {
  onDrop: (files) => selectCover(files?.[0]),
  multiple: false,
});

watch(coverPreview, () => {
  previewFailed.value = false;
});

const getDominantColor = (file: File): Promise<string> =>
  new Promise((resolve, reject) => {
    const img = new Image();
    const url = URL.createObjectURL(file);

    img.onload = () => {
      URL.revokeObjectURL(url);
      const canvas = document.createElement("canvas");
      canvas.width = 50;
      canvas.height = 50;
      const ctx = canvas.getContext("2d");
      if (!ctx) {
        reject(new Error("Canvas is unavailable"));
        return;
      }
      ctx.drawImage(img, 0, 0, 50, 50);
      const { data } = ctx.getImageData(0, 0, 50, 50);
      const counts = new Map<string, number>();

      for (let i = 0; i < data.length; i += 4) {
        if (data[i + 3] < 128) continue;
        const r = Math.min(255, Math.round(data[i] / 32) * 32);
        const g = Math.min(255, Math.round(data[i + 1] / 32) * 32);
        const b = Math.min(255, Math.round(data[i + 2] / 32) * 32);
        const key = `${r},${g},${b}`;
        counts.set(key, (counts.get(key) ?? 0) + 1);
      }

      const [dominant] = [...counts.entries()].sort((a, b) => b[1] - a[1]);
      if (!dominant) {
        reject(new Error("Image has no visible pixels"));
        return;
      }
      const [r, g, b] = dominant[0].split(",").map(Number);
      resolve(
        `#${r.toString(16).padStart(2, "0")}${g.toString(16).padStart(2, "0")}${b.toString(16).padStart(2, "0")}`,
      );
    };

    img.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error("Image could not be read"));
    };
    img.src = url;
  });

watch(
  () => state.coverFile,
  async (file, _previous, onCleanup) => {
    let cancelled = false;
    onCleanup(() => {
      cancelled = true;
    });
    ambientColor.value = props.initial?.ambientTheme ?? null;
    emit("ambientChange", ambientColor.value);
    if (!file || !coverValidation.value.success) return;

    try {
      const color = await getDominantColor(file);
      if (cancelled) return;
      ambientColor.value = color;
      emit("ambientChange", color);
    } catch {
      // Cover selection still works when a browser cannot extract its colors.
    }
  },
);

const onSubmit = (event: FormSubmitEvent<CreatePlaylistSchema>) => {
  emit("submit", { ...event.data, ambientTheme: ambientColor.value ?? undefined });
};
</script>
