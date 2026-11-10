<template>
  <UModal
    v-model:open="open"
    :title="modalTitle"
    description="Runs when files are added to this folder."
    :ui="modalUi"
  >
    <template #title>
      <span
        aria-hidden="true"
        class="absolute top-0.5 left-0 flex h-9 w-9 items-center justify-center rounded-xl bg-gray-500/10"
      >
        <Icon icon="mdi:lightning-bolt-outline" class="h-5 w-5 text-gray-600 dark:text-gray-400" />
      </span>
      {{ modalTitle }}
    </template>

    <template #body>
      <div
        class="flex flex-col gap-8 lg:gap-0 lg:divide-y lg:divide-gray-200/70 lg:dark:divide-gray-700/70"
      >
        <PolicyRuleStep
          :step="1"
          title="What should happen?"
          description="The job this rule performs on matching files."
        >
          <div
            v-if="isEditing && selectedAction"
            class="flex items-center gap-3 rounded-xl border border-gray-200/70 p-3 dark:border-gray-700/70"
          >
            <span
              class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg"
              :class="selectedAction.tileClass"
            >
              <Icon :icon="selectedAction.icon" class="h-5 w-5" />
            </span>

            <div class="min-w-0 flex-1">
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                {{ selectedAction.label }}
              </p>
              <p class="text-xs text-gray-600 dark:text-gray-400">
                The action can't be changed after a rule is created.
              </p>
            </div>

            <Icon icon="mdi:lock-outline" class="h-4 w-4 shrink-0 text-gray-500" />
          </div>

          <UFormField v-else :error="errors.actionType">
            <div role="radiogroup" aria-label="Action" class="grid gap-2 lg:grid-cols-3">
              <button
                v-for="action in actionOptions"
                :key="action.value"
                type="button"
                role="radio"
                :aria-checked="form.actionType === action.value"
                class="relative flex items-center gap-3 rounded-xl border p-3 text-left outline-none transition-colors duration-150 focus-visible:ring-2 focus-visible:ring-primary lg:flex-col lg:items-start lg:p-4"
                :class="actionRowClass(action.value)"
                @click="selectAction(action.value)"
              >
                <span
                  class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg"
                  :class="action.tileClass"
                >
                  <Icon :icon="action.icon" class="h-5 w-5" />
                </span>

                <span class="min-w-0 flex-1 lg:pr-2">
                  <span class="block text-sm font-medium text-gray-900 dark:text-gray-100">
                    {{ action.label }}
                  </span>
                  <span class="block text-xs text-gray-600 dark:text-gray-400">
                    {{ action.description }}
                  </span>
                </span>

                <Icon
                  v-if="form.actionType === action.value"
                  icon="mdi:check-circle"
                  class="h-5 w-5 shrink-0 text-primary lg:absolute lg:top-4 lg:right-4"
                />
                <span
                  v-else
                  aria-hidden="true"
                  class="h-5 w-5 shrink-0 rounded-full border border-gray-300 lg:absolute lg:top-4 lg:right-4 dark:border-gray-600"
                />
              </button>
            </div>
          </UFormField>
        </PolicyRuleStep>

        <template v-if="form.actionType !== null">
          <PolicyRuleStep
            :step="2"
            title="Which files?"
            class="motion-safe:animate-[slide-in-from-top-and-fade_200ms_ease-out]"
            description="Limit the rule to certain kinds of files."
          >
            <div
              role="group"
              aria-label="File scope"
              class="flex w-full gap-1 rounded-xl bg-black/5 p-1 sm:inline-flex sm:w-auto sm:self-start dark:bg-white/5"
            >
              <UButton
                v-for="scope in scopeOptions"
                :key="scope.label"
                :label="scope.label"
                :aria-pressed="form.anyFile === scope.anyFile"
                :color="form.anyFile === scope.anyFile ? 'primary' : 'neutral'"
                :variant="form.anyFile === scope.anyFile ? 'soft' : 'ghost'"
                class="h-8 flex-1 justify-center rounded-lg px-4 sm:flex-none"
                @click="setFileScope(scope.anyFile)"
              />
            </div>

            <p class="text-xs text-gray-600 dark:text-gray-400">
              {{ scopeHint }}
            </p>

            <PolicyRuleReveal :open="!form.anyFile">
              <UFormField :error="errors.selectedGroups">
                <div role="group" aria-label="File types" class="flex flex-wrap gap-2">
                  <UButton
                    v-for="group in eligibleGroups"
                    :key="group.value"
                    :label="group.label"
                    :icon="group.icon"
                    :aria-pressed="form.selectedGroups.includes(group.value)"
                    :color="form.selectedGroups.includes(group.value) ? 'primary' : 'neutral'"
                    :variant="form.selectedGroups.includes(group.value) ? 'soft' : 'outline'"
                    class="h-8 rounded-xl px-3"
                    @click="toggleGroup(group.value)"
                  />
                </div>
              </UFormField>
            </PolicyRuleReveal>

            <p
              v-if="form.actionType === PolicyActionType.AutoTag"
              class="flex items-start gap-1.5 text-xs text-gray-600 dark:text-gray-400"
            >
              <Icon icon="mdi:information-outline" class="mt-px h-3.5 w-3.5 shrink-0" />
              Auto-tagging runs on audio for now. Other types are kept for future support.
            </p>
          </PolicyRuleStep>

          <PolicyRuleStep
            v-if="form.actionType === PolicyActionType.Transcode"
            :step="3"
            title="Quality ladder"
            description="Each selected quality becomes its own stream."
            class="motion-safe:animate-[slide-in-from-top-and-fade_200ms_ease-out]"
          >
            <PolicyRuleReveal :open="hasVideoTarget">
              <div class="flex flex-col gap-2 sm:flex-row sm:gap-3">
                <span
                  class="shrink-0 text-xs font-medium text-gray-600 sm:w-10 sm:pt-2 dark:text-gray-400"
                >
                  Video
                </span>

                <UFormField :error="errors.videoRungs">
                  <div role="group" aria-label="Video qualities" class="flex flex-wrap gap-2">
                    <UButton
                      v-for="rung in videoRungOptions"
                      :key="rung.value"
                      :aria-pressed="transcodeParams.videoRungs.includes(rung.value)"
                      :color="
                        transcodeParams.videoRungs.includes(rung.value) ? 'primary' : 'neutral'
                      "
                      :variant="
                        transcodeParams.videoRungs.includes(rung.value) ? 'soft' : 'outline'
                      "
                      class="h-8 gap-1.5 rounded-xl px-3"
                      @click="toggleVideoRung(rung.value)"
                    >
                      <span>{{ rung.label }}</span>
                      <span class="font-normal">{{ rung.hint }}</span>
                    </UButton>
                  </div>
                </UFormField>
              </div>
            </PolicyRuleReveal>

            <PolicyRuleReveal :open="hasAudioTarget">
              <div class="flex flex-col gap-2 sm:flex-row sm:gap-3">
                <span
                  class="shrink-0 text-xs font-medium text-gray-600 sm:w-10 sm:pt-2 dark:text-gray-400"
                >
                  Audio
                </span>

                <UFormField :error="errors.audioRungs">
                  <div role="group" aria-label="Audio qualities" class="flex flex-wrap gap-2">
                    <UButton
                      v-for="rung in audioRungOptions"
                      :key="rung.value"
                      :label="rung.label"
                      :aria-pressed="transcodeParams.audioRungs.includes(rung.value)"
                      :color="
                        transcodeParams.audioRungs.includes(rung.value) ? 'primary' : 'neutral'
                      "
                      :variant="
                        transcodeParams.audioRungs.includes(rung.value) ? 'soft' : 'outline'
                      "
                      class="h-8 rounded-xl px-3"
                      @click="toggleAudioRung(rung.value)"
                    />
                  </div>
                </UFormField>
              </div>
            </PolicyRuleReveal>

            <p v-if="ladderSummary" class="text-xs text-gray-500 dark:text-gray-500">
              {{ ladderSummary }}
            </p>
          </PolicyRuleStep>

          <PolicyRuleStep
            v-else-if="form.actionType === PolicyActionType.Backup"
            :step="3"
            title="Backup schedule"
            description="Where copies go and how often they are made."
            class="motion-safe:animate-[slide-in-from-top-and-fade_200ms_ease-out]"
          >
            <UFormField label="Destination folder" :error="errors.destinationPath">
              <UInput
                v-model="backupParams.destinationPath"
                icon="i-mdi-folder-outline"
                placeholder="/alexandria/storage/backups"
                class="w-full"
                :ui="{ base: 'font-mono' }"
              />
            </UFormField>

            <UFormField label="How often">
              <div role="group" aria-label="Backup frequency" class="flex flex-wrap gap-2">
                <UButton
                  v-for="option in backupFrequencyOptions"
                  :key="option.value"
                  :label="option.label"
                  :aria-pressed="backupParams.frequency === option.value"
                  :color="backupParams.frequency === option.value ? 'primary' : 'neutral'"
                  :variant="backupParams.frequency === option.value ? 'soft' : 'outline'"
                  class="h-8 rounded-xl px-3"
                  @click="backupParams.frequency = option.value"
                />
              </div>
            </UFormField>
          </PolicyRuleStep>

          <PolicyRuleStep
            :step="runStepNumber"
            title="When it runs"
            description="Re-runs and ordering between rules."
            class="motion-safe:animate-[slide-in-from-top-and-fade_200ms_ease-out]"
          >
            <div
              class="divide-y divide-gray-200/70 rounded-xl border border-gray-200/70 dark:divide-gray-700/70 dark:border-gray-700/70"
            >
              <div class="flex items-center justify-between gap-4 p-3">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                    Also run on new versions
                  </p>
                  <p class="text-xs text-gray-600 dark:text-gray-400">
                    Process a file again when a new version is uploaded.
                  </p>
                </div>

                <USwitch v-model="form.applyOnNewVersion" aria-label="Also run on new versions" />
              </div>

              <div
                class="flex flex-col gap-3 p-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4"
              >
                <div class="min-w-0">
                  <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Run order</p>
                  <p class="text-xs text-gray-600 dark:text-gray-400">
                    Earlier rules run first when several match a file.
                  </p>
                </div>

                <div
                  role="group"
                  aria-label="Run order"
                  class="flex shrink-0 gap-1 rounded-xl bg-black/5 p-1 dark:bg-white/5"
                >
                  <UButton
                    v-for="option in priorityOptions"
                    :key="option.value"
                    :label="option.label"
                    :aria-pressed="form.priority === option.value"
                    :color="form.priority === option.value ? 'primary' : 'neutral'"
                    :variant="form.priority === option.value ? 'soft' : 'ghost'"
                    class="h-8 flex-1 justify-center rounded-lg px-3 sm:flex-none"
                    @click="form.priority = option.value"
                  />
                </div>
              </div>
            </div>
          </PolicyRuleStep>
        </template>
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton variant="outline" color="neutral" :disabled="isPending" @click="open = false">
          Cancel
        </UButton>

        <UButton color="primary" :loading="isPending" @click="handleSubmit">
          {{ submitLabel }}
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
  type TranscodeParameters,
  type UpdatePolicyRuleRequest,
  VideoRung,
} from "@/api/policy";
import { addRule, updateRule } from "@/mutations/policies";
import { formatCount } from "@/utils/count";
import { POLICY_FILE_GROUPS, type PolicyFileGroup } from "@/utils/files/mime-catalog";
import { glassModalContent } from "@/utils/modal-ui";

