<template>
  <ConfirmModal
    :open="open"
    title="Edit User"
    :description="user ? `Editing @${user.userName}` : ''"
    confirm-label="Save changes"
    confirm-icon="i-lucide-save"
    :loading="loading"
    @confirm="form?.submit()"
    @close="emit('close', $event)"
  >
    <UForm
      ref="form"
      :schema="updateUserSchema"
      :state="state"
      class="space-y-4"
      @submit="emit('submit', $event.data)"
    >
      <UFormField
        label="Username"
        name="userName"
        description="Letters, numbers, underscores, dots, hyphens. Max 50 chars."
      >
        <UInput
          v-model="state.userName"
          placeholder="username"
          icon="i-lucide-user"
          class="w-full"
        />
      </UFormField>

      <UFormField label="Email address" name="email">
        <UInput
          v-model="state.email"
          placeholder="user@example.com"
          icon="i-lucide-mail"
          type="email"
          class="w-full"
        />
      </UFormField>

      <UFormField label="Role" name="role">
        <USelect
          v-model="state.role"
          :items="roleOptions"
          placeholder="Keep current role"
          class="w-full"
        />
      </UFormField>

      <UFormField
        label="Storage quota (GB)"
        name="storageQuotaGb"
        description="Empty keeps the current quota. 0 means unlimited."
      >
        <UInput
          :model-value="state.storageQuotaGb"
          type="number"
          min="0"
          :placeholder="quotaPlaceholder"
          icon="i-lucide-hard-drive"
          class="w-full"
          @update:model-value="onQuotaInput"
        />
      </UFormField>
    </UForm>
  </ConfirmModal>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";

import type { UserDetailsDto } from "@/types/user";

import { UserRole } from "@/enums/UserRole";
import { type UpdateUserSchema, updateUserSchema } from "@/schemas/user";
import { bytesToGb, parseQuotaGbInput } from "@/utils/size.utils";

const props = defineProps<{
  open: boolean;
  user: UserDetailsDto | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  submit: [data: UpdateUserSchema];
  close: [confirmed: boolean];
}>();

const form = ref();

// Local form state — the parent has no idea this exists
const state = reactive<UpdateUserSchema>({
  email: undefined,
  role: undefined,
  storageQuotaGb: undefined,
  userName: undefined,
});

// Whenever the parent swaps in a different user (or opens the modal),
// Re-sync the local state so the fields are pre-filled correctly.
// Quota intentionally stays empty (keep) — prefilling it would resubmit the old
// value and trip the shrink guard once usage has grown past it.
watch(
  () => props.user,
  (user) => {
    state.userName = user?.userName ?? undefined;
    state.email = user?.email ?? undefined;
    state.role = user?.role ?? undefined;
    state.storageQuotaGb = undefined;
  },
  { immediate: true },
);

const quotaPlaceholder = computed(() => {
  if (!props.user || props.user.storageQuota <= 0) return "Unlimited (current)";
  return `${bytesToGb(props.user.storageQuota)} GB (current)`;
});

const onQuotaInput = (value: string | number) => {
  state.storageQuotaGb = parseQuotaGbInput(value);
};

const roleOptions = [
  { label: "User", value: UserRole.User },
  { label: "Administrator", value: UserRole.Admin },
];
</script>
