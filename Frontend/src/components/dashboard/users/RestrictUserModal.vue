<template>
  <ConfirmModal
    :open="open"
    :title="user?.isLockedOut ? 'Remove Restriction' : 'Restrict Account'"
    :description="
      user ? `@${user.userName} will be locked until the chosen date` : ''
    "
    :confirm-label="
      user?.isLockedOut ? 'Remove restriction' : 'Apply restriction'
    "
    :confirm-icon="user?.isLockedOut ? 'i-lucide-lock-open' : 'i-lucide-lock'"
    confirm-color="warning"
    :loading="loading"
    :alert="{
      title: user?.isLockedOut
        ? 'The user will regain access immediately.'
        : 'The user will be unable to sign in until this date passes.',
    }"
    @confirm="form?.submit()"
    @close="emit('close', $event)"
  >
    <UForm
      v-if="!user?.isLockedOut"
      ref="form"
      :schema="restrictUserSchema"
      :state="state"
      class="space-y-4"
      @submit="emit('submit', $event.data)"
    >
      <UFormField
        label="Lockout ends on"
        name="lockoutEndDate"
        description="Must be a future date."
      >
        <!-- @vue-ignore -->
        <UInputDate v-model="state.lockoutEndDate" class="w-full" />
      </UFormField>
    </UForm>
  </ConfirmModal>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from "vue";
import { restrictUserSchema } from "@/schemas/user";
import type { RestrictUserSchema } from "@/schemas/user";
import type { UserDetailsDto } from "@/types/user";

const props = defineProps<{
  open: boolean;
  user: UserDetailsDto | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  // When removing a restriction no form is needed, so we emit the userId directly.
  // When applying one, we emit the full schema data.
  submit: [data: RestrictUserSchema | { userId: string }];
  close: [confirmed: boolean];
}>();

const form = ref();

// Local form state — reset whenever a new user is loaded
const state = reactive<RestrictUserSchema>({
  userId: "",
  lockoutEndDate: null,
});

watch(
  () => props.user,
  (user) => {
    state.userId = user?.id ?? "";
    state.lockoutEndDate = null;

    // If the account is already locked, there's no date picker —
    // clicking confirm just emits the userId for the unlock call.
    if (user?.isLockedOut) {
      form.value = {
        submit: () => emit("submit", { userId: user.id }),
      };
    }
  },
  { immediate: true },
);
</script>