import PolicyRuleReveal from "./PolicyRuleReveal.vue";
import PolicyRuleStep from "./PolicyRuleStep.vue";

const {
  policyId,
  directoryId,
  rule = undefined,
} = defineProps<{
  policyId: string;
  directoryId: string;
  rule?: PolicyRuleDto;
}>();

const open = defineModel<boolean>("open", { required: true });

const { mutateAsync: addRuleMutation, isLoading: isAdding } = addRule();
const { mutateAsync: updateRuleMutation, isLoading: isUpdating } = updateRule();

const isPending = computed(() => isAdding.value || isUpdating.value);
const isEditing = computed(() => Boolean(rule));

const modalTitle = computed(() => {
  if (isEditing.value) return "Edit automation rule";
  return "New automation rule";
});

const submitLabel = computed(() => {
  if (isEditing.value) return "Save changes";
  return "Add rule";
});

// The title slot positions the icon tile against the wrapper so the dialog keeps
// its accessible title and description elements.
const modalUi = {
  content: `${glassModalContent} sm:max-w-xl lg:max-w-4xl`,
  header: "shrink-0 items-start",
  footer: "shrink-0",
  wrapper: "relative min-h-10 pl-12 pr-8",
};

interface ActionOption {
  value: PolicyActionType;
  label: string;
  description: string;
  icon: string;
  tileClass: string;
}

