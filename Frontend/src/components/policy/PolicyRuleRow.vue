<template>
  <div
    class="group flex items-center gap-3 rounded-xl border border-gray-200/70 px-3 py-2.5 dark:border-gray-700/70"
  >
    <div
      class="shrink-0 w-7 h-7 rounded-md flex items-center justify-center"
      :class="actionColorClass"
    >
      <Icon :icon="actionIcon" class="w-4 h-4" />
    </div>

    <div class="flex-1 min-w-0">
      <div class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
        {{ actionLabel }}
      </div>
      <div class="truncate text-xs text-gray-600 dark:text-gray-400">{{ triggerLabel }}</div>
      <p
        v-if="isInactiveAutoTag"
        class="mt-1 flex items-start gap-1.5 text-xs text-amber-700 dark:text-amber-400"
      >
        <Icon icon="mdi:alert-circle-outline" class="mt-px h-3.5 w-3.5 shrink-0" />
        Auto-tagging is turned off for this installation, so this rule does nothing. Run setup again
        to turn it back on; the rule is kept.
      </p>
    </div>

    <div
      class="flex items-center gap-1 transition-opacity shrink-0"
      :class="
        isMobile ? 'opacity-100' : 'opacity-0 group-hover:opacity-100 focus-within:opacity-100'
      "
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
import { breakpointsTailwind, useBreakpoints } from "@vueuse/core";
import { computed, ref } from "vue";

import { PolicyActionType, type PolicyRuleDto } from "@/api/policy";
import { useAppToast } from "@/composables/useAppToast";
import {
  policyActionPresentation,
  policyTriggerLabel,
} from "@/features/policies/rule-presentation";
import { deleteRule } from "@/mutations/policies";

const { directoryId, rule } = defineProps<{
  rule: PolicyRuleDto;
  directoryId: string;
}>();

const emit = defineEmits<{ edit: [] }>();

const autoTaggingEnabled = import.meta.env.VITE_AUTOTAGGING_ENABLED === "true";
const isInactiveAutoTag = computed(
  () => rule.actionType === PolicyActionType.AutoTag && !autoTaggingEnabled,
);

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

const appToast = useAppToast("policy");

const { mutateAsync: deleteRuleMutation } = deleteRule();
const isDeleting = ref(false);

const handleDelete = async () => {
  isDeleting.value = true;
  try {
    await deleteRuleMutation({ ruleId: rule.id, directoryId });
    appToast.success("Rule removed");
  } catch (err) {
    appToast.error("Couldn't remove the rule", err);
  } finally {
    isDeleting.value = false;
  }
};

const presentation = computed(() => policyActionPresentation(rule.actionType));
const actionIcon = computed(() => presentation.value.icon);
const actionColorClass = computed(() => presentation.value.colorClass);
const actionLabel = computed(() => presentation.value.label);
const triggerLabel = computed(() => policyTriggerLabel(rule.triggerType, rule.triggerValue));
</script>
