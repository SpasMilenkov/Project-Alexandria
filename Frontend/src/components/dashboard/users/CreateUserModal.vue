<template>
  <UModal
    :open="open"
    title="New account"
    description="They'll receive an invitation email once the account is created."
    :close="{ onClick: () => emit('close') }"
    :ui="formModalUi"
    @update:open="onOpenChange"
  >
    <template #body>
      <UForm
        :schema="createUserSchema"
        :state="state"
        :disabled="loading"
        class="flex min-h-0 flex-1 flex-col"
        @submit="onSubmit"
      >
        <div class="min-h-0 space-y-8 overflow-y-auto overscroll-contain p-6 md:p-8">
          <fieldset class="min-w-0 space-y-4">
            <legend class="mb-4 text-sm font-semibold text-gray-900 dark:text-gray-100">
              Account
            </legend>

            <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
              <UFormField
                label="Username"
                name="userName"
                description="Letters, numbers, dots, dashes, underscores."
                required
              >
                <UInput
                  v-model="state.userName"
                  placeholder="jane.smith"
                  icon="i-lucide-user"
                  autocomplete="off"
                  size="lg"
                  class="w-full"
                  autofocus
                />
              </UFormField>

              <UFormField
                label="Email address"
                name="email"
                description="Where the invitation is sent."
                required
              >
                <UInput
                  v-model="state.email"
                  type="email"
                  placeholder="jane@example.com"
                  icon="i-lucide-mail"
                  autocomplete="off"
                  size="lg"
                  class="w-full"
                />
              </UFormField>
            </div>
          </fieldset>

          <fieldset class="min-w-0 space-y-4">
            <legend class="mb-4 text-sm font-semibold text-gray-900 dark:text-gray-100">
              Password
            </legend>

            <div class="grid grid-cols-1 gap-4 md:grid-cols-2">
              <UFormField label="Password" name="password" required>
                <UInput
                  v-model="state.password"
                  :type="passwordInputType"
                  icon="i-lucide-lock"
                  autocomplete="new-password"
                  size="lg"
                  class="w-full"
                >
                  <template #trailing>
                    <UButton
                      color="neutral"
                      variant="link"
                      size="sm"
                      :icon="visibilityIcon"
                      :aria-label="visibilityLabel"
                      :aria-pressed="showPasswords"
                      class="justify-center"
                      @click="showPasswords = !showPasswords"
                    />
                  </template>
                </UInput>
              </UFormField>

              <UFormField label="Confirm password" name="confirmPassword" required>
                <UInput
                  v-model="state.confirmPassword"
                  :type="passwordInputType"
                  icon="i-lucide-lock-keyhole"
                  autocomplete="new-password"
                  size="lg"
                  class="w-full"
                />
              </UFormField>
            </div>

            <div class="flex items-center gap-3">
              <div class="flex flex-1 gap-1" aria-hidden="true">
                <span
                  v-for="bar in STRENGTH_BARS"
                  :key="bar"
                  class="h-1 flex-1 rounded-full transition-colors duration-200"
                  :class="strengthBarClass(bar)"
                />
              </div>

              <p
                class="w-24 shrink-0 text-right text-xs"
                :class="strengthTextClass"
                aria-live="polite"
              >
                {{ strengthLabel }}
              </p>
            </div>
          </fieldset>

          <fieldset class="min-w-0 space-y-4">
            <legend class="mb-4 text-sm font-semibold text-gray-900 dark:text-gray-100">
              Access
            </legend>

            <UFormField label="Role" name="role">
              <div
                role="radiogroup"
                aria-label="Role"
                class="grid grid-cols-1 gap-3 sm:grid-cols-2"
              >
                <button
                  v-for="option in ROLE_OPTIONS"
                  :key="option.value"
                  type="button"
                  role="radio"
                  :aria-checked="state.role === option.value"
                  :disabled="loading"
                  class="flex items-start gap-3 rounded-xl border p-4 text-left transition-all duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary disabled:cursor-not-allowed disabled:opacity-60"
                  :class="roleTileClass(option.value)"
                  @click="state.role = option.value"
                >
                  <span
                    class="flex size-9 shrink-0 items-center justify-center rounded-lg bg-elevated"
                  >
                    <UIcon
                      :name="option.icon"
                      class="size-5"
                      :class="roleIconClass(option.value)"
                    />
                  </span>

                  <span class="min-w-0 flex-1">
                    <span class="block text-sm font-medium text-gray-900 dark:text-gray-100">
                      {{ option.label }}
                    </span>
                    <span class="mt-0.5 block text-xs text-gray-600 dark:text-gray-400">
                      {{ option.description }}
                    </span>
                  </span>

                  <UIcon
                    :name="roleIndicatorIcon(option.value)"
                    class="size-4 shrink-0"
                    :class="roleIconClass(option.value)"
                  />
                </button>
              </div>
            </UFormField>

            <UFormField
              label="Storage quota"
              name="storageQuotaGb"
              description="Leave empty for the 10 GB default. 0 means unlimited."
              class="md:w-1/2 md:pe-2"
            >
              <UInput
                :model-value="state.storageQuotaGb"
                type="number"
                min="0"
                placeholder="10"
                icon="i-lucide-hard-drive"
                size="lg"
                class="w-full"
                @update:model-value="onQuotaInput"
              >
                <template #trailing>
                  <span class="text-xs text-gray-500 dark:text-gray-500">GB</span>
                </template>
              </UInput>
            </UFormField>
          </fieldset>
        </div>

        <div
          class="flex shrink-0 justify-end gap-2 border-t border-gray-200/70 bg-default p-4 dark:border-gray-700/70 sm:px-6 md:px-8"
        >
          <UButton
            type="button"
            color="neutral"
            variant="outline"
            size="lg"
            class="justify-center"
            :disabled="loading"
            @click="emit('close')"
          >
            Cancel
          </UButton>

          <UButton
            type="submit"
            color="primary"
            variant="solid"
            size="lg"
            icon="i-lucide-user-plus"
            class="flex-1 justify-center sm:flex-none"
            :loading="loading"
          >
            Create account
          </UButton>
        </div>
      </UForm>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { FormSubmitEvent } from "@nuxt/ui";

