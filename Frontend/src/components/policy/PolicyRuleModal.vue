<template>
  <UModal
    v-model:open="open"
    :title="isEditing ? 'Edit Rule' : 'Add Automation Rule'"
    :ui="{ content: 'max-w-lg' }"
  >
    <template #body>
      <div class="flex flex-col gap-6 p-1">
        <!-- Action type -->
        <UFormField label="Action" :error="errors.actionType">
          <div class="grid grid-cols-3 gap-2">
            <button
              v-for="opt in actionTypeOptions"
              :key="opt.value"
              type="button"
              class="flex flex-col items-center gap-2 p-3 rounded-lg border-2 transition-colors"
              :class="
                form.actionType === opt.value
                  ? 'border-primary bg-primary/10 text-primary'
                  : 'border-neutral-200 dark:border-neutral-700 hover:border-neutral-300 dark:hover:border-neutral-600'
              "
              :disabled="isEditing"
              @click="onActionTypeChange(opt.value)"
            >
              <Icon :icon="opt.icon" class="w-5 h-5" />
              <span class="text-xs font-medium">{{ opt.label }}</span>
            </button>
          </div>
        </UFormField>

        <USeparator />

        <!-- Applies to trigger -->
        <div v-if="form.actionType !== null" class="flex flex-col gap-3">
          <div class="flex items-center justify-between">
            <span class="text-sm font-medium">Applies to</span>
            <div class="flex items-center gap-2">
              <span class="text-xs text-gray-500">All eligible files</span>
              <USwitch v-model="form.anyFile" @update:model-value="onAnyFileToggle" />
            </div>
          </div>
          <Transition name="fade">
            <div v-if="!form.anyFile" class="flex flex-col gap-2">
              <p class="text-xs text-gray-500 dark:text-gray-400">
                Choose which file types this rule applies to
              </p>
              <div
                v-if="eligibleGroups.length > 0"
                class="grid gap-2"
                :class="eligibleGroups.length <= 4 ? 'grid-cols-2' : 'grid-cols-3'"
              >
                <button
                  v-for="group in eligibleGroups"
                  :key="group.value"
                  type="button"
                  class="flex items-center gap-2 px-3 py-2 rounded-lg border-2 text-left transition-colors"
                  :class="
                    form.selectedGroups.includes(group.value)
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-neutral-200 dark:border-neutral-700 hover:border-neutral-300 dark:hover:border-neutral-600'
                  "
                  @click="toggleGroup(group.value)"
                >
                  <Icon :icon="group.icon" class="w-4 h-4 shrink-0" />
                  <span class="text-xs font-medium truncate">{{ group.label }}</span>
                </button>
              </div>
              <p v-if="errors.selectedGroups" class="text-xs text-red-500">
                {{ errors.selectedGroups }}
              </p>
            </div>
          </Transition>
        </div>

        <USeparator v-if="form.actionType !== null && (form.anyFile || form.selectedGroups.length > 0)" label="Parameters" />

        <!-- Transcode parameters -->
        <template v-if="form.actionType === PolicyActionType.Transcode && (form.anyFile || form.selectedGroups.length > 0)">
          <div class="flex flex-col gap-5">
            <Transition name="fade">
              <div v-if="hasVideoTarget" class="flex flex-col gap-2">
                <div class="flex items-center gap-2">
                  <Icon icon="mdi:video-outline" class="w-4 h-4 text-gray-500" />
                  <span class="text-sm font-medium">Video quality ladder</span>
                </div>
                <div class="grid grid-cols-3 gap-2">
                  <button
                    v-for="rung in videoRungOptions"
                    :key="rung.value"
                    type="button"
                    class="flex flex-col items-center gap-0.5 py-2 px-1 rounded-lg border-2 transition-colors"
                    :class="
                      transcodeParams.videoRungs.includes(rung.value)
                        ? 'border-primary bg-primary/10 text-primary'
                        : 'border-neutral-200 dark:border-neutral-700 hover:border-neutral-300'
                    "
                    @click="toggleVideoRung(rung.value)"
                  >
                    <span class="text-sm font-semibold">{{ rung.label }}</span>
                    <span class="text-xs text-gray-400">{{ rung.hint }}</span>
                  </button>
                </div>
                <p v-if="errors.videoRungs" class="text-xs text-red-500">{{ errors.videoRungs }}</p>
              </div>
            </Transition>
            <Transition name="fade">
              <div v-if="hasAudioTarget" class="flex flex-col gap-2">
                <div class="flex items-center gap-2">
                  <Icon icon="mdi:music-note" class="w-4 h-4 text-gray-500" />
                  <span class="text-sm font-medium">Audio quality ladder</span>
                </div>
                <div class="grid grid-cols-5 gap-2">
                  <button
                    v-for="rung in audioRungOptions"
                    :key="rung.value"
                    type="button"
                    class="flex flex-col items-center gap-0.5 py-2 px-1 rounded-lg border-2 transition-colors"
                    :class="
                      transcodeParams.audioRungs.includes(rung.value)
                        ? 'border-primary bg-primary/10 text-primary'
                        : 'border-neutral-200 dark:border-neutral-700 hover:border-neutral-300'
                    "
                    @click="toggleAudioRung(rung.value)"
                  >
                    <span class="text-xs font-semibold">{{ rung.label }}</span>
                  </button>
                </div>
                <p v-if="errors.audioRungs" class="text-xs text-red-500">{{ errors.audioRungs }}</p>
              </div>
            </Transition>
          </div>
        </template>

        <!-- Backup parameters -->
        <template v-else-if="form.actionType === PolicyActionType.Backup">
          <div class="flex flex-col gap-4">
            <UFormField label="Destination path" :error="errors.destinationPath">
              <UInput
                v-model="backupParams.destinationPath"
                placeholder="/alexandria/storage/backups"
                class="w-full"
                :ui="{ base: 'font-mono' }"
              />
            </UFormField>
            <UFormField label="Frequency">
              <div class="grid grid-cols-3 gap-2">
                <button
                  v-for="opt in backupFrequencyOptions"
                  :key="opt.value"
                  type="button"
                  class="flex items-center justify-center py-2 px-1 rounded-lg border-2 transition-colors"
                  :class="
                    backupParams.frequency === opt.value
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-neutral-200 dark:border-neutral-700 hover:border-neutral-300'
                  "
                  @click="backupParams.frequency = opt.value"
                >
                  <span class="text-xs font-medium">{{ opt.label }}</span>
                </button>
              </div>
            </UFormField>
          </div>
        </template>

        <!-- AutoTag parameters -->
        <template v-else-if="form.actionType === PolicyActionType.AutoTag">
          <UFormField label="Read tags from">
            <div class="grid grid-cols-2 gap-2">
              <button
                v-for="opt in tagSourceOptions"
                :key="opt.value"
                type="button"
                class="flex items-center gap-2 px-3 py-2.5 rounded-lg border-2 transition-colors"
                :class="
                  autoTagParams.source === opt.value
                    ? 'border-primary bg-primary/10 text-primary'
                    : 'border-neutral-200 dark:border-neutral-700 hover:border-neutral-300'
                "
                @click="autoTagParams.source = opt.value"
              >
                <Icon :icon="opt.icon" class="w-4 h-4 shrink-0" />
                <span class="text-sm font-medium">{{ opt.label }}</span>
              </button>
            </div>
          </UFormField>
        </template>

        <!-- No action selected -->
        <div
          v-else
          class="flex flex-col items-center gap-2 py-8 rounded-lg border border-dashed border-neutral-300 dark:border-neutral-700"
        >
          <Icon icon="mdi:cog-outline" class="w-8 h-8 text-gray-300 dark:text-gray-600" />
          <span class="text-sm text-gray-500">Select an action to continue</span>
        </div>

        <USeparator v-if="form.actionType !== null" />

        <!-- Priority and version -->
        <div v-if="form.actionType !== null" class="grid grid-cols-2 gap-4">
          <UFormField label="Priority" :error="errors.priority">
            <USelectMenu v-model="selectedPriorityOption" :items="priorityOptions" class="w-full" />
          </UFormField>
          <UFormField label="Re-run on new version">
            <div class="flex items-center h-9">
              <USwitch v-model="form.applyOnNewVersion" />
            </div>
          </UFormField>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2 w-full">
        <UButton variant="ghost" color="neutral" @click="open = false">Cancel</UButton>
        <UButton :loading="isPending" :disabled="!isValid" @click="handleSubmit">
          {{ isEditing ? "Save changes" : "Add rule" }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed, reactive, ref, watch } from "vue";
