<template>
  <UModal
    :open="open"
    :title="user?.isLockedOut ? 'Remove Restriction' : 'Restrict Account'"
    :description="
      user
        ? `@${user.userName} will be ${user.isLockedOut ? 'unlocked immediately' : 'locked until the chosen date'}`
        : ''
    "
    :close="{ onClick: () => emit('close', false) }"
  >
    <template #body>
      <div class="space-y-4 p-1">
        <UAlert
          :color="user?.isLockedOut ? 'success' : 'warning'"
          variant="subtle"
          :icon="user?.isLockedOut ? 'i-lucide-lock-open' : 'i-lucide-triangle-alert'"
          :title="
            user?.isLockedOut
              ? 'The user will regain access immediately.'
              : 'The user will be unable to sign in until this date passes.'
          "
        />

        <!-- @vue-expect-error -->
        <UForm
          v-if="!user?.isLockedOut"
          ref="form"
          :schema="restrictUserSchema"
          :state="state"
          class="space-y-4"
          @submit="onSubmit"
        >
          <UFormField
            label="Lockout ends on"
            name="lockoutEndDate"
            description="Must be a future date."
            class="w-full"
          >
            <!-- @vue-ignore -->
            <UInputDate ref="lockoutEndDateInput" v-model="state.lockoutEndDate" class="w-full">
              <template #trailing>
                <UPopover :reference="lockoutEndDateInput?.inputsRef[3]?.$el">
                  <UButton
                    color="neutral"
                    variant="link"
                    size="sm"
                    icon="i-lucide-calendar"
                    aria-label="Pick a date"
                    class="px-0"
                  />
                  <template #content>
                    <!-- @vue-ignore -->
                    <UCalendar v-model="state.lockoutEndDate" />
                  </template>
                </UPopover>
              </template>
            </UInputDate>
          </UFormField>
        </UForm>
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton label="Cancel" color="neutral" variant="outline" @click="emit('close', false)" />
        <UButton
          :label="user?.isLockedOut ? 'Remove restriction' : 'Apply restriction'"
          :icon="user?.isLockedOut ? 'i-lucide-lock-open' : 'i-lucide-lock'"
          color="warning"
          variant="solid"
          :loading="loading"
          @click="handleConfirm"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { CalendarDate } from "@internationalized/date";
import type { FormSubmitEvent } from "@nuxt/ui";

import { reactive, ref, shallowRef, watch } from "vue";

import type { UserDetailsDto } from "@/types/user";

import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { type RestrictUserSchema, restrictUserSchema } from "@/schemas/user";

const props = defineProps<{
  open: boolean;
  user: UserDetailsDto | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  submit: [data: RestrictUserSchema | { userId: string }];
  close: [confirmed: boolean];
}>();

useModalBackGuard(() => emit("close", false));

const form = ref();
const lockoutEndDateInput = shallowRef();

const state = reactive<{ lockoutEndDate: CalendarDate | undefined; userId: string }>({
  lockoutEndDate: undefined,
  userId: "",
});

watch(
  () => props.user,
  (user) => {
    state.userId = user?.id ?? "";
    state.lockoutEndDate = undefined;
  },
  { immediate: true },
);

const handleConfirm = () => {
  if (props.user?.isLockedOut) {
    emit("submit", { userId: props.user.id });
  } else {
    form.value?.submit();
  }
};

const onSubmit = (event: FormSubmitEvent<RestrictUserSchema>) => {
  emit("submit", event.data);
};
</script>
