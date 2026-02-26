<script setup lang="ts">
import type { AuthFormField, FormSubmitEvent } from "@nuxt/ui";
import { type LoginSchema, loginSchema } from "@/schemas/auth";
import { useAuthStore } from "@/stores/auth";
import { useRouter } from "vue-router";

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

async function onSubmit(payload: FormSubmitEvent<LoginSchema>) {
  await authStore.login(payload.data);

  if (authStore.error) {
    toast.add({
      description: authStore.error,
      title: "Error trying to log in",
    });
    return;
  }
  router.push("/dashboard");
}
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-4 p-4">
    <UPageCard class="w-full max-w-md">
      <UAuthForm
        :schema="loginSchema"
        title="Login"
        description="Enter your credentials to access your account."
        icon="i-lucide-user"
        :fields="fields"
        @submit="onSubmit"
      />
    </UPageCard>
  </div>
</template>
