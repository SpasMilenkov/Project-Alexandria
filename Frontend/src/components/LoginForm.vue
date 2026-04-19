<script setup lang="ts">
import type { AuthFormField, FormSubmitEvent } from "@nuxt/ui";
import { type LoginSchema, loginSchema } from "@/schemas/auth";
import { useAuthStore } from "@/stores/auth";
import { useRouter } from "vue-router";
import { OnboardingStep } from "@/enums";

const toast = useToast();
const authStore = useAuthStore();
const router = useRouter();

const fields: AuthFormField[] = [
  {
    label: "Email",
    name: "email",
    placeholder: "Enter your email",
    required: true,
    type: "email",
  },
  {
    label: "Password",
    name: "password",
    placeholder: "Enter your password",
    required: true,
    type: "password",
  },
  {
    label: "Remember me",
    name: "remember",
    type: "checkbox",
  },
];

const onSubmit = async (payload: FormSubmitEvent<LoginSchema>) => {
  await authStore.login(payload.data);

  if (authStore.error) {
    toast.add({
      description: authStore.error,
      title: "Error trying to log in",
    });
    return;
  }
  switch (authStore.user?.onboardingStep) {
    case OnboardingStep.SetPassword:
      router.push("/onboarding/set-password");
      break;
    case OnboardingStep.CompleteProfile:
      router.push("/onboarding/setup-profile");
      break;
    case OnboardingStep.Tour:
      router.push("/onboarding/tour");
      break;
    case OnboardingStep.Done:
      router.push("/dashboard");
      break;
  }
};
</script>

<template>
  <UPageCard class="w-full max-w-md">
    <UAuthForm
      :schema="loginSchema"
      title="Sign in"
      description="Enter your credentials to access your account."
      :fields="fields"
      :submit="{ label: 'Continue', color: 'primary', block: true }"
      :ui="{
        header: 'flex flex-col',
        title: 'text-2xl font-semibold',
        description: 'mt-1 text-sm text-muted',
      }"
      @submit="onSubmit"
    >
    </UAuthForm>
  </UPageCard>
</template>
