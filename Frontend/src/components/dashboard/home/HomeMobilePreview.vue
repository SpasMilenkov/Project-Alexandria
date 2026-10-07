<script setup lang="ts">
defineProps<{ shared: boolean; dropActive: boolean }>();
</script>

<template>
  <section aria-label="Mobile layout preview" class="w-full max-w-108 min-w-0">
    <header class="mb-4 flex items-center gap-2">
      <UIcon
        name="i-heroicons-device-phone-mobile"
        class="size-5 shrink-0 text-gray-600 dark:text-gray-400"
      />
      <div>
        <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Mobile layout</h2>
        <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">
          {{ shared ? "Shared layout preview" : "Your phone dashboard" }}
        </p>
      </div>
    </header>

    <div
      class="frosted-glass glass-surface rounded-4xl border-2 border-gray-300/70 p-3 dark:border-gray-600/70"
      :class="{ 'ring-2 ring-primary ring-offset-2': dropActive }"
    >
      <div class="mb-4 flex justify-center" aria-hidden="true">
        <div class="h-1.5 w-12 rounded-full bg-gray-300 dark:bg-gray-600" />
      </div>

      <div
        data-preview="mobile"
        class="mx-auto max-h-[min(65dvh,700px)] min-h-96 w-full max-w-97.5 overflow-y-auto"
      >
        <slot />
      </div>

      <div class="mt-4 flex justify-center" aria-hidden="true">
        <div class="h-1 w-24 rounded-full bg-gray-300 dark:bg-gray-600" />
      </div>
    </div>
  </section>
</template>
