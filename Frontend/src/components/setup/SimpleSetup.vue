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
    description: "Create a system account",
    icon: "i-lucide-user",
    title: "Registration",
    value: "Registration",
  },
  {
    description: "Select your features",
    icon: "i-lucide-library-big",
    title: "Configure ",
    value: "Configuration",
  },
  {
    description: "Finalize your configuration",
    icon: "i-lucide-book-check",
    title: "Finalize",
    value: "Finalization",
  },
];
</script>
<style scoped></style>
