<script setup lang="ts">
import { glassDrawerContent } from "@/utils/modalUi";

const open = defineModel<boolean>("open", { required: true });

defineProps<{ docked: boolean }>();
</script>

<template>
  <aside
    v-if="docked"
    aria-label="Dashboard editing panel"
    class="home-inspector frosted-glass glass-surface flex min-h-0 min-w-0 flex-col border-gray-200/70 dark:border-gray-700/70"
  >
    <slot />
  </aside>

  <UDrawer
    v-else
    v-model:open="open"
    title="Dashboard editing panel"
    description="Dashboard layout settings or the selected widget settings."
    direction="bottom"
    :modal="false"
    :overlay="false"
    :handle-only="true"
    :ui="{ content: `${glassDrawerContent} z-30 max-h-[92dvh]` }"
  >
    <template #content>
      <div class="flex min-h-0 flex-col overflow-y-auto overscroll-contain">
        <div
          class="sticky top-0 z-10 flex items-center justify-end rounded-t-lg bg-default px-4 pb-2 pt-2"
        >
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-heroicons-x-mark"
            aria-label="Back to layout"
            @click="() => (open = false)"
          />
        </div>

        <slot />
      </div>
    </template>
  </UDrawer>
</template>

<style scoped>
.home-inspector {
  border-left-width: 1px;
}
</style>
