<template>
  <div class="flex flex-col gap-5">
    <div
      class="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/15 text-primary transition-colors duration-300"
    >
      <Icon icon="mdi:lightning-bolt-outline" class="h-6 w-6" />
    </div>

    <div>
      <h2 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
        Automation
      </h2>
      <p class="mt-2 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
        Files you add to these folders, including subfolders, are processed automatically. Turn off
        anything you don't want; the folders are created either way.
      </p>
    </div>

    <div class="flex flex-col gap-3">
      <section
        v-for="draft in automatable"
        :key="draft.kind"
        class="flex flex-col gap-3 rounded-xl border border-gray-200/70 bg-neutral-50 p-4 dark:border-gray-700/70 dark:bg-neutral-800"
        :aria-label="`${folderName(draft)} automation`"
        @focusin="emit('focus', draft.kind)"
        @pointerenter="emit('focus', draft.kind)"
      >
        <div class="flex items-center gap-3">
          <Icon
            :icon="presetFor(draft.kind).icon"
            class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
          />

          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
              {{ folderName(draft) }}
            </p>
            <p class="text-xs text-gray-600 dark:text-gray-400">{{ summary(draft) }}</p>
          </div>

          <USwitch
            v-model="draft.automationEnabled"
            :aria-label="`Automate ${folderName(draft)}`"
          />
        </div>

        <template v-if="draft.automationEnabled">
          <div v-if="canTranscode(presetFor(draft.kind), capabilities)" class="flex flex-col gap-2">
            <p class="text-xs font-medium text-gray-600 dark:text-gray-400">Streaming quality</p>

            <div class="flex flex-wrap gap-1.5">
              <UButton
                v-for="option in qualityOptions(draft)"
                :key="option.value"
                size="xs"
                :label="option.label"
                :aria-pressed="isQualitySelected(draft, option.value)"
                :color="isQualitySelected(draft, option.value) ? 'primary' : 'neutral'"
                :variant="isQualitySelected(draft, option.value) ? 'soft' : 'outline'"
                @click="toggleQuality(draft, option.value)"
              />
            </div>
          </div>

          <UCheckbox
            v-if="canAutoTag(presetFor(draft.kind), capabilities)"
            v-model="draft.autoTag"
            label="Tag genre and mood automatically"
          />

          <p v-if="errors[draft.kind]" class="text-xs text-red-600 dark:text-red-400">
            {{ errors[draft.kind] }}
          </p>
        </template>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import type { AudioRung, VideoRung } from "@/api/policy";
import type { LibraryFolderKind } from "@/enums";

import {
  AUDIO_RUNG_OPTIONS,
  type LibraryCapabilities,
  type LibraryErrors,
  type LibraryFolderDraft,
  VIDEO_RUNG_OPTIONS,
  canAutoTag,
  canTranscode,
  isAutomatable,
  presetFor,
  willAutoTag,
  willTranscode,
} from "@/features/onboarding/library-setup";

const { capabilities } = defineProps<{
  capabilities: LibraryCapabilities;
  errors: LibraryErrors;
}>();

const emit = defineEmits<{ focus: [kind: LibraryFolderKind] }>();

const drafts = defineModel<LibraryFolderDraft[]>("drafts", { required: true });

const automatable = computed(() =>
  drafts.value.filter((draft) => isAutomatable(presetFor(draft.kind), capabilities)),
);

const folderName = (draft: LibraryFolderDraft) =>
  draft.name.trim() || presetFor(draft.kind).defaultName;

const isAudio = (draft: LibraryFolderDraft) => presetFor(draft.kind).transcode === "audio";

const summary = (draft: LibraryFolderDraft) => {
  const transcodes = willTranscode(draft, capabilities);
  const tags = willAutoTag(draft, capabilities);

  if (!transcodes && !tags) return "Plain folder, nothing runs automatically";
  if (!transcodes) return "New audio gets tagged automatically";
  if (isAudio(draft) && tags) return "Audio files get streamable copies and tags";
  if (isAudio(draft)) return "Audio files get streamable copies";

  return "Video files get streamable copies";
};

const qualityOptions = (draft: LibraryFolderDraft): { label: string; value: number }[] => {
  if (isAudio(draft)) return AUDIO_RUNG_OPTIONS;

  return VIDEO_RUNG_OPTIONS;
};

const isQualitySelected = (draft: LibraryFolderDraft, value: number) => {
  if (isAudio(draft)) return draft.audioRungs.includes(value as AudioRung);

  return draft.videoRungs.includes(value as VideoRung);
};

const toggleIn = <T>(list: T[], value: T) => {
  const index = list.indexOf(value);
  if (index === -1) list.push(value);
  else list.splice(index, 1);
};

const toggleQuality = (draft: LibraryFolderDraft, value: number) => {
  if (isAudio(draft)) toggleIn(draft.audioRungs, value as AudioRung);
  else toggleIn(draft.videoRungs, value as VideoRung);
};
</script>