import { z } from "zod";

import {
  type AddPolicyRuleRequest,
  AudioRung,
  type AutoTagParameters,
  BackupFrequency,
  type BackupParameters,
  PolicyActionType,
  type PolicyRuleDto,
  PolicyTriggerType,
  TagSource,
  type TranscodeParameters,
  type UpdatePolicyRuleRequest,
  VideoRung,
} from "@/api/policy";
import { addRule, updateRule } from "@/mutations/policies";

const props = defineProps<{
  policyId: string;
  directoryId: string;
  rule?: PolicyRuleDto;
}>();

const open = defineModel<boolean>("open", { required: true });

const { mutateAsync: addRuleMutation, isLoading: isAdding } = addRule();
const { mutateAsync: updateRuleMutation, isLoading: isUpdating } = updateRule();

const isPending = computed(() => isAdding.value || isUpdating.value);
const isEditing = computed(() => Boolean(props.rule));

// Priority options - values are sent to the backend as-is
const priorityOptions = [
  { label: "Highest", value: 0 },
  { label: "High", value: 10 },
  { label: "Normal", value: 25 },
  { label: "Low", value: 100 },
] as const;

type PriorityOption = (typeof priorityOptions)[number];

const snapPriority = (p: number): number => {
  const closest = [...priorityOptions].reduce((prev, curr) =>
    Math.abs(curr.value - p) < Math.abs(prev.value - p) ? curr : prev,
  );
  return closest.value;
};