const actionOptions: ActionOption[] = [
  {
    value: PolicyActionType.Transcode,
    label: "Transcode",
    description: "Create streaming qualities for video and audio",
    icon: "mdi:film-open-outline",
    tileClass: "bg-blue-100 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400",
  },
  {
    value: PolicyActionType.AutoTag,
    label: "Auto-tag",
    description: "Detect genre, mood and more from audio",
    icon: "mdi:tag-outline",
    tileClass: "bg-amber-100 text-amber-600 dark:bg-amber-900/30 dark:text-amber-400",
  },
  {
    value: PolicyActionType.Backup,
    label: "Backup",
    description: "Copy files to another location on a schedule",
    icon: "mdi:cloud-upload-outline",
    tileClass: "bg-green-100 text-green-600 dark:bg-green-900/30 dark:text-green-400",
  },
];

const ANY_FILE_HINTS: Record<PolicyActionType, string> = {
  [PolicyActionType.Transcode]: "Every video and audio file added here.",
  [PolicyActionType.AutoTag]: "Every audio file added here.",
  [PolicyActionType.Backup]: "Every file added here.",
};

const scopeOptions = [
  { label: "All eligible files", anyFile: true },
  { label: "Specific types", anyFile: false },
];

// Values are sent to the backend as-is; the dispatcher runs rules in ascending order.
const priorityOptions = [
  { label: "First", value: 0 },
  { label: "Early", value: 10 },
  { label: "Normal", value: 25 },
  { label: "Late", value: 100 },
];

