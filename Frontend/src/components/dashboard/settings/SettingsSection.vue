<template>
  <div
    class="rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
  >
    <!-- The heading wraps the toggle (accordion pattern) so headings stay navigable. -->
    <h2 class="m-0">
      <button
        type="button"
        :aria-expanded="open"
        :aria-controls="contentId"
        class="flex w-full items-center gap-3 rounded-[14px] px-4 py-3 text-left transition-colors duration-150 hover:bg-gray-900/5 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary sm:px-5 dark:hover:bg-white/5"
        @click="open = !open"
      >
        <span
          class="grid h-9 w-9 shrink-0 place-items-center rounded-lg border border-gray-200/70 text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
        >
          <Icon :icon="icon" class="h-5 w-5" />
        </span>

        <span class="min-w-0 flex-1">
          <span class="flex items-center gap-2">
            <span class="text-base font-semibold text-gray-900 dark:text-gray-100">
              {{ title }}
            </span>
            <span
              v-if="modified"
              class="h-1.5 w-1.5 shrink-0 rounded-full bg-primary"
              title="Changed from default"
            />
          </span>
          <span class="block truncate text-sm text-gray-600 dark:text-gray-400">
            {{ description }}
          </span>
        </span>

        <span
          v-if="!open && $slots.summary"
          class="hidden max-w-[55%] flex-wrap justify-end gap-1.5 lg:flex"
        >
          <slot name="summary" />
        </span>

        <UIcon
          name="i-lucide-chevron-down"
          class="h-5 w-5 shrink-0 text-gray-500 transition-transform duration-200 motion-reduce:transition-none"
          :class="{ 'rotate-180': open }"
        />
      </button>
    </h2>

    <UCollapsible
      v-model:open="open"
      :unmount-on-hide="false"
      :ui="{
        content:
          'data-[state=open]:animate-[collapsible-down_350ms_ease-in-out] data-[state=closed]:animate-[collapsible-up_200ms_ease-in-out] motion-reduce:animate-none!',
      }"
    >
      <template #content>
        <div
          :id="contentId"
          class="border-t border-gray-200/70 px-4 pb-6 pt-4 sm:px-5 dark:border-gray-700/70"
        >
          <div class="mb-4 flex justify-end">
            <UButton
              label="Reset section"
              color="error"
              variant="outline"
              size="xs"
              :disabled="!modified"
              @click="emit('reset')"
            />
          </div>

          <slot />
        </div>
      </template>
    </UCollapsible>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useId } from "vue";

const { icon, title, description, modified } = defineProps<{
  icon: string;
  title: string;
  description: string;
  modified: boolean;
}>();

const emit = defineEmits<{
  reset: [];
}>();

const open = defineModel<boolean>("open", { required: true });

const contentId = useId();
</script>
