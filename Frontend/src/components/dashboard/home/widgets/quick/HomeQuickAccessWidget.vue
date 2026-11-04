<script setup lang="ts">
import { computed } from "vue";
import { useRouter } from "vue-router";

import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { homeFavoriteFolders } from "@/queries/home-widgets";
import { useHomeStore } from "@/stores/home";
import { useTabStore } from "@/stores/tab";

import HomeQuickAccessDisplay from "./HomeQuickAccessDisplay.vue";

const { widget } = defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();
const tabs = useTabStore();
const router = useRouter();

const query = useHomeWidgetQuery(
  () => homeFavoriteFolders({ userId: store.ownerId ?? "", epoch: store.ownerEpoch }),
  (preview) => ({ userId: store.ownerId ?? "", epoch: store.ownerEpoch, value: preview.folders }),
);

const folders = computed(() => {
  const response = query.data.value;

  if (response?.userId !== store.ownerId || response.epoch !== store.ownerEpoch) return null;

  return response.value.items;
});

const authPending = computed(() => isAuthError(query.error.value));

const openFolder = (id: string, name: string) => {
  if (store.isEditing) return;

  tabs.createTab(id);
  if (tabs.activeTabId) tabs.updateTabTitle(tabs.activeTabId, name);

  void router.push("/dashboard");
};
</script>

<template>
  <div
    class="quick-access-widget flex min-h-0 flex-1 flex-col"
    :class="{ 'pointer-events-none': store.isEditing }"
  >
    <HomeWidgetDataState
      :loading="authPending || (!folders && !query.error.value)"
      :failed="!folders && Boolean(query.error.value)"
      @retry="query.refresh()"
    >
      <div
        v-if="folders && !folders.length"
        class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon name="i-heroicons-folder" class="size-8 shrink-0 text-gray-500 dark:text-gray-500" />
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No favorite folders</p>
        <p class="access-empty-hint text-xs text-gray-600 dark:text-gray-400">
          Add favorites from a folder's menu in File Explorer.
        </p>

        <UButton
          class="access-empty-action"
          to="/dashboard"
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="store.isEditing"
          >Browse folders</UButton
        >
      </div>

      <HomeQuickAccessDisplay
        v-else-if="folders"
        :folders="folders"
        :access-style="widget.options.quickAccessStyle"
        :disabled="store.isEditing"
        @open="openFolder"
      />
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
@container home-widget (max-height: 200px) {
  .access-empty-hint,
  .access-empty-action {
    display: none;
  }
}
</style>
