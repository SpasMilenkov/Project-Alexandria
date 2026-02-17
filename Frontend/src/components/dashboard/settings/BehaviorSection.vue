<script setup lang="ts">
import { computed } from "vue";
import { useSettingsStore } from "@/stores/settings";
import { Icon } from "@iconify/vue";

const settingsStore = useSettingsStore();

const skipDeleteConfirmation = computed({
  get: () => settingsStore.skipDeleteConfirmation,
  set: (value: boolean) => settingsStore.setSkipDeleteConfirmation(value),
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
  <UCard class="overflow-hidden" :ui="{body: 'p-2 sm:p-2'}">
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
          <div class="flex items-center justify-between">
            <UButton
              label="Reset"
              color="gray"
              variant="ghost"
              size="xs"
              @click="handleResetBehavior"
            />
          </div>

          <div class="space-y-6">
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
          </div>

          <p class="text-xs text-gray-500">
            Disabling confirmations can lead to accidental data loss. Use with
            caution.
          </p>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
