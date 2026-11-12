<script setup lang="ts">
import ActiveSessionsCard from "@/components/dashboard/account/ActiveSessionsCard.vue";
import SignInHistoryCard from "@/components/dashboard/account/SignInHistoryCard.vue";
</script>

<template>
  <div class="flex min-h-full w-full flex-col gap-3 p-3">
    <header
      class="flex shrink-0 items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-user" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">My account</h1>
      </div>
    </header>

    <div class="mx-auto flex w-full max-w-3xl flex-col gap-3">
      <div class="flex items-center gap-2 px-1 pt-2">
        <UIcon name="i-lucide-shield-check" class="h-4 w-4 text-gray-600 dark:text-gray-400" />
        <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">Security</h2>
      </div>

      <ActiveSessionsCard />

      <SignInHistoryCard />
    </div>
  </div>
</template>