import { computed, reactive, ref } from "vue";

import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { UserRole } from "@/enums/UserRole";
import { type CreateUserSchema, createUserSchema } from "@/schemas/user";
import { formModalUi } from "@/utils/modal-ui";
import { parseQuotaGbInput } from "@/utils/size";

defineProps<{
  open: boolean;
  loading?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [data: CreateUserSchema];
}>();

useModalBackGuard(() => emit("close"));

const ROLE_OPTIONS = [
  {
    description: "Browses and manages their own files and media.",
    icon: "i-lucide-user",
    label: "User",
    value: UserRole.User,
  },
  {
    description: "Manages users, system settings and all content.",
    icon: "i-lucide-shield-check",
    label: "Administrator",
    value: UserRole.Admin,
  },
];

const STRENGTH_BARS = [1, 2, 3, 4];

const STRENGTH_LEVELS = [
  {
    bar: "bg-red-500",
    label: "Too weak",
    text: "text-red-600 dark:text-red-400",
  },
  {
    bar: "bg-orange-500",
    label: "Weak",
    text: "text-orange-600 dark:text-orange-400",
  },
  {
    bar: "bg-yellow-500",
    label: "Fair",
    text: "text-yellow-700 dark:text-yellow-400",
  },
  {
    bar: "bg-emerald-500",
    label: "Strong",
    text: "text-emerald-600 dark:text-emerald-400",
  },
];

const state = reactive<CreateUserSchema>({
  confirmPassword: "",
  email: "",
  password: "",
  role: UserRole.User,
  storageQuotaGb: undefined,
  userName: "",
});

const showPasswords = ref(false);

const passwordInputType = computed(() => (showPasswords.value ? "text" : "password"));
const visibilityIcon = computed(() => (showPasswords.value ? "i-lucide-eye-off" : "i-lucide-eye"));
const visibilityLabel = computed(() => (showPasswords.value ? "Hide passwords" : "Show passwords"));

const strengthScore = computed(() => {
  const password = state.password;

  if (!password) {
    return 0;
  }

  // Under the minimum length the backend rejects it, so variety cannot lift it.
  if (password.length < 8) {
    return 1;
  }

  let score = 1;
  if (password.length >= 12) {
    score++;
  }
  if (/[A-Z]/u.test(password) && /[0-9]/u.test(password)) {
    score++;
  }
  if (/[^A-Za-z0-9]/u.test(password)) {
    score++;
  }

  return score;
});

const strengthLevel = computed(() => STRENGTH_LEVELS[Math.max(strengthScore.value, 1) - 1]);

const strengthLabel = computed(() => {
  if (strengthScore.value === 0) {
    return "At least 8 characters";
  }
  return strengthLevel.value.label;
});

const strengthTextClass = computed(() => {
  if (strengthScore.value === 0) {
    return "text-gray-500 dark:text-gray-500";
  }
  return strengthLevel.value.text;
});

const strengthBarClass = (bar: number) => {
  if (bar <= strengthScore.value) {
    return strengthLevel.value.bar;
  }
  return "bg-gray-200 dark:bg-gray-700";
};

const roleTileClass = (role: UserRole) => {
  if (state.role === role) {
    return "scale-[1.03] border-primary bg-primary/5 ring-1 ring-primary";
  }
  return "border-gray-200/70 hover:bg-elevated dark:border-gray-700/70";
};

const roleIconClass = (role: UserRole) => {
  if (state.role === role) {
    return "text-primary";
  }
  return "text-gray-500 dark:text-gray-500";
};

const roleIndicatorIcon = (role: UserRole) => {
  if (state.role === role) {
    return "i-lucide-circle-check";
  }
  return "i-lucide-circle";
};

const onQuotaInput = (value: string | number) => {
  state.storageQuotaGb = parseQuotaGbInput(value);
};

const onOpenChange = (isOpen: boolean) => {
  if (!isOpen) {
    emit("close");
  }
};

const onSubmit = (event: FormSubmitEvent<CreateUserSchema>) => {
  emit("submit", event.data);
};
</script>
