<template>
  <div
    class="flex items-center gap-3 px-3 py-2.5 rounded-lg bg-neutral-100 dark:bg-neutral-800/50 group"
  >
    <div
      class="shrink-0 w-7 h-7 rounded-md flex items-center justify-center"
      :class="actionColorClass"
    >
      <Icon :icon="actionIcon" class="w-4 h-4" />
    </div>

    <div class="flex-1 min-w-0">
      <div class="text-sm font-medium truncate">{{ actionLabel }}</div>
      <div class="text-xs text-gray-500 dark:text-gray-400 truncate">{{ triggerLabel }}</div>
    </div>

    <div
      class="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0"
    >
      <UButton
        icon="i-mdi-pencil-outline"
        size="xs"
        variant="ghost"
        color="neutral"
        aria-label="Edit rule"
        @click="emit('edit')"
      />
      <UButton
        icon="i-mdi-delete-outline"
        size="xs"
        variant="ghost"
        color="error"
        :loading="isDeleting"
        aria-label="Delete rule"
        @click="handleDelete"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed, ref } from "vue";

import { PolicyActionType, PolicyTriggerType, type PolicyRuleDto } from "@/api/policy";
import { deleteRule } from "@/mutations/policies";

const props = defineProps<{
  rule: PolicyRuleDto;
  directoryId: string;
}>();

const emit = defineEmits<{ edit: [] }>();

const { mutateAsync: deleteRuleMutation } = deleteRule();
const isDeleting = ref(false);

const handleDelete = async () => {
  isDeleting.value = true;
  try {
    await deleteRuleMutation({ ruleId: props.rule.id, directoryId: props.directoryId });
  } finally {
    isDeleting.value = false;
  }
};

const actionIcon = computed(() => {
  switch (props.rule.actionType) {
    case PolicyActionType.Transcode:
      return "mdi:film-open-outline";
    case PolicyActionType.Backup:
      return "mdi:cloud-upload-outline";
    case PolicyActionType.AutoTag:
      return "mdi:tag-outline";
    default:
      return "mdi:cog-outline";
  }
});

const actionColorClass = computed(() => {
  switch (props.rule.actionType) {
    case PolicyActionType.Transcode:
      return "bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400";
    case PolicyActionType.Backup:
      return "bg-green-100 dark:bg-green-900/30 text-green-600 dark:text-green-400";
    case PolicyActionType.AutoTag:
      return "bg-amber-100 dark:bg-amber-900/30 text-amber-600 dark:text-amber-400";
    default:
      return "bg-neutral-200 dark:bg-neutral-700 text-neutral-600 dark:text-neutral-400";
  }
});

const actionLabel = computed(() => {
  switch (props.rule.actionType) {
    case PolicyActionType.Transcode:
      return "Transcode media";
    case PolicyActionType.Backup:
      return "Backup";
    case PolicyActionType.AutoTag:
      return "Auto-tag";
    default:
      return props.rule.actionType;
  }
});

const triggerLabel = computed(() => {
  switch (props.rule.triggerType) {
    case PolicyTriggerType.AnyFile:
      return "Any file";
    case PolicyTriggerType.FileGroup:
      return `.${props.rule.triggerValue} files`;
    default:
      return props.rule.triggerValue;
  }
});
</script>
