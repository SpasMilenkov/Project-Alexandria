<script setup lang="ts">
import { computed } from "vue";
import { useSettingsStore, TOAST_LEVELS } from "@/stores/settings";
import type { ToastLevel } from "@/stores/settings";
import { Icon } from "@iconify/vue";

const settingsStore = useSettingsStore();

const skipDeleteConfirmation = computed({
  get: () => settingsStore.skipDeleteConfirmation,
  set: (value: boolean) => settingsStore.setSkipDeleteConfirmation(value),
});

const toastLevel = computed({
  get: () => settingsStore.toastLevel,
  set: (value: ToastLevel) => settingsStore.setToastLevel(value),
});

const isOpen = computed({
  get: () => settingsStore.isBehaviorSectionOpen,
  set: (value: boolean) => settingsStore.setBehaviorSectionOpen(value),
});

const handleResetBehavior = () => {
  settingsStore.resetBehaviorSettings();
};
</script>

<template>
  <UCard class="overflow-hidden" :ui="{ body: 'p-2 sm:p-2' }">
    <UCollapsible v-model:open="isOpen">
      <UButton
        variant="ghost"
        color="neutral"
        block
        class="justify-between"
        :trailing-icon="
          isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'
        "
      >
        <div class="flex items-center gap-2">
          <Icon icon="mdi:cog" class="w-5 h-5 text-primary" />
          <h2 class="text-lg font-semibold">Behavior</h2>
        </div>
      </UButton>

      <template #content>
        <div class="pt-4 space-y-6">
          <!-- Reset -->
          <div class="flex items-center justify-end">
            <UButton
              label="Reset"
              color="error"
              variant="outline"
              size="xs"
              @click="handleResetBehavior"
            />
          </div>

          <div class="space-y-6">
            <!-- Skip delete confirmation -->
            <div class="flex items-start justify-between gap-4">
              <div class="flex flex-col gap-1">
                <span class="text-sm font-medium"
                  >Skip delete confirmation</span
                >
                <span
                  class="text-xs text-gray-500 dark:text-gray-400 leading-relaxed"
                >
                  When enabled, non-empty directories will be deleted without
                  prompting
                </span>
              </div>
              <USwitch v-model="skipDeleteConfirmation" size="lg" />
            </div>

            <USeparator />

            <!-- Toast notification level -->
            <div class="flex flex-col gap-3">
              <div class="flex flex-col gap-1">
                <span class="text-sm font-medium">Notification chatter</span>
                <span
                  class="text-xs text-gray-500 dark:text-gray-400 leading-relaxed"
                >
                  How much do you want to hear from us?
                </span>
              </div>

              <div class="flex flex-col gap-2">
                <button
                  v-for="level in TOAST_LEVELS"
                  :key="level.value"
                  type="button"
                  class="flex items-center gap-3 p-3 rounded-lg border text-left transition-colors cursor-pointer"
                  :class="
                    toastLevel === level.value
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-stone-200 dark:border-stone-700 hover:border-stone-400 dark:hover:border-stone-500'
                  "
                  @click="toastLevel = level.value"
                >
                  <Icon
                    :icon="level.icon"
                    class="w-5 h-5 shrink-0"
                    :class="
                      toastLevel === level.value
                        ? 'text-primary'
                        : 'text-gray-500 dark:text-gray-400'
                    "
                  />
                  <div class="flex flex-col gap-0.5 min-w-0">
                    <span class="text-sm font-medium leading-none">{{
                      level.label
                    }}</span>
                    <span class="text-xs text-gray-500 dark:text-gray-400">{{
                      level.description
                    }}</span>
                  </div>
                  <Icon
                    v-if="toastLevel === level.value"
                    icon="mdi:check-circle"
                    class="w-4 h-4 text-primary ml-auto shrink-0"
                  />
                </button>
              </div>
            </div>
          </div>

          <p class="text-xs text-gray-500 dark:text-gray-400">
            Disabling confirmations can lead to accidental data loss. Use with
            caution.
          </p>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
