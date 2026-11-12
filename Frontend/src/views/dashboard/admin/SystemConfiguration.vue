<script setup lang="ts">
import AuditRetentionPanel from "@/components/dashboard/admin/AuditRetentionPanel.vue";
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <header
      class="flex shrink-0 flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon
          name="i-lucide-sliders-horizontal"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
          System configuration
        </h1>
      </div>
    </header>

    <AuditRetentionPanel />
  </div>
</template>