const snapPriority = (p: number): number => {
  const closest = priorityOptions.reduce((prev, curr) => {
    if (Math.abs(curr.value - p) < Math.abs(prev.value - p)) return curr;
    return prev;
  });

  return closest.value;
};

interface GroupOption {
  label: string;
  value: string;
  icon: string;
}

// Group names come from the backend catalog; a new backend group fails type-checking here until
// it gets an icon.
const GROUP_ICONS: Record<PolicyFileGroup, string> = {
  Images: "i-mdi-image-outline",
  Videos: "i-mdi-video-outline",
  Audio: "i-mdi-music-note-outline",
  Documents: "i-mdi-file-document-outline",
  Spreadsheets: "i-mdi-table-large",
  Presentations: "i-mdi-presentation",
  Archives: "i-mdi-archive-outline",
  Code: "i-mdi-code-braces",
  Text: "i-mdi-text-box-outline",
};

const groupOption = (group: PolicyFileGroup): GroupOption => ({
  label: group,
  value: group,
  icon: GROUP_ICONS[group],
});

const allGroupsList = (): GroupOption[] => POLICY_FILE_GROUPS.map(groupOption);

const groupsByAction: Record<PolicyActionType, GroupOption[]> = {
  [PolicyActionType.Transcode]: [groupOption("Videos"), groupOption("Audio")],
  [PolicyActionType.AutoTag]: allGroupsList(),
  [PolicyActionType.Backup]: allGroupsList(),
};

const backupSchema = z.object({
  destinationPath: z.string().trim().min(1, "Destination folder is required"),
  frequency: z.nativeEnum(BackupFrequency),
});

// `source` is fixed: the backend ignores it (reserved for a future enrichment mode), so there
// is no user input to validate for AutoTag.
const AUTO_TAG_PARAMETERS: AutoTagParameters = { source: 1 };

const existingGroups = (): string[] => {
  if (!rule || rule.triggerType === PolicyTriggerType.AnyFile) return [];
  return rule.triggerValue.split(";").filter(Boolean);
};

