<template>
  <div class="flex flex-col items-center justify-center gap-4 p-4">
    <UPageCard class="w-full max-w-md">
      <UAuthForm
        :schema="registerSchema"
        title="Register"
        description="Register your system account"
        icon="i-lucide-user"
        :fields="fields"
        @submit="onSubmit"
      />
    </UPageCard>
  </div>
</template>
<script setup lang="ts">
import type { AuthFormField, FormSubmitEvent } from "@nuxt/ui";
import { type RegisterSchema, registerSchema } from "@/schemas/auth";
import { useAuthStore } from "@/stores/auth";
import { useRouter } from "vue-router";
import { logger } from "@/utils/logger";

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
    label: "Username",
    name: "name",
    placeholder: "Enter your username",
    required: true,
    type: "password",
  },
  {
    label: "Password",
    name: "password",
    placeholder: "Enter your password",
    required: true,
    type: "password",
  },
  {
    label: "Confirm password",
    name: "confirm-password",
    placeholder: "Confirm your password",
    required: true,
    type: "password",
  },
];

async function onSubmit(payload: FormSubmitEvent<RegisterSchema>) {
  logger.log("Submitted", payload);
  toast.add({
    description: "Login Request successfully submitted",
    title: "Submitted",
  });
  await authStore.register(payload.data);

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
<style scoped></style>
