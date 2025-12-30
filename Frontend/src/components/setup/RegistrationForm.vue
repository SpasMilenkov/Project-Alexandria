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
import type { FormSubmitEvent, AuthFormField } from "@nuxt/ui";
import { registerSchema, type RegisterSchema } from "@/schemas/auth";
import { useAuthStore } from "@/stores/auth";
import { useRouter } from "vue-router";

const toast = useToast();
const authStore = useAuthStore();
const router = useRouter();

const fields: AuthFormField[] = [
  {
    name: "email",
    type: "email",
    label: "Email",
    placeholder: "Enter your email",
    required: true,
  },
  {
    name: "name",
    label: "Username",
    type: "password",
    placeholder: "Enter your username",
    required: true,
  },
  {
    name: "password",
    label: "Password",
    type: "password",
    placeholder: "Enter your password",
    required: true,
  },
  {
    name: "confirm-password",
    label: "Confirm password",
    type: "password",
    placeholder: "Confirm your password",
    required: true,
  },
];

async function onSubmit(payload: FormSubmitEvent<RegisterSchema>) {
  console.log("Submitted", payload);
  toast.add({
    title: "Submitted",
    description: "Login Request successfully submitted",
  });
  await authStore.register(payload.data);

  if (authStore.error) {
    toast.add({
      title: "Error trying to log in",
      description: authStore.error,
    });
    return;
  }
  router.push("/dashboard");
}
</script>
<style scoped></style>
