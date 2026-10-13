<script setup lang="ts">
import { useMediaQuery } from "@vueuse/core";

import { glassDrawerContent } from "@/utils/modalUi";

const open = defineModel<boolean>("open", { required: true });

defineProps<{ docked: boolean }>();

const isMobile = useMediaQuery("(max-width: 767px)");
</script>

<template>
  <aside
    v-if="docked"
    aria-label="Dashboard editing panel"
    class="frosted-glass glass-surface sticky top-24 flex max-h-[calc(100dvh-12rem)] min-w-0 flex-col gap-6 overflow-y-auto rounded-2xl border border-gray-200/70 p-4 dark:border-gray-700/70"
  >
    <slot />
  </aside>

  <UDrawer
    v-else
    v-model:open="open"
    :direction="isMobile ? 'bottom' : 'right'"
    title="Layout controls"
    description="Changes stay in your draft until you press Done."
    :ui="{ content: glassDrawerContent }"
  >
    <template #body>
      <div class="flex flex-col gap-6"><slot /></div>
    </template>

    <template #footer>
      <UButton color="neutral" variant="outline" class="justify-center" @click="open = false">
        Back to layout
      </UButton>
    </template>
  </UDrawer>
</template>
