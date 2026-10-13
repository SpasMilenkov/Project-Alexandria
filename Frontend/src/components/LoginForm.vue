<script setup lang="ts">
import type { AuthFormField, FormSubmitEvent } from "@nuxt/ui";

import { useRoute, useRouter } from "vue-router";

import { type LoginSchema, loginSchema } from "@/schemas/auth";
import { useAuthStore } from "@/stores/auth";
import { getSignInDestination } from "@/utils/home-landing";

const toast = useToast();
const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

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

  await router.replace(
    getSignInDestination(router, authStore.user?.onboardingStep, route.query.redirect),
  );
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
