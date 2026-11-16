<template>
  <div class="flex w-full flex-col gap-3" aria-hidden="true">
    <div
      class="flex flex-wrap items-center justify-center gap-1.5 text-xs text-gray-600 dark:text-gray-400"
    >
      <span>Right-click a folder</span>
      <Icon icon="mdi:chevron-right" class="h-3.5 w-3.5" />
      <span class="flex items-center gap-1 font-medium text-gray-900 dark:text-gray-100">
        <Icon icon="mdi:information-outline" class="h-3.5 w-3.5" />
        View details
      </span>
      <Icon icon="mdi:chevron-right" class="h-3.5 w-3.5" />
      <span class="font-medium text-gray-900 dark:text-gray-100">Automation</span>
    </div>

    <div
      class="ms-auto flex w-full max-w-sm flex-col gap-4 rounded-lg bg-white p-4 shadow-2xl ring-1 shadow-black/15 ring-black/10 dark:bg-neutral-900 dark:shadow-black/40 dark:ring-white/10"
    >
      <div class="flex items-center gap-3">
        <span
          class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-neutral-100 text-gray-600 dark:bg-neutral-800 dark:text-gray-400"
        >
          <Icon icon="mdi:folder" class="h-6 w-6" />
        </span>

        <div class="min-w-0">
          <p class="truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
            {{ folderName }}
          </p>
          <p class="text-xs text-gray-500 dark:text-gray-500">Folder</p>
        </div>
      </div>

      <div class="flex flex-col gap-2">
        <p class="text-xs font-semibold text-gray-600 dark:text-gray-400">Details</p>
        <div class="grid grid-cols-2 gap-2">
          <span class="h-9 rounded-lg bg-neutral-100 dark:bg-neutral-800" />
          <span class="h-9 rounded-lg bg-neutral-100 dark:bg-neutral-800" />
        </div>
      </div>

      <div class="flex flex-col gap-3 rounded-xl p-3 ring-2 ring-primary/60">
        <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">Automation</p>

        <div
          v-if="rules.length === 0"
          class="flex flex-col items-center gap-1 rounded-xl border border-dashed border-gray-300/80 px-4 py-4 text-center dark:border-gray-700/80"
        >
          <Icon
            icon="mdi:lightning-bolt-outline"
            class="h-6 w-6 text-gray-400 dark:text-gray-600"
          />
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
            No automation configured
          </p>
          <p class="text-xs text-gray-600 dark:text-gray-400">You can set it up here any time.</p>
        </div>

        <template v-else>
          <div
            v-for="rule in rules"
            :key="rule.key"
            class="flex items-center gap-3 rounded-xl border border-gray-200/70 px-3 py-2.5 dark:border-gray-700/70"
          >
            <div
              class="flex h-7 w-7 shrink-0 items-center justify-center rounded-md"
              :class="rule.colorClass"
            >
              <Icon :icon="rule.icon" class="h-4 w-4" />
            </div>

            <div class="min-w-0 flex-1">
              <div class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                {{ rule.label }}
              </div>
              <div class="truncate text-xs text-gray-600 dark:text-gray-400">
                {{ rule.detail }}
              </div>
            </div>
          </div>

          <div
            class="flex items-center justify-between gap-4 rounded-xl border border-gray-200/70 px-3 py-2.5 dark:border-gray-700/70"
          >
            <div class="min-w-0">
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Include subfolders</p>
              <p class="text-xs text-gray-600 dark:text-gray-400">
                Rules also run for files in nested folders.
              </p>
            </div>

            <USwitch :model-value="true" disabled tabindex="-1" />
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import { PolicyActionType, PolicyTriggerType } from "@/api/policy";
import {
  AUDIO_RUNG_OPTIONS,
  type LibraryCapabilities,
  type LibraryFolderDraft,
  VIDEO_RUNG_OPTIONS,
  canAutoTag,
  canTranscode,
  isDraftAutomated,
  presetFor,
} from "@/features/onboarding/library-setup";
import {
  policyActionPresentation,
  policyTriggerLabel,
} from "@/features/policies/rule-presentation";

const { capabilities, draft } = defineProps<{
  draft: LibraryFolderDraft;
  capabilities: LibraryCapabilities;
}>();

interface PreviewRule {
  key: string;
  icon: string;
  colorClass: string;
  label: string;
  detail: string;
}

const preset = computed(() => presetFor(draft.kind));

const folderName = computed(() => draft.name.trim() || preset.value.defaultName);

const qualityLabels = (): string[] => {
  if (preset.value.transcode === "audio") {
    return AUDIO_RUNG_OPTIONS.filter((o) => draft.audioRungs.includes(o.value)).map((o) => o.label);
  }

  return VIDEO_RUNG_OPTIONS.filter((o) => draft.videoRungs.includes(o.value)).map((o) => o.label);
};

const groupFor = (): string => {
  if (preset.value.transcode === "video") return "Videos";

  return "Audio";
};

const toPreviewRule = (actionType: PolicyActionType, detail: string): PreviewRule => {
  const presentation = policyActionPresentation(actionType);

  return {
    colorClass: presentation.colorClass,
    detail,
    icon: presentation.icon,
    key: String(actionType),
    label: presentation.label,
  };
};

const rules = computed<PreviewRule[]>(() => {
  if (!isDraftAutomated(draft, capabilities)) return [];

  const list: PreviewRule[] = [];
  const trigger = policyTriggerLabel(PolicyTriggerType.FileGroup, groupFor());

  const qualities = qualityLabels();
  if (canTranscode(preset.value, capabilities) && qualities.length > 0) {
    list.push(toPreviewRule(PolicyActionType.Transcode, `${trigger} · ${qualities.join(", ")}`));
  }

  if (canAutoTag(preset.value, capabilities) && draft.autoTag) {
    list.push(toPreviewRule(PolicyActionType.AutoTag, trigger));
  }

  return list;
});
</script>
