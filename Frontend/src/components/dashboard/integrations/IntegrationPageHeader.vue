<template>
  <header
    class="flex shrink-0 flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
  >
    <div class="flex min-w-0 items-center gap-2">
      <UTooltip text="All integrations">
        <UButton
          to="/dashboard/admin/integrations"
          icon="i-lucide-chevron-left"
          color="neutral"
          variant="ghost"
          size="sm"
          square
          class="-ml-2 justify-center"
          aria-label="Back to all integrations"
        />
      </UTooltip>

      <UIcon :name="icon" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
      <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">{{ title }}</h1>

      <UBadge v-if="status" :color="status.color" variant="subtle" size="sm">
        {{ status.label }}
      </UBadge>
    </div>

    <div v-if="$slots.actions" class="flex flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { ServiceType } from "@/enums";

import { useServiceStatus } from "@/composables/useServiceStatus";

const { title, icon, services } = defineProps<{
  title: string;
  icon: string;
  // Services whose live health the header badge reports (the worst of them).
  services: ServiceType[];
}>();

const HEALTH_COLORS: Record<string, "success" | "warning" | "error"> = {
  Degraded: "warning",
  Down: "error",
  Healthy: "success",
};

const { isLoading, worstOf } = useServiceStatus();

const status = computed(() => {
  if (isLoading.value || services.length === 0) return null;

  const { health } = worstOf(services);

  return { color: HEALTH_COLORS[health.label] ?? "success", label: health.label };
});
</script>