const defaultForm = () => ({
  actionType: rule?.actionType ?? (null as PolicyActionType | null),
  anyFile: !rule || rule.triggerType === PolicyTriggerType.AnyFile,
  selectedGroups: existingGroups(),
  priority: snapPriority(rule?.priority ?? 25),
  applyOnNewVersion: rule?.applyOnNewVersion ?? false,
});

const defaultTranscodeParams = (): TranscodeParameters => {
  let existing: TranscodeParameters | null = null;
  if (rule?.actionType === PolicyActionType.Transcode) {
    existing = rule.parameters as TranscodeParameters;
  }

  return {
    videoRungs: [...(existing?.videoRungs ?? [VideoRung.P720, VideoRung.P1080])],
    audioRungs: [...(existing?.audioRungs ?? [AudioRung.Kbps192, AudioRung.Kbps320])],
    generateThumbnail: true,
  };
};

const defaultBackupParams = (): BackupParameters => ({
  destinationPath: "",
  frequency: BackupFrequency.Weekly,
  ...(rule?.actionType === PolicyActionType.Backup ? (rule.parameters as BackupParameters) : {}),
});

const form = reactive(defaultForm());
const transcodeParams = reactive(defaultTranscodeParams());
const backupParams = reactive(defaultBackupParams());
const errors = ref<Record<string, string>>({});
const submitAttempted = ref(false);

watch(open, (isOpen) => {
  if (!isOpen) return;

  Object.assign(form, defaultForm());
  Object.assign(transcodeParams, defaultTranscodeParams());
  Object.assign(backupParams, defaultBackupParams());
  errors.value = {};
  submitAttempted.value = false;
});

const selectedAction = computed(() =>
  actionOptions.find((action) => action.value === form.actionType),
);

const eligibleGroups = computed(() => {
  if (form.actionType === null) return [];
  return groupsByAction[form.actionType];
});

const scopeHint = computed(() => {
  if (!form.anyFile) return "Only the file types you pick below.";
  if (form.actionType === null) return "";
  return ANY_FILE_HINTS[form.actionType];
});

// Until a type is picked both ladders stay visible, so switching the file scope only moves the
// type pills instead of collapsing the ladders at the same time.
const showsAllTargets = computed(() => form.anyFile || form.selectedGroups.length === 0);

const hasVideoTarget = computed(
  () => showsAllTargets.value || form.selectedGroups.includes("Videos"),
);
const hasAudioTarget = computed(
  () => showsAllTargets.value || form.selectedGroups.includes("Audio"),
);

const runStepNumber = computed(() => {
  if (form.actionType === PolicyActionType.AutoTag) return 3;
  return 4;
});

const ladderSummary = computed(() => {
  const parts: string[] = [];

  if (hasVideoTarget.value && transcodeParams.videoRungs.length > 0) {
    parts.push(formatCount(transcodeParams.videoRungs.length, "video quality", "video qualities"));
  }

  if (hasAudioTarget.value && transcodeParams.audioRungs.length > 0) {
    parts.push(formatCount(transcodeParams.audioRungs.length, "audio quality", "audio qualities"));
  }

  if (parts.length === 0) return "";
  return `${parts.join(" and ")} per file`;
});

const actionRowClass = (value: PolicyActionType) => {
  if (form.actionType === value) return "border-primary bg-primary/5 ring-1 ring-primary";
  return "border-gray-200/70 hover:border-gray-300 hover:bg-gray-500/5 dark:border-gray-700/70 dark:hover:border-gray-600";
};

const selectAction = (type: PolicyActionType) => {
  form.actionType = type;

  const valid = groupsByAction[type].map((g) => g.value);
  form.selectedGroups = form.selectedGroups.filter((g) => valid.includes(g));
};

const setFileScope = (anyFile: boolean) => {
  form.anyFile = anyFile;
  if (anyFile) form.selectedGroups = [];
};

const toggleInList = <T>(list: T[], value: T) => {
  const idx = list.indexOf(value);
  if (idx === -1) list.push(value);
  else list.splice(idx, 1);
};

