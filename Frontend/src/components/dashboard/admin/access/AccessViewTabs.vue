<script setup lang="ts">
import { useRoute } from "vue-router";

const route = useRoute();

const TABS = [
  { icon: "i-lucide-shield-check", label: "Overview", name: "admin-activity" },
  { icon: "i-lucide-list", label: "Full log", name: "admin-activity-log" },
] as const;

const isActive = (name: string) => route.name === name;

const tabColor = (name: string) => {
  if (isActive(name)) return "primary";

  return "neutral";
};

const tabVariant = (name: string) => {
  if (isActive(name)) return "soft";

  return "ghost";
};
</script>

<template>
  <nav
    aria-label="Access and security views"
    class="inline-flex shrink-0 items-center gap-0.5 rounded-lg border border-gray-200 p-0.5 dark:border-gray-700"
  >
    <UButton
      v-for="tab in TABS"
      :key="tab.name"
      :to="{ name: tab.name }"
      :icon="tab.icon"
      :label="tab.label"
      :color="tabColor(tab.name)"
      :variant="tabVariant(tab.name)"
      :aria-current="isActive(tab.name) ? 'page' : undefined"
      size="xs"
      class="rounded-md"
    />
  </nav>
</template>