// Groups eligible per action type
type GroupOption = { label: string; value: string; icon: string };

const allGroupsList = (): GroupOption[] => [
  { label: "Images", value: "Images", icon: "mdi:image-outline" },
  { label: "Videos", value: "Videos", icon: "mdi:video-outline" },
  { label: "Audio", value: "Audio", icon: "mdi:music-note-outline" },
  { label: "Documents", value: "Documents", icon: "mdi:file-document-outline" },
  { label: "Spreadsheets", value: "Spreadsheets", icon: "mdi:table-large" },
  { label: "Presentations", value: "Presentations", icon: "mdi:presentation" },
  { label: "Archives", value: "Archives", icon: "mdi:archive-outline" },
  { label: "Code", value: "Code", icon: "mdi:code-braces" },
  { label: "Text", value: "Text", icon: "mdi:text-box-outline" },
];

const groupsByAction: Record<PolicyActionType, GroupOption[]> = {
  [PolicyActionType.Transcode]: [
    { label: "Videos", value: "Videos", icon: "mdi:video-outline" },
    { label: "Audio", value: "Audio", icon: "mdi:music-note-outline" },
  ],
  [PolicyActionType.AutoTag]: allGroupsList(),
  [PolicyActionType.Backup]: allGroupsList(),
};

const eligibleGroups = computed(() =>
  form.actionType !== null ? groupsByAction[form.actionType] : [],
);

// Zod schemas
const transcodeSchema = z
  .object({
    videoRungs: z.array(z.nativeEnum(VideoRung)),
    audioRungs: z.array(z.nativeEnum(AudioRung)),
  })
  .superRefine((data, ctx) => {
    if (data.videoRungs.length === 0 && data.audioRungs.length === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Select at least one quality rung",
        path: ["videoRungs"],
      });
    }
  });

const backupSchema = z.object({
  destinationPath: z.string().min(1, "Destination path is required"),
  frequency: z.nativeEnum(BackupFrequency),
});

const autoTagSchema = z.object({
  source: z.nativeEnum(TagSource),
});

const baseSchema = z
  .object({
    actionType: z.nativeEnum(PolicyActionType).nullable(),
    anyFile: z.boolean(),
    selectedGroups: z.array(z.string()),
    priority: z.number().int().min(0),
    applyOnNewVersion: z.boolean(),
  })
  .superRefine((data, ctx) => {
    if (data.actionType === null) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Select an action",
        path: ["actionType"],
      });
    }
    if (!data.anyFile && data.selectedGroups.length === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Select at least one file category",
        path: ["selectedGroups"],
      });
    }
  });

// Reactive state
const existingGroups = (): string[] => {
  if (!props.rule || props.rule.triggerType === PolicyTriggerType.AnyFile) return [];
  return props.rule.triggerValue.split(";").filter(Boolean);
};

