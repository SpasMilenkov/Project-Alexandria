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
    </UForm>
  </ConfirmModal>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from "vue";
import { updateUserSchema } from "@/schemas/user";
import type { UpdateUserSchema } from "@/schemas/user";
import type { UserDetailsDto } from "@/types/user";
import { UserRole } from "@/enums/UserRole";

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
  email: null,
  role: null,
  userName: null,
});

// Whenever the parent swaps in a different user (or opens the modal),
// Re-sync the local state so the fields are pre-filled correctly.
watch(
  () => props.user,
  (user) => {
    state.userName = user?.userName ?? null;
    state.email = user?.email ?? null;
    state.role = user?.role ?? null;
  },
  { immediate: true },
);

const roleOptions = [
  { label: "User", value: UserRole.User },
  { label: "Administrator", value: UserRole.Admin },
];
</script>
