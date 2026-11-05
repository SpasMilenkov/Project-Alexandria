<template>
  <DetailsSection title="Automation">
    <template v-if="policy" #actions>
      <UButton
        icon="i-mdi-plus"
        label="Add rule"
        size="xs"
        color="neutral"
        variant="outline"
        @click="openAddModal"
      />

      <UTooltip text="Remove automation">
        <UButton
          icon="i-mdi-delete-outline"
          size="xs"
          color="error"
          variant="ghost"
          square
          class="justify-center"
          :loading="isDeleting"
          aria-label="Remove policy"
          @click="handleDeletePolicy"
        />
      </UTooltip>
    </template>

    <div v-if="isPending" class="flex h-24 items-center justify-center text-gray-500">
      <BlockSpinner :size="20" />
    </div>

    <div
      v-else-if="!policy"
      class="flex flex-col items-center gap-3 rounded-xl border border-dashed border-gray-300/80 px-4 py-6 text-center dark:border-gray-700/80"
    >
      <Icon icon="mdi:lightning-bolt-outline" class="h-8 w-8 text-gray-400 dark:text-gray-600" />

      <div>
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No automation configured</p>
        <p class="mt-0.5 text-xs text-gray-600 dark:text-gray-400">
          Rules run automatically when files are uploaded here.
        </p>
      </div>

      <UButton
        size="sm"
        color="neutral"
        variant="outline"
        :loading="isCreating"
        @click="handleCreatePolicy"
      >
        Set up automation
      </UButton>
    </div>

    <template v-else>
      <div
        v-if="policy.rules.length === 0"
        class="flex flex-col items-center gap-2 rounded-xl border border-dashed border-gray-300/80 px-4 py-5 text-center dark:border-gray-700/80"
      >
        <p class="text-sm text-gray-600 dark:text-gray-400">No rules yet</p>
        <UButton size="xs" color="neutral" variant="outline" @click="openAddModal">
          Add your first rule
        </UButton>
      </div>

      <div v-else class="flex flex-col gap-2">
        <PolicyRuleRow
          v-for="rule in policy.rules"
          :key="rule.id"
          :rule="rule"
          :directory-id="directoryId"
          @edit="openEditModal(rule)"
        />
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

        <USwitch
          :model-value="policy.inheritedByChildren"
          :disabled="isUpdating"
          aria-label="Include subfolders"
          @update:model-value="toggleInheritance"
        />
      </div>
    </template>
  </DetailsSection>

  <PolicyRuleModal
    v-if="policy"
    v-model:open="ruleModalOpen"
    :policy-id="policy.id"
    :directory-id="directoryId"
    :rule="editingRule ?? undefined"
  />
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { ref } from "vue";

import type { PolicyRuleDto } from "@/api/policy";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import DetailsSection from "@/components/common/details/DetailsSection.vue";
import { useAppToast } from "@/composables/useAppToast";
import { createPolicy, deletePolicy, updatePolicy } from "@/mutations/policies";
import { getPolicyByDirectory } from "@/queries/policies";

import PolicyRuleModal from "./PolicyRuleModal.vue";
import PolicyRuleRow from "./PolicyRuleRow.vue";

const props = defineProps<{
  directoryId: string;
}>();

const { data: policy, isPending } = useQuery(getPolicyByDirectory(props.directoryId));

const appToast = useAppToast("policy");

const { mutateAsync: createPolicyMutation, isLoading: isCreating } = createPolicy();
const { mutateAsync: updatePolicyMutation, isLoading: isUpdating } = updatePolicy();
const { mutateAsync: deletePolicyMutation, isLoading: isDeleting } = deletePolicy();

const ruleModalOpen = ref(false);
const editingRule = ref<PolicyRuleDto | null>(null);

const openAddModal = () => {
  editingRule.value = null;
  ruleModalOpen.value = true;
};

const openEditModal = (rule: PolicyRuleDto) => {
  editingRule.value = rule;
  ruleModalOpen.value = true;
};

const handleCreatePolicy = async () => {
  try {
    await createPolicyMutation({
      directoryId: props.directoryId,
      inheritedByChildren: false,
    });
    appToast.success("Automation enabled");
  } catch (err) {
    appToast.error("Couldn't enable automation", err);
  }
};

const toggleInheritance = async () => {
  if (!policy.value) return;
  try {
    await updatePolicyMutation({
      policyId: policy.value.id,
      directoryId: props.directoryId,
      inheritedByChildren: !policy.value.inheritedByChildren,
    });
  } catch (err) {
    appToast.error("Couldn't update automation", err);
  }
};

const handleDeletePolicy = async () => {
  if (!policy.value) return;
  try {
    await deletePolicyMutation({
      policyId: policy.value.id,
      directoryId: props.directoryId,
    });
    appToast.success("Automation removed");
  } catch (err) {
    appToast.error("Couldn't remove automation", err);
  }
};
</script>