const defaultForm = () => ({
  actionType: props.rule?.actionType ?? (null as PolicyActionType | null),
  anyFile: !props.rule || props.rule.triggerType === PolicyTriggerType.AnyFile,
  selectedGroups: existingGroups(),
  priority: snapPriority(props.rule?.priority ?? 25),
  applyOnNewVersion: props.rule?.applyOnNewVersion ?? false,
});

const defaultTranscodeParams = (): TranscodeParameters => {
  const existing =
    props.rule?.actionType === PolicyActionType.Transcode
      ? (props.rule.parameters as TranscodeParameters)
      : null;
  return {
    videoRungs: existing?.videoRungs ?? [VideoRung.P720, VideoRung.P1080],
    audioRungs: existing?.audioRungs ?? [AudioRung.Kbps192, AudioRung.Kbps320],
    generateThumbnail: true,
  };
};

const defaultBackupParams = (): BackupParameters => ({
  destinationPath: "",
  frequency: BackupFrequency.Weekly,
  ...(props.rule?.actionType === PolicyActionType.Backup
    ? (props.rule.parameters as BackupParameters)
    : {}),
});

const defaultAutoTagParams = (): AutoTagParameters => ({
  source: TagSource.FileMetadata,
  ...(props.rule?.actionType === PolicyActionType.AutoTag
    ? (props.rule.parameters as AutoTagParameters)
    : {}),
});

const form = reactive(defaultForm());
const transcodeParams = reactive(defaultTranscodeParams());
const backupParams = reactive(defaultBackupParams());
const autoTagParams = reactive(defaultAutoTagParams());
const errors = ref<Record<string, string>>({});

watch(open, (val) => {
  if (!val) return;
  Object.assign(form, defaultForm());
  Object.assign(transcodeParams, defaultTranscodeParams());
  Object.assign(backupParams, defaultBackupParams());
  Object.assign(autoTagParams, defaultAutoTagParams());
  errors.value = {};
});

// Priority select bridges to form.priority as a plain integer
const selectedPriorityOption = computed<PriorityOption>({
  get() {
    return priorityOptions.find((o) => o.value === form.priority) ?? priorityOptions[2];
  },
  set(opt) {
    form.priority = opt.value;
  },
});

const hasVideoTarget = computed(() => form.anyFile || form.selectedGroups.includes("Videos"));
const hasAudioTarget = computed(() => form.anyFile || form.selectedGroups.includes("Audio"));

const onActionTypeChange = (type: PolicyActionType) => {
  form.actionType = type;
  const valid = groupsByAction[type].map((g) => g.value);
  form.selectedGroups = form.selectedGroups.filter((g) => valid.includes(g));
};

const onAnyFileToggle = (val: boolean) => {
  if (val) form.selectedGroups = [];
};

const toggleGroup = (group: string) => {
  const idx = form.selectedGroups.indexOf(group);
  if (idx === -1) form.selectedGroups.push(group);
  else form.selectedGroups.splice(idx, 1);
};

const toggleVideoRung = (rung: VideoRung) => {
  const idx = transcodeParams.videoRungs.indexOf(rung);
  if (idx === -1) transcodeParams.videoRungs.push(rung);
  else transcodeParams.videoRungs.splice(idx, 1);
};

const toggleAudioRung = (rung: AudioRung) => {
  const idx = transcodeParams.audioRungs.indexOf(rung);
  if (idx === -1) transcodeParams.audioRungs.push(rung);
  else transcodeParams.audioRungs.splice(idx, 1);
};

const resolvedParameters = computed(() => {
  switch (form.actionType) {
    case PolicyActionType.Transcode:
      return { ...transcodeParams };
    case PolicyActionType.Backup:
      return { ...backupParams };
    case PolicyActionType.AutoTag:
      return { ...autoTagParams };
    default:
      return null;
  }
});

const isValid = computed(() => {
  if (form.actionType === null) return false;
  if (!form.anyFile && form.selectedGroups.length === 0) return false;
  if (form.actionType === PolicyActionType.Transcode) {
    const needsVideo = form.anyFile || form.selectedGroups.includes("Videos");
    const needsAudio = form.anyFile || form.selectedGroups.includes("Audio");
    const hasVideoRungs = !needsVideo || transcodeParams.videoRungs.length > 0;
    const hasAudioRungs = !needsAudio || transcodeParams.audioRungs.length > 0;
    if (!hasVideoRungs && !hasAudioRungs) return false;
  }
  if (form.actionType === PolicyActionType.Backup && !backupParams.destinationPath.trim()) {
    return false;
  }
  return true;
});