const toggleGroup = (group: string) => toggleInList(form.selectedGroups, group);
const toggleVideoRung = (rung: VideoRung) => toggleInList(transcodeParams.videoRungs, rung);
const toggleAudioRung = (rung: AudioRung) => toggleInList(transcodeParams.audioRungs, rung);

const resolvedParameters = computed(() => {
  switch (form.actionType) {
    case PolicyActionType.Transcode:
      return { ...transcodeParams };
    case PolicyActionType.Backup:
      return { ...backupParams };
    case PolicyActionType.AutoTag:
      return { ...AUTO_TAG_PARAMETERS };
    default:
      return null;
  }
});

const collectTranscodeErrors = (found: Record<string, string>) => {
  const hasVideoRungs = hasVideoTarget.value && transcodeParams.videoRungs.length > 0;
  const hasAudioRungs = hasAudioTarget.value && transcodeParams.audioRungs.length > 0;
  if (hasVideoRungs || hasAudioRungs) return;

  const message = "Select at least one quality";
  if (hasVideoTarget.value) found.videoRungs = message;
  else if (hasAudioTarget.value) found.audioRungs = message;
};

const collectBackupErrors = (found: Record<string, string>) => {
  const result = backupSchema.safeParse(backupParams);
  if (result.success) return;

  result.error.issues.forEach((issue) => {
    found[issue.path[0] as string] = issue.message;
  });
};

const validate = (): boolean => {
  const found: Record<string, string> = {};

  if (form.actionType === null) {
    found.actionType = "Choose what this rule should do";
  }

  if (!form.anyFile && form.selectedGroups.length === 0) {
    found.selectedGroups = "Select at least one file type";
  }

  if (form.actionType === PolicyActionType.Transcode) collectTranscodeErrors(found);
  if (form.actionType === PolicyActionType.Backup) collectBackupErrors(found);

  errors.value = found;
  return Object.keys(found).length === 0;
};

// Errors only appear after the first submit attempt, then follow every edit so they clear
// as soon as the field is fixed.
watch([form, transcodeParams, backupParams], () => {
  if (submitAttempted.value) validate();
});

const handleSubmit = async () => {
  submitAttempted.value = true;
  if (!validate() || !resolvedParameters.value || form.actionType === null) return;

  const triggerType = form.anyFile ? PolicyTriggerType.AnyFile : PolicyTriggerType.FileGroup;
  const triggerValue = form.anyFile ? "*" : form.selectedGroups.join(";");

  if (isEditing.value && rule) {
    const payload: UpdatePolicyRuleRequest & { ruleId: string; directoryId: string } = {
      ruleId: rule.id,
      directoryId,
      triggerType,
      triggerValue,
      priority: form.priority,
      applyOnNewVersion: form.applyOnNewVersion,
      parameters: resolvedParameters.value,
    };
    await updateRuleMutation(payload);
  } else {
    const payload: AddPolicyRuleRequest & { policyId: string; directoryId: string } = {
      policyId,
      directoryId,
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

const videoRungOptions = [
  { label: "360p", value: VideoRung.P360, hint: "Mobile" },
  { label: "480p", value: VideoRung.P480, hint: "SD" },
  { label: "720p", value: VideoRung.P720, hint: "HD" },
  { label: "1080p", value: VideoRung.P1080, hint: "Full HD" },
  { label: "1440p", value: VideoRung.P1440, hint: "2K" },
  { label: "2160p", value: VideoRung.P2160, hint: "4K" },
];

const audioRungOptions = [
  { label: "96 kbps", value: AudioRung.Kbps96 },
  { label: "128 kbps", value: AudioRung.Kbps128 },
  { label: "192 kbps", value: AudioRung.Kbps192 },
  { label: "256 kbps", value: AudioRung.Kbps256 },
  { label: "320 kbps", value: AudioRung.Kbps320 },
];

const backupFrequencyOptions = [
  { label: "Daily", value: BackupFrequency.Daily },
  { label: "Every 3 days", value: BackupFrequency.Every3Days },
  { label: "Weekly", value: BackupFrequency.Weekly },
  { label: "Monthly", value: BackupFrequency.Monthly },
  { label: "Every 3 months", value: BackupFrequency.Every3Months },
];
</script>
