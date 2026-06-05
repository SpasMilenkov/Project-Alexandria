<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Icon icon="mdi:cog-play-outline" class="w-5 h-5 text-gray-500 dark:text-gray-400" />
          <span class="font-semibold text-sm">Automation</span>
        </div>

        <div v-if="policy" class="flex items-center gap-1">
          <UTooltip
            :text="policy.inheritedByChildren ? 'Inherited by subfolders' : 'Not inherited'"
          >
            <UButton
              :icon="
                policy.inheritedByChildren ? 'i-mdi-source-branch' : 'i-mdi-source-branch-remove'
              "
              size="xs"
              variant="ghost"
              color="neutral"
              :loading="isUpdating"
              aria-label="Toggle inheritance"
              @click="toggleInheritance"
            />
          </UTooltip>
          <UButton
            icon="i-mdi-plus"
            size="xs"
            variant="ghost"
            color="neutral"
            aria-label="Add rule"
            @click="openAddModal"
          />
          <UButton
            icon="i-mdi-delete-outline"
            size="xs"
            variant="ghost"
            color="error"
            :loading="isDeleting"
            aria-label="Remove policy"
            @click="handleDeletePolicy"
          />
        </div>
      </div>
    </template>

    <!-- Loading -->
    <div v-if="isLoading" class="flex items-center justify-center py-6">
      <UIcon name="i-mdi-loading" class="w-5 h-5 animate-spin text-gray-400" />
    </div>

    <!-- No policy -->
    <div v-else-if="!policy" class="flex flex-col items-center gap-3 py-6 text-center">
      <div
        class="w-10 h-10 rounded-full bg-neutral-100 dark:bg-neutral-800 flex items-center justify-center"
      >
        <Icon icon="mdi:cog-outline" class="w-5 h-5 text-gray-400" />
      </div>
      <div>
        <div class="text-sm font-medium">No automation configured</div>
        <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
          Rules run automatically when files are uploaded here
        </div>
      </div>
      <UButton size="sm" variant="outline" :loading="isCreating" @click="handleCreatePolicy">
        Set up automation
      </UButton>
    </div>

    <!-- Rules list -->
    <template v-else>
      <div
        v-if="policy.rules.length === 0"
        class="flex flex-col items-center gap-2 py-5 text-center"
      >
        <div class="text-sm text-gray-500 dark:text-gray-400">No rules yet</div>
        <UButton size="xs" variant="ghost" @click="openAddModal">Add your first rule</UButton>
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
    </template>
  </UCard>

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

import { createPolicy, deletePolicy, updatePolicy } from "@/mutations/policies";
import { getPolicyByDirectory } from "@/queries/policies";

import PolicyRuleModal from "./PolicyRuleModal.vue";
import PolicyRuleRow from "./PolicyRuleRow.vue";

const props = defineProps<{
  directoryId: string;
}>();

const { data: policy, isLoading } = useQuery(getPolicyByDirectory(props.directoryId));

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
  await createPolicyMutation({
    directoryId: props.directoryId,
    inheritedByChildren: false,
  });
};

const toggleInheritance = async () => {
  if (!policy.value) return;
  await updatePolicyMutation({
    policyId: policy.value.id,
    directoryId: props.directoryId,
    inheritedByChildren: !policy.value.inheritedByChildren,
  });
};

const handleDeletePolicy = async () => {
  if (!policy.value) return;
  await deletePolicyMutation({
    policyId: policy.value.id,
    directoryId: props.directoryId,
  });
};
</script>