const validate = (): boolean => {
  errors.value = {};
  const base = baseSchema.safeParse(form);
  if (!base.success) {
    base.error.issues.forEach((i) => {
      errors.value[i.path[0] as string] = i.message;
    });
  }
  if (form.actionType === PolicyActionType.Transcode) {
    const r = transcodeSchema.safeParse(transcodeParams);
    if (!r.success) {
      r.error.issues.forEach((i) => {
        errors.value[i.path[0] as string] = i.message;
      });
    }
  } else if (form.actionType === PolicyActionType.Backup) {
    const r = backupSchema.safeParse(backupParams);
    if (!r.success) {
      r.error.issues.forEach((i) => {
        errors.value[i.path[0] as string] = i.message;
      });
    }
  } else if (form.actionType === PolicyActionType.AutoTag) {
    const r = autoTagSchema.safeParse(autoTagParams);
    if (!r.success) {
      r.error.issues.forEach((i) => {
        errors.value[i.path[0] as string] = i.message;
      });
    }
  }
  return Object.keys(errors.value).length === 0;
};

const handleSubmit = async () => {
  if (!validate() || !resolvedParameters.value || form.actionType === null) return;
  const triggerType = form.anyFile ? PolicyTriggerType.AnyFile : PolicyTriggerType.FileGroup;
  const triggerValue = form.anyFile ? "*" : form.selectedGroups.join(";");
  if (isEditing.value && props.rule) {
    const payload: UpdatePolicyRuleRequest & { ruleId: string; directoryId: string } = {
      ruleId: props.rule.id,
      directoryId: props.directoryId,
      triggerType,
      triggerValue,
      priority: form.priority,
      applyOnNewVersion: form.applyOnNewVersion,
      parameters: resolvedParameters.value,
    };
    await updateRuleMutation(payload);
  } else {
    const payload: AddPolicyRuleRequest & { policyId: string; directoryId: string } = {
      policyId: props.policyId,
      directoryId: props.directoryId,
      actionType: form.actionType,
      triggerType,
      triggerValue,
      priority: form.priority,
      applyOnNewVersion: form.applyOnNewVersion,
      parameters: resolvedParameters.value,
    };
    await addRuleMutation(payload);
  }
  open.value = false;
};

// Static option arrays
const actionTypeOptions = [
  { label: "Transcode", value: PolicyActionType.Transcode, icon: "mdi:film-open-outline" },
  { label: "Auto-tag", value: PolicyActionType.AutoTag, icon: "mdi:tag-outline" },
  { label: "Backup", value: PolicyActionType.Backup, icon: "mdi:cloud-upload-outline" },
];

const videoRungOptions = [
  { label: "360p", value: VideoRung.P360, hint: "mobile" },
  { label: "480p", value: VideoRung.P480, hint: "SD" },
  { label: "720p", value: VideoRung.P720, hint: "HD" },
  { label: "1080p", value: VideoRung.P1080, hint: "Full HD" },
  { label: "1440p", value: VideoRung.P1440, hint: "2K" },
  { label: "4K", value: VideoRung.P2160, hint: "2160p" },
];

const audioRungOptions = [
  { label: "96k", value: AudioRung.Kbps96 },
  { label: "128k", value: AudioRung.Kbps128 },
  { label: "192k", value: AudioRung.Kbps192 },
  { label: "256k", value: AudioRung.Kbps256 },
  { label: "320k", value: AudioRung.Kbps320 },
];

const backupFrequencyOptions = [
  { label: "Daily", value: BackupFrequency.Daily },
  { label: "3 days", value: BackupFrequency.Every3Days },
  { label: "Weekly", value: BackupFrequency.Weekly },
  { label: "Monthly", value: BackupFrequency.Monthly },
  { label: "3 months", value: BackupFrequency.Every3Months },
];

const tagSourceOptions = [
  { label: "File metadata", value: TagSource.FileMetadata, icon: "mdi:information-outline" },
  { label: "File name", value: TagSource.FileName, icon: "mdi:file-outline" },
];
</script>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition:
    opacity 0.15s ease,
    transform 0.15s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
