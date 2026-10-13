<script setup lang="ts">
import { HomeGreetingStyle } from "@/enums/home-widget";

defineProps<{ greetingStyle: HomeGreetingStyle }>();
</script>

<template>
  <span
    class="flex size-24 shrink-0 justify-center gap-2 overflow-hidden rounded-lg bg-gray-100 p-2 text-gray-600 dark:bg-gray-800 dark:text-gray-400"
    :class="greetingStyle === HomeGreetingStyle.Welcome ? 'flex-col items-start' : 'items-center'"
    aria-hidden="true"
  >
    <span class="shrink-0 text-2xl leading-none">👋</span>

    <span class="flex w-full min-w-0 flex-col gap-2">
      <span
        class="block w-full rounded bg-current"
        :class="greetingStyle === HomeGreetingStyle.Welcome ? 'h-2' : 'h-1'"
      />
      <span class="block h-0.5 w-3/4 rounded bg-current" />
    </span>
  </span>
</template>
