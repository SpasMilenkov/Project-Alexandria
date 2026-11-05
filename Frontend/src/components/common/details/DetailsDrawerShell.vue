<template>
  <UDrawer
    v-model:open="open"
    :title="title"
    :description="description"
    :direction="isMobile ? 'bottom' : 'right'"
    :handle-only="!isMobile"
    :ui="drawerUi"
  >
    <template #content>
      <div class="flex min-h-0 w-full flex-col" :class="widthClass">
        <header
          class="shrink-0 border-b border-gray-200/70 px-4 pt-2 pb-4 md:px-6 md:pt-6 dark:border-gray-700/70"
        >
          <div class="flex items-start gap-3">
            <span
              class="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gray-500/10 text-gray-600 dark:text-gray-400"
            >
              <slot name="icon" />
            </span>

            <div class="min-w-0 flex-1 pt-0.5">
              <div class="group flex min-w-0 items-start gap-1">
                <p
                  class="line-clamp-2 text-base font-semibold wrap-break-word text-gray-900 dark:text-gray-100"
                  :title="title"
                >
                  {{ title }}
                </p>

                <DetailsCopyButton :value="title" :label="titleLabel" />
              </div>

              <div
                class="mt-0.5 flex min-w-0 flex-wrap items-center gap-x-1.5 gap-y-1 text-xs text-gray-600 dark:text-gray-400"
              >
                <slot name="meta" />
              </div>
            </div>

            <div class="flex shrink-0 items-center gap-1">
              <slot name="actions" />

              <UButton
                icon="i-lucide-x"
                color="neutral"
                variant="ghost"
                square
                class="justify-center"
                aria-label="Close"
                @click="open = false"
              />
            </div>
          </div>

          <div v-if="$slots.toolbar" class="mt-4">
            <slot name="toolbar" />
          </div>
        </header>

        <div
          class="flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto overscroll-contain px-4 py-5 md:px-6"
        >
          <slot />
        </div>

        <footer
          v-if="$slots.footer"
          class="shrink-0 border-t border-gray-200/70 px-4 py-4 md:px-6 dark:border-gray-700/70"
        >
          <slot name="footer" />
        </footer>
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { breakpointsTailwind, useBreakpoints } from "@vueuse/core";
import { computed } from "vue";

import { glassDrawerContent } from "@/utils/modal-ui";

import DetailsCopyButton from "./DetailsCopyButton.vue";

const {
  title,
  description = undefined,
  wide = false,
} = defineProps<{
  title: string;
  titleLabel: string;
  description?: string;
  wide?: boolean;
}>();

const open = defineModel<boolean>("open", { required: true });

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

const drawerUi = computed(() => {
  if (isMobile.value) return { content: `${glassDrawerContent} max-h-[88dvh] rounded-t-2xl` };
  return { content: glassDrawerContent };
});

const widthClass = computed(() => {
  if (wide) return "md:w-[36rem] lg:w-[40rem]";
  return "md:w-[32rem]";
});
</script>
