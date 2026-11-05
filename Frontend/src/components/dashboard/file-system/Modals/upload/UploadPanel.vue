<template>
  <div class="overflow-hidden rounded-lg border border-gray-200/70 dark:border-gray-700/70">
    <div
      v-if="$slots.header"
      class="flex min-h-12 items-center gap-3 border-b border-gray-200/70 px-4 py-2 dark:border-gray-700/70"
    >
      <slot name="header" />
    </div>

    <slot />

    <slot name="footer" />
  </div>
</template>
