<script setup lang="ts">
import type { MetadataRenderer } from "@/composables/useAuditMessage";

defineProps<{
  metadata: Extract<MetadataRenderer, { type: "diff" }>;
}>();
</script>

<template>
  <div class="flex flex-col gap-1">
    <p class="text-xs font-medium text-muted">{{ metadata.label }}</p>
    <div class="flex items-center gap-2 flex-wrap">
      <!-- Before -->
      <span
        class="inline-flex items-center rounded-md px-3 py-1 font-mono text-xs bg-rose-500/10 text-rose-700 dark:text-rose-300 ring-1 ring-inset ring-rose-500/20 dark:ring-rose-400/20"
      >
        {{ metadata.old }}
      </span>

      <!-- Arrow -->
      <UIcon name="i-lucide-arrow-right" class="w-3.5 h-3.5 text-muted shrink-0" />

      <!-- After -->
      <span
        class="inline-flex items-center rounded-md px-3 py-1 font-mono text-xs bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 ring-1 ring-inset ring-emerald-500/20 dark:ring-emerald-400/20"
      >
        {{ metadata.new }}
      </span>
    </div>
  </div>
</template>
