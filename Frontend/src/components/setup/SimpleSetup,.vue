<template>
  <UStepper ref="stepper" :items="items" class="w-full">
    <template #content="{ item }">
      <RegistrationForm v-if="item.value === 'Registration'" />
    </template>
  </UStepper>
</template>
<script setup lang="ts">
import type { StepperItem } from "@nuxt/ui";
import RegistrationForm from "./RegistrationForm.vue";

const items: StepperItem[] = [
  {
    value: "Registration",
    title: "Registration",
    description: "Create a system account",
    icon: "i-lucide-user",
  },
  {
    value: "Configuration",
    title: "Configure ",
    description: "Select your features",
    icon: "i-lucide-library-big",
  },
  {
    value: "Finalization",
    title: "Finalize",
    description: "Finalize your configuration",
    icon: "i-lucide-book-check",
  },
];
</script>
<style scoped></style>
