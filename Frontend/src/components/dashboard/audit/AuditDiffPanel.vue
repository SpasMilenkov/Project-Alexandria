<script setup lang="ts">
import type { MetadataRenderer } from "@/composables/useAuditMessage";

defineProps<{
  metadata: Extract<MetadataRenderer, { type: "diff" }>;
}>();
</script>

<template>
  <div class="flex min-w-0 flex-col gap-1.5 sm:flex-row sm:flex-wrap sm:items-center sm:gap-2">
    <span
      class="inline-flex min-w-0 items-center self-start rounded-md bg-rose-500/10 px-2.5 py-1 font-mono text-xs break-all text-rose-700 ring-1 ring-rose-500/20 ring-inset dark:text-rose-300 dark:ring-rose-400/20"
    >
      {{ metadata.old }}
    </span>

    <UIcon
      name="i-lucide-arrow-right"
      class="hidden h-3.5 w-3.5 shrink-0 text-gray-500 sm:block dark:text-gray-500"
    />

    <UIcon
      name="i-lucide-arrow-down"
      class="ml-2 h-3.5 w-3.5 shrink-0 text-gray-500 sm:hidden dark:text-gray-500"
    />

    <span
      class="inline-flex min-w-0 items-center self-start rounded-md bg-emerald-500/10 px-2.5 py-1 font-mono text-xs break-all text-emerald-700 ring-1 ring-emerald-500/20 ring-inset dark:text-emerald-300 dark:ring-emerald-400/20"
    >
      {{ metadata.new }}
    </span>
  </div>
</template>
