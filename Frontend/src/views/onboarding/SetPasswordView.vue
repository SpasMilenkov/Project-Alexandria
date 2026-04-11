<template>
  <div class="flex flex-1 items-center justify-center">
    <div class="w-full max-w-4xl">
      <div
        class="overflow-hidden rounded-2xl border border-gray-200/60 bg-white/50 shadow-lg shadow-black/5 backdrop-blur-sm dark:border-gray-700/50 dark:bg-white/4"
      >
        <div class="flex flex-col lg:flex-row lg:min-h-120">
          <!-- LEFT: form pane -->
          <div
            class="flex flex-col justify-between p-4 md:p-8 lg:w-[52%] lg:border-r lg:border-gray-200/60 lg:dark:border-gray-700/50"
          >
            <!-- Header -->
            <div>
              <div class="mb-7 flex items-start gap-4">
                <div
                  class="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary/10"
                >
                  <Icon icon="mdi:lock-reset" class="h-5 w-5 text-primary" />
                </div>
                <div>
                  <h1 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
                    Create your password
                  </h1>
                  <p class="mt-1 text-sm leading-relaxed text-gray-500 dark:text-gray-400">
                    Your administrator set a temporary password for this account. Replace it with
                    something only you know.
                  </p>
                </div>
              </div>

              <UForm
                :schema="changePasswordSchema"
                :state="form"
                class="space-y-5"
                @submit="handleSubmit"
                @keydown.enter="handleSubmit"
              >
                <!-- Temporary password -->
                <UFormField
                  label="Temporary password"
                  name="initialPassword"
                  description="The one-time password you received from your administrator."
                >
                  <UInput
                    v-model="form.initialPassword"
                    :type="show.initial ? 'text' : 'password'"
                    placeholder="Paste the temporary password"
                    autocomplete="current-password"
                    class="w-full"
                    :ui="{ base: 'w-full' }"
                  >
                    <template #trailing>
                      <button
                        type="button"
                        class="flex items-center text-gray-400 transition-colors hover:text-gray-600 dark:hover:text-gray-300"
                        :aria-label="show.initial ? 'Hide password' : 'Show password'"
                        @click="show.initial = !show.initial"
                      >
                        <Icon
                          :icon="show.initial ? 'mdi:eye-off-outline' : 'mdi:eye-outline'"
                          class="h-4 w-4"
                        />
                      </button>
                    </template>
                  </UInput>
                </UFormField>

                <!-- New password -->
                <UFormField label="New password" name="newPassword">
                  <UInput
                    v-model="form.newPassword"
                    :type="show.new ? 'text' : 'password'"
                    placeholder="Create a strong password"
                    autocomplete="new-password"
                    class="w-full"
                    :ui="{ base: 'w-full' }"
                  >
                    <template #trailing>
                      <button
                        type="button"
                        class="flex items-center text-gray-400 transition-colors hover:text-gray-600 dark:hover:text-gray-300"
                        :aria-label="show.new ? 'Hide password' : 'Show password'"
                        @click="show.new = !show.new"
                      >
                        <Icon
                          :icon="show.new ? 'mdi:eye-off-outline' : 'mdi:eye-outline'"
                          class="h-4 w-4"
                        />
                      </button>
                    </template>
                  </UInput>
                </UFormField>

                <!-- Confirm password -->
                <UFormField label="Confirm new password" name="confirmPassword">
                  <UInput
                    v-model="form.confirmPassword"
                    :type="show.confirm ? 'text' : 'password'"
                    placeholder="Repeat your new password"
                    autocomplete="new-password"
                    class="w-full"
                    :ui="{ base: 'w-full' }"
                  >
                    <template #trailing>
                      <button
                        type="button"
                        class="flex items-center text-gray-400 transition-colors hover:text-gray-600 dark:hover:text-gray-300"
                        :aria-label="show.confirm ? 'Hide password' : 'Show password'"
                        @click="show.confirm = !show.confirm"
                      >
                        <Icon
                          :icon="show.confirm ? 'mdi:eye-off-outline' : 'mdi:eye-outline'"
                          class="h-4 w-4"
                        />
                      </button>
                    </template>
                  </UInput>
                </UFormField>

                <!-- Server error -->
                <Transition
                  enter-active-class="transition-all duration-200 ease-out"
                  enter-from-class="opacity-0 -translate-y-1"
                  leave-active-class="transition-all duration-150 ease-in"
                  leave-to-class="opacity-0"
                >
                  <div
                    v-if="serverError"
                    class="flex items-start gap-3 rounded-xl border border-red-200/70 bg-red-50/70 px-4 py-3.5 dark:border-red-900/50 dark:bg-red-950/30"
                  >
                    <Icon
                      icon="mdi:alert-circle-outline"
                      class="mt-0.5 h-4 w-4 shrink-0 text-red-500"
                    />
                    <div>
                      <p class="text-sm font-medium text-red-700 dark:text-red-400">
                        {{ serverError.title }}
                      </p>
                      <p
                        v-if="serverError.hint"
                        class="mt-0.5 text-xs text-red-500/80 dark:text-red-400/70"
                      >
                        {{ serverError.hint }}
                      </p>
                    </div>
                  </div>
                </Transition>

                <UButton
                  type="submit"
                  color="primary"
                  variant="solid"
                  size="lg"
                  block
                  trailing-icon="i-mdi-arrow-right"
                >
                  Set password & continue
                </UButton>
              </UForm>
            </div>
          </div>

          <!-- RIGHT: strength & requirements pane -->
          <div
            class="flex flex-col justify-center gap-8 bg-gray-50/40 p-8 dark:bg-white/2 lg:flex-1"
          >
            <!-- Strength meter -->
            <div>
              <p
                class="mb-3 text-xs font-semibold uppercase tracking-widest text-gray-400 dark:text-gray-500"
              >
                Password strength
              </p>
              <div class="flex gap-1.5 mb-3">
                <div
                  v-for="i in 5"
                  :key="i"
                  class="h-2 flex-1 rounded-full transition-all duration-300"
                  :class="
                    form.newPassword && i <= strength.score
                      ? strength.barColor
                      : 'bg-gray-200/70 dark:bg-gray-700/70'
                  "
                />
              </div>
              <p
                class="text-sm font-medium transition-colors duration-200"
                :class="form.newPassword ? strength.textColor : 'text-gray-400 dark:text-gray-500'"
              >
                {{ form.newPassword ? strength.label : "Enter a password to see its strength" }}
              </p>
            </div>

            <!-- Divider -->
            <div class="border-t border-gray-200/60 dark:border-gray-700/50" />

            <!-- Requirements -->
            <div>
              <p
                class="mb-4 text-xs font-semibold uppercase tracking-widest text-gray-400 dark:text-gray-500"
              >
                Requirements
              </p>
              <div class="space-y-3">
                <div
                  v-for="req in requirements"
                  :key="req.label"
                  class="flex items-center gap-3 transition-colors duration-200"
                  :class="
                    req.met
                      ? 'text-green-600 dark:text-green-400'
                      : 'text-gray-400 dark:text-gray-500'
                  "
                >
                  <div
                    class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full transition-colors duration-200"
                    :class="
                      req.met
                        ? 'bg-green-100 dark:bg-green-900/30'
                        : 'bg-gray-100/80 dark:bg-white/5'
                    "
                  >
                    <Icon :icon="req.met ? 'mdi:check' : 'mdi:minus'" class="h-3.5 w-3.5" />
                  </div>
                  <span class="text-sm">{{ req.label }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from "vue";
import { useRouter } from "vue-router";
import { Icon } from "@iconify/vue";
import type { AxiosError } from "axios";
import { changeInitialPassword } from "@/mutations/user";
import { changePasswordSchema } from "@/schemas/user";
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { OnboardingStep } from "@/enums";

useOnboardingGuard(OnboardingStep.SetPassword);

const router = useRouter();
const { mutate, error: mutationError, status } = changeInitialPassword();

watch(status, (s) => {
  if (s === "success") router.push({ name: "setup-profile" });
});

const form = reactive({ confirmPassword: "", initialPassword: "", newPassword: "" });
const show = reactive({ confirm: false, initial: false, new: false });

const serverError = computed(() => {
  if (!mutationError.value) return null;
  const axiosErr = mutationError.value as AxiosError<{ message?: string }>;
  const httpStatus = axiosErr?.response?.status;
  const serverMessage = axiosErr?.response?.data?.message;

  const map: Record<number, { title: string; hint?: string }> = {
    400: {
      hint: "Double-check the password your administrator sent you, then try again.",
      title: "The temporary password you entered is incorrect.",
    },
    401: {
      hint: "Please log out and log back in, then try again.",
      title: "Your session has expired.",
    },
    403: {
      hint: "Contact your administrator if you think this is a mistake.",
      title: "This action isn't allowed for your account.",
    },
    404: {
      hint: "If you just received your invite, try logging in again from the start.",
      title: "Account not found.",
    },
    409: {
      hint: "Choose a different password that you haven't used on this account before.",
      title: "That password has already been used.",
    },
    422: {
      hint: "Make sure all five requirements are satisfied.",
      title: "Your new password doesn't meet the requirements.",
    },
    429: { hint: "Wait a minute before trying again.", title: "Too many attempts." },
  };

  if (httpStatus && map[httpStatus]) return map[httpStatus];
  if (httpStatus && httpStatus >= 500)
    return {
      hint: "This is not your fault — please try again in a moment.",
      title: "Something went wrong on our end.",
    };
  return {
    hint: "If this keeps happening, please contact your administrator.",
    title:
      serverMessage && serverMessage.length < 120 ? serverMessage : "An unexpected error occurred.",
  };
});

const requirements = computed(() => [
  { label: "At least 8 characters", met: form.newPassword.length >= 8 },
  { label: "One uppercase letter", met: /[A-Z]/.test(form.newPassword) },
  { label: "One lowercase letter", met: /[a-z]/.test(form.newPassword) },
  { label: "One number", met: /[0-9]/.test(form.newPassword) },
  { label: "One special character", met: /[^a-zA-Z0-9]/.test(form.newPassword) },
]);

const strength = computed(() => {
  const score = requirements.value.filter((r) => r.met).length;
  const levels = [
    { barColor: "bg-red-400", label: "Very weak", textColor: "text-red-500" },
    { barColor: "bg-orange-400", label: "Weak", textColor: "text-orange-500" },
    { barColor: "bg-yellow-400", label: "Fair", textColor: "text-yellow-600 dark:text-yellow-400" },
    { barColor: "bg-lime-400", label: "Good", textColor: "text-lime-600 dark:text-lime-400" },
    { barColor: "bg-green-500", label: "Strong", textColor: "text-green-600 dark:text-green-400" },
  ];
  return { score, ...(levels[score - 1] ?? levels[0]) };
});

const handleSubmit = () => {
  mutate({ ...form });
};
</script>
