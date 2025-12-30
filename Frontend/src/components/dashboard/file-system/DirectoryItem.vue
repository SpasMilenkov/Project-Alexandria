<template>
  <UDrawer
    :title="data.name"
    :description="'Created ' + formatDate(data.createdAt)"
    direction="right"
    v-model:open="openDrawer"
    :ui="{ container: 'md:max-w-[40rem]' }"
  >
    <!-- Grid View -->
    <UContextMenu v-if="viewMode === 'grid'" :items="contextMenuItems">
      <div class="relative group" tabindex="0">
        <button
          type="button"
          class="w-full flex flex-col items-center gap-2 p-4 rounded-lg transition-colors cursor-pointer"
          :class="[
            isSelected
              ? 'bg-primary/20 ring-2 ring-primary'
              : 'hover:bg-primary/40 dark:hover:bg-primary/35',
          ]"
          @click="handleClick"
          @dblclick="handleDoubleClick"
        >
          <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
          <span
            class="text-sm text-center line-clamp-2 w-full wrap-break-word font-medium"
          >
            {{ data.name }}
          </span>
        </button>
      </div>
    </UContextMenu>

    <!-- List View -->
    <UContextMenu v-else :items="contextMenuItems">
      <div class="relative group" tabindex="0">
        <button
          type="button"
          class="w-full flex items-center gap-3 px-4 py-2 transition-colors cursor-pointer text-left border-b last:border-b-0"
          :class="[
            isSelected
              ? 'bg-primary/20 ring-2 ring-primary'
              : 'hover:bg-primary/40 dark:hover:bg-primary/35',
          ]"
          @click="handleClick"
          @dblclick="handleDoubleClick"
        >
          <Icon icon="mdi:folder" :width="iconSize" :height="iconSize" class="shrink-0" />
          <span class="flex-1 truncate font-medium">{{ data.name }}</span>
          <Icon icon="mdi:chevron-right" class="w-4 h-4 shrink-0" />
        </button>
      </div>
    </UContextMenu>

    <template #body>
      <div class="flex flex-col gap-6 p-1">
        <!-- Folder Preview/Icon Section -->
        <div
          class="flex items-center gap-4 p-6 bg-gray-50 dark:bg-gray-900/50 rounded-lg"
        >
          <div class="p-4 bg-white dark:bg-gray-800 rounded-lg shadow-sm">
            <Icon icon="mdi:folder" class="w-16 h-16 text-primary" />
          </div>
          <div class="flex-1 min-w-0">
            <h3 class="font-semibold text-lg truncate mb-1">
              {{ data.name }}
            </h3>
            <div
              class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400"
            >
              <Icon icon="mdi:folder-open" class="w-4 h-4" />
              <span>Directory</span>
            </div>
          </div>
        </div>

        <!-- Directory Details Grid -->
        <div>
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
            Details
          </h4>
          <div class="grid grid-cols-2 gap-4">
            <div
              class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg"
            >
              <Icon
                icon="mdi:clock-outline"
                class="w-10 h-10 text-gray-500 mt-0.5"
              />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">
                  Created
                </div>
                <div class="font-medium">
                  {{ formatDate(data.createdAt) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg"
            >
              <Icon icon="mdi:update" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">
                  Modified
                </div>
                <div class="font-medium">
                  {{ formatDate(data.updatedAt) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg col-span-2"
            >
              <Icon
                icon="mdi:identifier"
                class="w-10 h-10 text-gray-500 mt-0.5"
              />
              <div class="min-w-0 flex-1">
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">
                  Directory ID
                </div>
                <div class="font-mono text-sm truncate">{{ data.id }}</div>
              </div>
            </div>

            <div
              v-if="data.parentId"
              class="flex items-start gap-3 p-3 bg-gray-50 dark:bg-gray-900/50 rounded-lg col-span-2"
            >
              <Icon
                icon="mdi:folder-arrow-up"
                class="w-10 h-10 text-gray-500 mt-0.5"
              />
              <div class="min-w-0 flex-1">
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">
                  Parent Directory ID
                </div>
                <div class="font-mono text-sm truncate">
                  {{ data.parentId }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Owner Section -->
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <Icon icon="mdi:account" class="w-8 h-8 text-primary" />
              <span class="font-semibold">Owner</span>
            </div>
          </template>

          <div class="flex items-center gap-3">
            <UAvatar :alt="data.ownerUserDto.name" size="lg" />
            <div>
              <div class="font-medium">{{ data.ownerUserDto.name }}</div>
              <div
                class="text-sm text-gray-600 dark:text-gray-400 flex items-center gap-1.5 mt-0.5"
              >
                <Icon icon="mdi:email" class="w-4 h-4" />
                {{ data.ownerUserDto.email }}
              </div>
            </div>
          </div>
        </UCard>

        <!-- Quick Actions -->
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <Icon icon="mdi:lightning-bolt" class="w-8 h-8 text-primary" />
              <span class="font-semibold">Quick Actions</span>
            </div>
          </template>

          <div class="grid grid-cols-2 gap-2">
            <UButton
              icon="i-mdi-folder-open"
              color="primary"
              variant="soft"
              block
              @click="handleOpenDirectory"
            >
              Open
            </UButton>
            <UButton
              icon="i-mdi-pencil"
              color="gray"
              variant="soft"
              block
              @click="handleRename"
            >
              Rename
            </UButton>
            <UButton
              icon="i-mdi-folder-move"
              color="gray"
              variant="soft"
              block
              @click="handleMove"
            >
              Move
            </UButton>
            <UButton
              icon="i-mdi-download"
              color="gray"
              variant="soft"
              block
              @click="handleDownload"
            >
              Download
            </UButton>
          </div>
        </UCard>
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import type { DirectorySummaryDto } from "@/api/directory";
import { Icon } from "@iconify/vue";
import type { ContextMenuItem } from "@nuxt/ui";
import { ref, computed } from "vue";

import { useSettingsStore } from "@/stores/settings";

const settingsStore = useSettingsStore()

const iconSize = computed(() => (props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize))

const props = defineProps<{
  data: DirectorySummaryDto;
  viewMode: "grid" | "list";
  isSelected: boolean;
  selectedCount?: number;
}>();

const emit = defineEmits<{
  navigate: [directoryId: string, dirName: string];
  click: [event: MouseEvent];
  open: [directoryId: string];
  rename: [directoryId: string];
  delete: [directoryIds: string[]];
  move: [directoryIds: string];
  copy: [directoryIds: string[]];
  download: [directoryIds: string[]];
}>();

const openDrawer = ref(false);

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const items: ContextMenuItem[] = [];

  // Single-select actions
  if (!isMultiSelect) {
    items.push([
      {
        label: "Open",
        icon: "i-mdi-folder-open",
        onSelect: () => emit("open", props.data.id),
      },
    ]);

    items.push([
      {
        label: "Rename",
        icon: "i-mdi-pencil",
        onSelect: () => emit("rename", props.data.id),
        disabled: !canRename(),
      },
    ]);

    items.push([
      {
        label: "Move",
        icon: "i-mdi-folder-move",
        onSelect: () => emit("move", props.data.id),
        disabled: !canMove(),
      },
      {
        label: "Copy",
        icon: "i-mdi-content-copy",
        onSelect: () => emit("copy", [props.data.id]),
        disabled: !canCopy(),
      },
      {
        label: "Download",
        icon: "i-mdi-download",
        onSelect: () => emit("download", [props.data.id]),
        disabled: !canDownload(),
      },
    ]);

    items.push([
      {
        label: "Delete",
        icon: "i-mdi-delete",
        onSelect: () => emit("delete", [props.data.id]),
        disabled: !canDelete(),
      },
    ]);
  } else {
    // Multi-select actions
    items.push([
      {
        label: `Move ${props.selectedCount} items`,
        icon: "i-mdi-folder-move",
        onSelect: () => emit("move", props.data.id),
        disabled: !canMove(),
      },
      {
        label: `Copy ${props.selectedCount} items`,
        icon: "i-mdi-content-copy",
        onSelect: () => emit("copy", []),
        disabled: !canCopy(),
      },
      {
        label: `Download ${props.selectedCount} items`,
        icon: "i-mdi-download",
        onSelect: () => emit("download", []),
        disabled: !canDownload(),
      },
    ]);

    items.push([
      {
        label: `Delete ${props.selectedCount} items`,
        icon: "i-mdi-delete",
        onSelect: () => emit("delete", []),
        disabled: !canDelete(),
      },
    ]);
  }

  return items;
});

// Permission check stubs
const canRename = (): boolean => {
  return true;
};

const canMove = (): boolean => {
  return true;
};

const canCopy = (): boolean => {
  return true;
};

const canDownload = (): boolean => {
  return true;
};

const canDelete = (): boolean => {
  return true;
};

const formatDate = (date: string | Date): string => {
  const d = new Date(date);
  const now = new Date();
  const diff = now.getTime() - d.getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (days === 0) return "today";
  if (days === 1) return "yesterday";
  if (days < 7) return `${days} days ago`;
  if (days < 30) return `${Math.floor(days / 7)} weeks ago`;
  if (days < 365) return `${Math.floor(days / 30)} months ago`;

  return d.toLocaleDateString("bg");
};

const handleClick = (event: MouseEvent) => {
  if (!props.isSelected && event.button === 2) {
    emit("click", event);
    return;
  }
  emit("click", event);
};

const handleDoubleClick = () => {
  openDrawer.value = false;
  emit("navigate", props.data.id, props.data.name);
};

// Quick action handlers
const handleOpenDirectory = () => {
  openDrawer.value = false;
  emit("navigate", props.data.id, props.data.name);
};

const handleRename = () => {
  openDrawer.value = false;
  emit("rename", props.data.id);
};

const handleMove = () => {
  openDrawer.value = false;
  emit("move", props.data.id);
};

const handleDownload = () => {
  openDrawer.value = false;
  emit("download", [props.data.id]);
};
</script>

<style scoped></style>
