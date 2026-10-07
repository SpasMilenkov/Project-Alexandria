<script setup lang="ts">
import { useMediaQuery } from "@vueuse/core";

import type { HomeWidgetType } from "@/enums/home-widget";

import HomeWidgetCatalogueItems from "@/components/dashboard/home/HomeWidgetCatalogueItems.vue";
import { glassDrawerContent } from "@/utils/modalUi";

const open = defineModel<boolean>("open", { required: true });
const isMobile = useMediaQuery("(max-width: 767px)");

defineProps<{ disabled: boolean; docked?: boolean; disabledReason?: string }>();

const emit = defineEmits<{
  add: [type: HomeWidgetType];
  pointerDrag: [type: HomeWidgetType, event: PointerEvent];
}>();
</script>

<template>
  <template v-if="docked">
    <aside
      v-if="open"
      aria-label="Widget catalogue"
      class="flex min-w-0 flex-col gap-4 border-t border-gray-200/70 pt-4 dark:border-gray-700/70"
    >
      <header class="flex items-start justify-between gap-4">
        <div>
          <h2 class="font-semibold text-gray-900 dark:text-gray-100">Add a widget</h2>
          <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
            Drag a widget onto the grid, or select Add.
          </p>
        </div>

        <UButton
          color="neutral"
          variant="ghost"
          icon="i-heroicons-x-mark"
          aria-label="Close widget catalogue"
          class="min-h-10 min-w-10 justify-center"
          @click="open = false"
        />
      </header>

      <HomeWidgetCatalogueItems
        :disabled="disabled"
        :disabled-reason="disabledReason"
        :draggable="true"
        @add="emit('add', $event)"
        @pointer-drag="(type, event) => emit('pointerDrag', type, event)"
      />
    </aside>
  </template>

  <UDrawer
    v-else
    v-model:open="open"
    :direction="isMobile ? 'bottom' : 'right'"
    title="Add a widget"
    description="Choose what belongs on your Home page."
    :ui="{ content: glassDrawerContent }"
  >
    <template #body>
      <div class="mx-auto w-full max-w-2xl pb-6">
        <HomeWidgetCatalogueItems
          :disabled="disabled"
          :disabled-reason="disabledReason"
          :draggable="false"
          @add="emit('add', $event)"
        />
      </div>
    </template>
  </UDrawer>
</template>
